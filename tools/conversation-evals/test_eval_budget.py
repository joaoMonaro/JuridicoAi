import unittest
from decimal import Decimal
from eval_budget import RunBudget, BudgetExceeded, InvalidUsage, fingerprint

class BudgetTests(unittest.TestCase):
    def test_reserves_worst_case_before_charging_even_when_cached_usage_is_possible(self):
        budget = RunBudget('0.01', 3, '2', '.5', '10')
        ordinal = budget.reserve(1000, 500, 'synthetic')
        self.assertEqual(Decimal('.007'), Decimal(budget.snapshot()['debitedOrReservedUsd']))
        with self.assertRaises(BudgetExceeded):
            budget.reserve(1000, 500, 'second')
        budget.settle(ordinal, {'input_tokens': 1000, 'output_tokens': 100,
                              'input_tokens_details': {'cached_tokens': 500}}, 'fixture')
        self.assertEqual(Decimal('.00225'), Decimal(budget.snapshot()['debitedOrReservedUsd']))
        budget.reserve(1000, 500, 'third')

    def test_missing_usage_keeps_reservation_and_does_not_turn_failure_into_free_call(self):
        budget = RunBudget('.01', 1, 2, 1, 10)
        ordinal = budget.reserve(1000, 500, 'uncertain')
        with self.assertRaises(InvalidUsage):
            budget.settle(ordinal, {'input_tokens': 1000}, 'fixture')
        self.assertEqual(1, budget.snapshot()['unknownUsageCalls'])
        with self.assertRaises(BudgetExceeded):
            budget.reserve(1, 1, 'retry')

    def test_reservation_must_be_durable_before_dispatch(self):
        def fail(_):
            raise OSError('Synthetic journal failure')
        budget = RunBudget(1, 5, 1, 0, 1, persist=fail)
        with self.assertRaises(OSError):
            budget.reserve(1, 1, 'not_dispatched')
        self.assertEqual(0, budget.snapshot()['calls'])

    def test_usage_cannot_silently_exceed_counted_input_or_output(self):
        budget = RunBudget(1, 5, 1, 0, 1)
        ordinal = budget.reserve(100, 100, 'synthetic')
        with self.assertRaises(InvalidUsage):
            budget.settle(ordinal, {'input_tokens': 101, 'output_tokens': 50}, 'fixture')
        self.assertEqual(1, budget.snapshot()['unknownUsageCalls'])

    def test_accounting_failure_keeps_pessimistic_reservation(self):
        budget = RunBudget(1, 5, 1, 0, 1)
        ordinal = budget.reserve(100, 100, 'synthetic')
        def fail(_):
            raise OSError('Synthetic journal failure')
        budget.persist = fail
        with self.assertRaises(OSError):
            budget.settle(ordinal, {'input_tokens': 100, 'output_tokens': 10}, 'fixture')
        self.assertEqual('reserved', budget.snapshot()['events'][0]['state'])

    def test_rejects_unverified_zero_prices_nan_negative_and_boolean_usage(self):
        for values in [(0, 1, 1), ('NaN', 1, 1), (1, -1, 1), (1, 1, 0)]:
            with self.assertRaises(ValueError):
                RunBudget(values[0], 5, values[1], 0, values[2])
        budget = RunBudget(1, 5, 1, 0, 1)
        with self.assertRaises(InvalidUsage):
            budget.reserve(True, 100, 'invalid')

    def test_count_fingerprint_matches_only_identical_billable_input(self):
        payload = {'model': 'fixture', 'instructions': 'policy', 'input': [{'role': 'user', 'content': 'Fictício'}],
                   'tools': [{'name': 'read', 'type': 'function'}]}
        self.assertEqual(fingerprint(payload), fingerprint({**payload, 'store': False,
                         'stream': True, 'truncation': 'disabled', 'max_output_tokens': 6000}))
        self.assertNotEqual(fingerprint(payload), fingerprint({**payload, 'input': 'Different'}))
        self.assertNotEqual(fingerprint(payload), fingerprint({**payload, 'model': 'other'}))

if __name__ == '__main__':
    unittest.main()
