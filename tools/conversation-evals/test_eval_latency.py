import json
from pathlib import Path
import tempfile
import unittest
from eval_latency import extract_latency

class LatencyTests(unittest.TestCase):
    def extract(self, rows):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'api.log'
            path.write_text('\n'.join(json.dumps(row) for row in rows), encoding='utf-8')
            return extract_latency(path)

    def test_exports_only_technical_fields_and_valid_correlation_ids(self):
        generation = '430ba894-03a4-4b10-9edd-fee1d1b2148a'
        result = self.extract([{'EventId': 6401,
            'Message': 'PRIVATE_MESSAGE', 'Exception': 'PRIVATE_EXCEPTION',
            'State': {'Stage': 'provider.input_tokens', 'DurationMs': 145.2, 'prompt': 'PRIVATE_PROMPT'},
            'Scopes': [{'GenerationId': generation, 'ClientRequestId': 'PRIVATE_TOKEN', 'title': 'PRIVATE_TITLE'}]}])
        self.assertEqual([{'kind': 'stage', 'stage': 'provider.input_tokens', 'durationMs': 145.2,
                           'GenerationId': generation}], result['events'])
        self.assertNotIn('PRIVATE_', json.dumps(result))

    def test_distinguishes_local_capacity_wait_from_provider_duration(self):
        result = self.extract([
            {'EventId': 6402, 'State': {'Reason': 'AI_USER_RATE_LIMITED', 'RetryAfterMs': 2000}},
            {'EventId': 6403, 'State': {'State': 'NaFila', 'SinceCreatedMs': 3100}},
            {'EventId': 6401, 'State': {'Stage': 'provider.generation', 'DurationMs': 30}}])
        self.assertEqual(['deferred', 'claim', 'stage'], [event['kind'] for event in result['events']])
        self.assertTrue(result['available'])

    def test_old_or_unrelated_logs_do_not_claim_zero_latency(self):
        self.assertFalse(self.extract([{'EventId': 12, 'State': {'DurationMs': 0}},
            {'EventId': 6401, 'State': {'Stage': 'PRIVATE_PROMPT', 'DurationMs': 1}},
            {'EventId': 6401, 'State': {'Stage': 'provider.generation', 'DurationMs': -1}}])['available'])
        with tempfile.TemporaryDirectory() as directory:
            self.assertFalse(extract_latency(Path(directory) / 'absent')['available'])

    def test_malformed_shapes_and_nonfinite_durations_are_ignored(self):
        rows = [None, [], {'EventId': 6401, 'State': {'Stage': {'secret': 'PRIVATE'}}},
                {'EventId': 6402, 'State': {'Reason': []}},
                {'EventId': 6403, 'State': {'State': {}}}]
        rows += [{'EventId': 6401, 'State': {'Stage': 'provider.generation', 'DurationMs': value}}
                 for value in [True, float('inf'), 10 ** 400]]
        self.assertFalse(self.extract(rows)['available'])
        self.assertTrue(self.extract([{'EventId': 6401, 'State': {
            'Stage': 'provider.generation', 'DurationMs': 1}, 'Scopes': None}])['available'])

if __name__ == '__main__':
    unittest.main()
