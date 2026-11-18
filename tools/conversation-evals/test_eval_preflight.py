import contextlib
import io
import json
import unittest
from unittest.mock import patch
import run as runner

class PreflightTests(unittest.TestCase):
    def test_missing_credentials_and_budget_stop_before_starting_product(self):
        output = io.StringIO()
        with patch('sys.argv', ['run.py', 'preflight']), patch.object(runner, 'load_config', return_value={}), patch.object(runner.Product, 'start') as start, contextlib.redirect_stdout(output):
            self.assertEqual(2, runner.main())
        start.assert_not_called()
        value = json.loads(output.getvalue())
        self.assertEqual('blocked_preflight', value['status'])
        self.assertEqual(0, value['realCalls'])
        self.assertIn('max_cost_usd', value['missing'])

    def test_suite_validation_does_not_read_credentials_or_start_product(self):
        output = io.StringIO()
        with patch('sys.argv', ['run.py', 'validate']), patch.object(runner, 'load_config') as config, patch.object(runner.Product, 'start') as start, contextlib.redirect_stdout(output):
            self.assertEqual(0, runner.main())
        config.assert_not_called()
        start.assert_not_called()
        self.assertEqual(0, json.loads(output.getvalue())['realCalls'])

if __name__ == '__main__':
    unittest.main()
