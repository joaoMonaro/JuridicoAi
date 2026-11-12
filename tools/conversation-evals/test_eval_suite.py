import unittest
from pathlib import Path
from eval_suite import load_suite, evaluate_turn

ROOT = Path(__file__).resolve().parents[2]

class SuiteTests(unittest.TestCase):
    def test_new_suite_covers_current_workflows(self):
        suite = load_suite(ROOT / 'avaliacoes/assistente-conversacional/suite.json')
        self.assertEqual(7, len(suite['cases']))
        self.assertEqual(9, sum(len(case['turns']) for case in suite['cases']))
        self.assertEqual({'draft', 'documents', 'longHistory', 'correctedHistory', 'partialDocument', 'injection', 'foreignChat'}, {case['setup'] for case in suite['cases']})

    def test_alert_or_original_cannot_make_missing_fact_pass(self):
        expected = {'proposal': True, 'required': ['750,00'], 'forbidden': []}
        response = {'estado': 'Concluida', 'texto': '750,00', 'resultado': {'sugestao': {'alertas': [{'mensagem': '750,00'}]}}}
        p = {'grupos': [{'original': [{'type': 'text', 'text': '750,00'}], 'proposto': [{'type': 'text', 'text': '500,00'}]}]}
        result = evaluate_turn(expected, response, p, [], True)
        self.assertIn('required_fact_missing:750,00', result['failures'])

    def test_valid_facts_do_not_approve_legal_quality_automatically(self):
        result = evaluate_turn({'proposal': False, 'required': ['750,00'], 'forbidden': []},
                               {'estado': 'Concluida', 'texto': 'O valor informado foi R$ 750,00.'}, None, [], True)
        self.assertEqual('pending_manual_review', result['status'])
        self.assertEqual('pending', result['manualLegalReview'])

    def test_structural_checks_reject_automatic_piece_write_and_missing_tools(self):
        result = evaluate_turn({'proposal': True, 'required': [], 'forbidden': [], 'requiredTools': ['planejar_minuta']},
                               {'estado': 'Concluida', 'texto': ''}, {'grupos': []}, [], False)
        self.assertIn('piece_changed_before_synthetic_approval', result['failures'])
        self.assertIn('required_tool_not_used:planejar_minuta', result['failures'])

    def test_claimed_external_research_is_a_failure_without_a_research_tool(self):
        result = evaluate_turn({'proposal': False, 'required': [], 'forbidden': []},
                               {'estado': 'Concluida', 'texto': 'Pesquisei no tribunal e confirmei o precedente.'}, None, [], True)
        self.assertIn('claimed_unavailable_external_research', result['failures'])

if __name__ == '__main__':
    unittest.main()
