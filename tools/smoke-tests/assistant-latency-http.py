"""Exercise stage timings through an isolated API/PostgreSQL and an in-memory provider.
No API key is loaded and the injected provider transport never performs network I/O.
"""
import io
import json
from pathlib import Path
import sys
import tempfile
import time

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/conversation-evals'))
import run as evaluator
from eval_budget import RunBudget
from eval_latency import extract_latency
from eval_proxy import EvaluationProxy

def main():
    evaluator.command(['dotnet', 'build', str(ROOT / 'src/JuridicoAI.Api'), '--no-restore'])
    requests = []

    def synthetic_transport(request, timeout):
        requests.append(request.full_url)
        if request.full_url == 'https://api.openai.com/v1/responses/input_tokens':
            time.sleep(.12)
            return io.BytesIO(b'{"input_tokens":1800}')
        assert request.full_url == 'https://api.openai.com/v1/responses'
        time.sleep(.18)
        answer = {'titulo': 'Consulta fictícia', 'texto': 'A contratante é Beatriz Santos e o valor é R$ 750,00.',
                  'resumo': '', 'intencao': 'Responder', 'alertas': [], 'grupos': [], 'substituirPropostaReferenciada': False}
        delta = {'type': 'response.output_text.delta', 'delta': json.dumps(answer, ensure_ascii=False)}
        done = {'type': 'response.completed', 'response': {'status': 'completed', 'model': 'fixture',
            'usage': {'input_tokens': 1800, 'output_tokens': 100, 'input_tokens_details': {'cached_tokens': 0}}, 'output': []}}
        return io.BytesIO(('data: ' + json.dumps(delta) + '\n\ndata: ' + json.dumps(done) + '\n\ndata: [DONE]\n\n').encode())

    with tempfile.TemporaryDirectory(prefix='juridicoai-latency-http-') as directory:
        output = Path(directory)
        proxy = EvaluationProxy('fixture', 'synthetic-unused-key', RunBudget('1', 1, '1', '.05', '5'), transport=synthetic_transport)
        product = evaluator.Product(output, proxy, 'fixture', ['1', '.05', '5'])
        try:
            proxy.start()
            product.start()
            piece, conversation, attachments = product.setup({'id': 'CONV-001', 'setup': 'draft'})
            # Seed a known document only for latency/transport verification. This
            # does not replace the separate proposal-approval integration tests.
            piece = product.http('PATCH', '/pecas/' + piece['id'], {'versao': piece['versao'], 'conteudoJson': {
                'type': 'doc', 'content': [{'type': 'paragraph', 'content': [{'type': 'text',
                'text': 'Minuta fictícia: Beatriz Santos, restituição de R$ 750,00.'}]}]}})
            expected = {'text': 'Qual é o nome da contratante e o valor da restituição na minuta aprovada? Só responda, sem propor alterações.',
                        'proposal': False, 'required': ['Beatriz Santos', '750,00'], 'forbidden': []}
            proxy.label = 'CONV-001/3-local'
            result, _, _ = product.turn(conversation, piece, expected, attachments, None, 30)
            assert not result['failures'], result['failures']
            ledger = product.export_ledger()
            assert len(ledger) == 1 and 'duration_ms' in ledger[0]
        finally:
            product.close()
            proxy.close()
        timing = extract_latency(output / 'api-operational.log')
        stages = {event['stage']: event['durationMs'] for event in timing['events'] if event['kind'] == 'stage'}
        required = {'chat.accept', 'generation.context', 'generation.prepare', 'generation.publish',
                    'generation.processing', 'provider.input_tokens', 'provider.headers', 'provider.generation'}
        assert required.issubset(stages), stages
        assert stages['provider.input_tokens'] >= 100 and stages['provider.headers'] >= 150
        assert any(event.get('GenerationId') for event in timing['events'])
        assert len(requests) == 2
        print(json.dumps({'status': 'passed_api_postgres_synthetic_provider', 'interactionSeconds': result['durationSeconds'],
            'clientTimingMs': result['clientTimingMs'], 'stageDurationMs': stages, 'ledgerDurationMs': ledger[0]['duration_ms'],
            'realExternalCalls': 0, 'syntheticProviderRequests': len(requests)}, ensure_ascii=False))

if __name__ == '__main__':
    main()
