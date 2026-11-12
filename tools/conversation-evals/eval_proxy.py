"""Loopback-only Responses proxy for a bounded, synthetic evaluation.
Uses the product provider unchanged; records usage and tool names, never auth or reasoning.
"""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import threading
import urllib.error
import urllib.request
from eval_budget import fingerprint, token_count, BudgetExceeded

class EvaluationProxy:
    def __init__(self, model, api_key, budget, audit=None, transport=None):
        self.model = model
        self.api_key = api_key
        self.budget = budget
        self.audit = audit or (lambda _: None)
        self.transport = transport or urllib.request.urlopen
        self.counts = {}
        self.tools = []
        self.label = 'preflight'
        self.stop_reason = None
        self.forbidden_input = None
        self.lock = threading.Lock()
        state = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def do_POST(self):
                self.headers_sent = False
                try:
                    self.handle_request()
                except (BrokenPipeError, ConnectionResetError):
                    pass
                except Exception as error:
                    state.stop_reason = state.stop_reason or type(error).__name__
                    if not self.headers_sent:
                        self.respond(502, {'error': {'message': 'Evaluation guard stopped; inspect the local budget journal'}})
                    else:
                        self.close_connection = True

            def read_body(self):
                if self.headers.get('Transfer-Encoding', '').lower() == 'chunked':
                    chunks = []
                    size = 0
                    while True:
                        length = int(self.rfile.readline().split(b';')[0], 16)
                        if length == 0:
                            self.rfile.readline()
                            return b''.join(chunks)
                        size += length
                        if size > 4 * 1024 * 1024:
                            raise ValueError('Request exceeds evaluation scope')
                        chunks.append(self.rfile.read(length))
                        self.rfile.read(2)
                length = int(self.headers.get('Content-Length', '0'))
                if length > 4 * 1024 * 1024:
                    raise ValueError('Request exceeds evaluation scope')
                return self.rfile.read(length)

            def respond(self, code, body):
                data = json.dumps(body).encode()
                self.headers_sent = True
                self.send_response(code)
                self.send_header('Content-Type', 'application/json')
                self.send_header('Content-Length', str(len(data)))
                self.end_headers()
                self.wfile.write(data)

            def handle_request(self):
                if self.path not in ['/v1/responses', '/v1/responses/input_tokens']:
                    return self.respond(404, {'error': {'message': 'Only Responses and token counting are allowed'}})
                label = state.label
                raw = self.read_body()
                payload = json.loads(raw)
                if state.stop_reason:
                    return self.respond(402, {'error': {'message': 'Evaluation stopped'}})
                if payload.get('model') != state.model or any(t.get('type') != 'function' for t in payload.get('tools', [])):
                    state.stop_reason = 'model_or_tool_scope_changed'
                    raise ValueError('Unexpected model or hosted tool')
                if state.forbidden_input and state.forbidden_input in raw.decode():
                    state.stop_reason = 'foreign_private_input_detected'
                    return self.respond(403, {'error': {'message': 'Private cross-conversation canary detected'}})
                request_hash = fingerprint(payload)
                count_request = self.path.endswith('/input_tokens')
                ordinal = None
                if not count_request:
                    if payload.get('store') is not False or payload.get('truncation') != 'disabled':
                        state.stop_reason = 'privacy_or_coverage_policy_changed'
                        raise ValueError('Product privacy/coverage settings changed')
                    with state.lock:
                        counted = state.counts.get(request_hash)
                    if counted is None:
                        state.stop_reason = 'input_count_not_found'
                        raise ValueError('No exact token count exists for this physical call')
                    try:
                        ordinal = state.budget.reserve(counted, payload.get('max_output_tokens'), request_hash)
                    except BudgetExceeded:
                        state.stop_reason = 'budget_exhausted'
                        return self.respond(402, {'error': {'message': 'Explicit evaluation budget exhausted'}})
                request = urllib.request.Request('https://api.openai.com' + self.path, data=raw,
                    headers={'Authorization': 'Bearer ' + state.api_key, 'Content-Type': 'application/json',
                             'Accept': 'application/json' if count_request else 'text/event-stream'}, method='POST')
                try:
                    response = state.transport(request, timeout=120)
                except urllib.error.HTTPError as error:
                    # Do not log provider error bodies or credentials. A failed generation keeps its full reservation.
                    status = error.code
                    error.close()
                    state.stop_reason = 'upstream_http_' + str(status)
                    if ordinal:
                        state.audit({'case': label, 'ordinal': ordinal, 'status': state.stop_reason})
                    return self.respond(status, {'error': {'message': 'Upstream request failed during evaluation'}})
                with response:
                    if count_request:
                        data = json.load(response)
                        counted = token_count(data.get('input_tokens'))
                        with state.lock:
                            state.counts[request_hash] = counted
                        return self.respond(200, data)
                    self.headers_sent = True
                    self.send_response(200)
                    self.send_header('Content-Type', 'text/event-stream')
                    self.send_header('Cache-Control', 'no-store')
                    self.end_headers()
                    completed = False
                    while True:
                        line = response.readline(4 * 1024 * 1024)
                        if not line:
                            break
                        if line.startswith(b'data: ') and line[6:].strip() != b'[DONE]':
                            event = json.loads(line[6:])
                            if event.get('type') == 'response.completed':
                                result = event.get('response', {})
                                if result.get('status') != 'completed':
                                    state.stop_reason = 'provider_incomplete'
                                else:
                                    state.budget.settle(ordinal, result.get('usage') or {}, result.get('model'))
                                    names = [item.get('name') for item in result.get('output', []) if item.get('type') == 'function_call']
                                    state.tools.extend({'case': label, 'name': name, 'ordinal': ordinal} for name in names)
                                    state.audit({'case': label, 'ordinal': ordinal, 'tools': names,
                                                 'status': 'completed', 'requestSha256': request_hash})
                                    completed = True
                        self.wfile.write(line)
                        self.wfile.flush()
                    if not completed:
                        state.stop_reason = state.stop_reason or 'usage_or_completion_unknown'
        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)

    @property
    def url(self):
        return f'http://127.0.0.1:{self.server.server_port}/v1/'

    def start(self):
        self.thread.start()

    def close(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=5)
