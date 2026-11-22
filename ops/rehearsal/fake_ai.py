"""Deterministic Responses fixture. No credentials, tools or external network calls."""
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

TEXT = "Minuta fictícia: a Empresa Exemplo solicita o cumprimento do contrato. Revisão do advogado necessária."

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def do_POST(self):
        if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
            chunks = []
            while True:
                n = int(self.rfile.readline().split(b";")[0], 16)
                if not n:
                    self.rfile.readline()
                    break
                chunks.append(self.rfile.read(n))
                self.rfile.read(2)
            payload = json.loads(b"".join(chunks))
        else:
            payload = json.loads(self.rfile.read(int(self.headers.get("Content-Length", "0"))))
        if payload.get("model") != "rehearsal-fixture":
            self.send_error(400)
            return
        if self.path == "/v1/responses/input_tokens":
            body = json.dumps({"input_tokens": 1500}).encode()
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(body)
            return
        if self.path != "/v1/responses":
            self.send_error(404)
            return
        print(json.dumps({"schema": payload.get("text", {}).get("format", {}).get("name"),
            "toolChoice": payload.get("tool_choice"), "schemaProperties": list(payload.get("text", {}).get("format", {}).get("schema", {}).get("properties", {})), "synthetic": True}), flush=True)
        answer = {"titulo": "Minuta fictícia de ensaio", "texto": "Preparei uma proposta fictícia para conferir a instalação.",
            "resumo": "", "intencao": "EditarPeca", "alertas": [], "substituirPropostaReferenciada": False,
            "grupos": [{"explicacao": "Proposta sintética do ensaio de publicação",
                "operacoes": [{"startIndex": 0, "deleteCount": 1,
                    "novosBlocos": [{"type": "paragraph", "attrs": {"textAlign": None}, "content": [{"type": "text", "text": TEXT, "marks": []}]}]}]}]}
        if "coberturaPretendida" in payload.get("text", {}).get("format", {}).get("schema", {}).get("properties", {}):
            answer["coberturaPretendida"] = "Relevantes"
        completed = {"type": "response.completed", "response": {"status": "completed", "model": "rehearsal-fixture",
            "usage": {"input_tokens": 1500, "output_tokens": 150, "input_tokens_details": {"cached_tokens": 0}}, "output": []}}
        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream")
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        for event in [{"type": "response.output_text.delta", "delta": json.dumps(answer, ensure_ascii=False)}, completed]:
            self.wfile.write(("data: " + json.dumps(event, ensure_ascii=False) + "\n\n").encode())
        self.wfile.flush()
        print(json.dumps({"operation": "synthetic_generation", "model": "rehearsal-fixture", "externalCalls": 0}), flush=True)

if __name__ == "__main__":
    ThreadingHTTPServer(("0.0.0.0", 8080), Handler).serve_forever()
