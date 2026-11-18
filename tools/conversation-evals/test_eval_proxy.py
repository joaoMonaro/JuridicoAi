import io
import json
import unittest
import urllib.error
import urllib.request
from eval_budget import RunBudget
from eval_proxy import EvaluationProxy

class ProxyTests(unittest.TestCase):
    def start(self, budget=None, malformed=False):
        self.calls = []
        def transport(request, timeout):
            body = json.loads(request.data)
            self.calls.append(request.full_url)
            if request.full_url.endswith('/input_tokens'):
                return io.BytesIO(json.dumps({'input_tokens': 100}).encode())
            self.assertEqual('reserved', self.proxy.budget.snapshot()['events'][-1]['state'])
            self.assertEqual('Bearer synthetic-key', request.get_header('Authorization'))
            usage = {} if malformed else {'input_tokens': 100, 'output_tokens': 20, 'input_tokens_details': {'cached_tokens': 0}}
            event = {'type': 'response.completed', 'response': {'status': 'completed', 'model': 'fixture', 'usage': usage,
                     'output': [{'type': 'function_call', 'name': 'planejar_minuta', 'arguments': '{"private":"must not persist"}'},
                                {'type': 'reasoning', 'encrypted_content': 'never-persist-this'}]}}
            return io.BytesIO(('data: ' + json.dumps(event) + '\n\ndata: [DONE]\n\n').encode())
        self.audit = []
        self.proxy = EvaluationProxy('fixture', 'synthetic-key', budget or RunBudget(1, 5, 1, 0, 1), self.audit.append, transport)
        self.proxy.start()
        self.addCleanup(self.proxy.close)
        return {'model': 'fixture', 'instructions': 'policy', 'input': 'Synthetic only', 'tools': [], 'tool_choice': 'auto'}

    def post(self, path, payload):
        request = urllib.request.Request(self.proxy.url + path, data=json.dumps(payload).encode(), method='POST', headers={'Content-Type': 'application/json'})
        try:
            with urllib.request.urlopen(request, timeout=3) as response:
                return response.status, response.read()
        except urllib.error.HTTPError as error:
            with error:
                return error.code, error.read()

    def test_budget_reserved_before_generation_and_only_safe_audit_is_saved(self):
        payload = self.start()
        self.assertEqual(200, self.post('responses/input_tokens', payload)[0])
        code, _ = self.post('responses', {**payload, 'store': False, 'truncation': 'disabled', 'stream': True, 'max_output_tokens': 100})
        self.assertEqual(200, code)
        self.assertEqual('settled', self.proxy.budget.snapshot()['events'][0]['state'])
        self.assertEqual(['planejar_minuta'], self.audit[0]['tools'])
        self.assertNotIn('never-persist-this', json.dumps(self.audit))
        self.assertNotIn('must not persist', json.dumps(self.audit))

    def test_missing_count_prevents_paid_dispatch(self):
        payload = self.start()
        self.assertEqual(502, self.post('responses', {**payload, 'store': False, 'truncation': 'disabled', 'max_output_tokens': 100})[0])
        self.assertEqual([], self.calls)
        self.assertEqual(0, self.proxy.budget.snapshot()['calls'])

    def test_missing_usage_retains_reservation_and_stops_further_generation(self):
        payload = self.start(malformed=True)
        self.post('responses/input_tokens', payload)
        self.post('responses', {**payload, 'store': False, 'truncation': 'disabled', 'max_output_tokens': 100})
        self.assertEqual(1, self.proxy.budget.snapshot()['unknownUsageCalls'])
        self.assertTrue(self.proxy.stop_reason)
        self.assertEqual(402, self.post('responses/input_tokens', payload)[0])
        self.assertEqual(2, len(self.calls))

    def test_foreign_private_canary_never_leaves_loopback(self):
        payload = self.start()
        self.proxy.forbidden_input = 'CANARY_FOREIGN'
        code, _ = self.post('responses/input_tokens', {**payload, 'input': 'CANARY_FOREIGN'})
        self.assertEqual(403, code)
        self.assertEqual([], self.calls)

    def test_wrong_model_or_hosted_tool_never_leaves_loopback(self):
        payload = self.start()
        self.assertEqual(502, self.post('responses/input_tokens', {**payload, 'tools': [{'type': 'web_search'}]})[0])
        self.assertEqual([], self.calls)

    def test_budget_exhaustion_does_not_dispatch_the_next_call(self):
        payload = self.start(budget=RunBudget('.00001', 2, 1, 0, 1))
        self.post('responses/input_tokens', payload)
        self.assertEqual(402, self.post('responses', {**payload, 'store': False, 'truncation': 'disabled', 'max_output_tokens': 100})[0])
        self.assertEqual(1, len(self.calls))
        self.assertEqual(0, self.proxy.budget.snapshot()['calls'])

if __name__ == '__main__':
    unittest.main()
