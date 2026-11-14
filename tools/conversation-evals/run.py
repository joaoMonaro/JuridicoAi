"""Evaluate the current conversational product in an isolated disposable API/PostgreSQL.
Requires an explicit model, verified prices and total budget before any paid call.
"""
import argparse
import hashlib
import threading
from datetime import datetime, timezone
from decimal import Decimal
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET
from eval_budget import RunBudget, decimal_value
from eval_proxy import EvaluationProxy
from eval_suite import load_suite, evaluate_turn
from eval_latency import extract_latency

ROOT = Path(__file__).resolve().parents[2]
SUITE = ROOT / 'avaliacoes/assistente-conversacional/suite.json'
TERMINAL = {'Concluida', 'Interrompida', 'Incompleta', 'Desatualizada', 'Cancelada'}


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    with open(temporary, 'x', encoding='utf-8') as stream:
        os.chmod(temporary, 0o600)
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def load_config():
    values = {}
    project = ROOT / 'src/JuridicoAI.Api/JuridicoAI.Api.csproj'
    identifier = ET.parse(project).findtext('.//UserSecretsId')
    path = Path.home() / '.microsoft/usersecrets' / identifier / 'secrets.json'
    def flatten(data, prefix=''):
        for key, value in data.items():
            if isinstance(value, dict):
                flatten(value, prefix + key + ':')
            else:
                values[prefix + key] = value
    if path.exists():
        flatten(json.loads(path.read_text(encoding='utf-8-sig')))
    # Explicit environment overrides secrets for this isolated evaluation only.
    for key, value in os.environ.items():
        if key.startswith('AssistenteIa__'):
            values[key.replace('__', ':')] = value
    if os.environ.get('OPENAI_API_KEY'):
        values['AssistenteIa:ApiKey'] = os.environ['OPENAI_API_KEY']
    if os.environ.get('OPENAI_MODEL'):
        values['AssistenteIa:Model'] = os.environ['OPENAI_MODEL']
    return values


def command(arguments, **kwargs):
    result = subprocess.run(arguments, text=True, capture_output=True, check=False, **kwargs)
    if result.returncode:
        raise RuntimeError('Local command failed: ' + ' '.join(arguments[:4]))
    return result.stdout.strip()


def free_port():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    if port == 5173:
        return free_port()
    return port


def sql_literal(value):
    return "'" + str(value).replace("'", "''") + "'"


class Product:
    def __init__(self, output, proxy, model, prices):
        self.output = output
        self.proxy = proxy
        self.model = model
        self.prices = prices
        self.container = 'juridicoai-eval-' + uuid.uuid4().hex[:12]
        self.api = None
        self.log = None
        self.temp = None
        self.token = None
        self.organization = None
        self.user = None
        self.base = None
        self.started_container = False

    def sql(self, query):
        return command(['docker', '--context', 'default', 'exec', '-i', self.container,
                        'psql', '-U', 'postgres', '-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-At'], input=query)

    def start(self):
        self.temp = tempfile.TemporaryDirectory(prefix='juridicoai-eval-')
        password = uuid.uuid4().hex
        command(['docker', '--context', 'default', 'run', '--rm', '-d', '--name', self.container,
                 '-e', 'POSTGRES_PASSWORD=' + password, '-p', '127.0.0.1::5432', 'postgres:17'])
        self.started_container = True
        for _ in range(120):
            result = subprocess.run(['docker', '--context', 'default', 'exec', self.container,
                                     'pg_isready', '-U', 'postgres'], capture_output=True, check=False)
            if result.returncode == 0:
                break
            time.sleep(.25)
        else:
            raise RuntimeError('Disposable PostgreSQL did not start')
        database_port = command(['docker', '--context', 'default', 'port', self.container, '5432/tcp']).rsplit(':', 1)[1]
        port = free_port()
        self.base = f'http://127.0.0.1:{port}'
        env = {key: value for key, value in os.environ.items() if not key.startswith(('AssistenteIa__', 'OPENAI_'))}
        env.update({'ASPNETCORE_ENVIRONMENT': 'Production', 'ASPNETCORE_URLS': self.base,
                    'Postgres__ConnectionString': f'Host=127.0.0.1;Port={database_port};Database=postgres;Username=postgres;Password={password}',
                    'Auth__JwtSecret': 'synthetic-eval-auth-' + uuid.uuid4().hex,
                    'AssistenteIa__Enabled': 'true', 'AssistenteIa__ReviewV2Enabled': 'true',
                    'AssistenteIa__ConversationV2Enabled': 'true', 'AssistenteIa__AutomaticConversationEnabled': 'true',
                    'AssistenteIa__DurableGenerationEnabled': 'true', 'AssistenteIa__HistoryMemoryEnabled': 'true',
                    'AssistenteIa__Model': self.model, 'AssistenteIa__ApiKey': 'loopback-evaluation-proxy',
                    'AssistenteIa__BaseUrl': self.proxy.url,
                    'AssistenteIa__InputCostPerMillionTokensUsd': str(self.prices[0]),
                    'AssistenteIa__CachedInputCostPerMillionTokensUsd': str(self.prices[1]),
                    'AssistenteIa__OutputCostPerMillionTokensUsd': str(self.prices[2]),
                    'AssistenteIa__PricingVersion': 'evaluation-explicit-provided-prices',
                    'AssistenteDocumentos__Enabled': 'true', 'AssistenteDocumentos__RetrievalEnabled': 'true',
                    'AssistenteDocumentos__AnalysisEnabled': 'true', 'AssistenteDocumentos__OcrEnabled': 'false',
                    'DocumentStorage__RootPath': str(Path(self.temp.name) / 'files'),
                    'Logging__LogLevel__Default': 'Warning',
                    'Logging__Console__FormatterName': 'json',
                    'Logging__Console__FormatterOptions__IncludeScopes': 'true',
                    'Logging__LogLevel__JuridicoAI.Api.Infrastructure.AI.OpenAiAssistenteIaProvider': 'Information',
                    'Logging__LogLevel__JuridicoAI.Api.Infrastructure.Documents.AssistenteGenerationProcessor': 'Information',
                    'Logging__LogLevel__JuridicoAI.Api.Controllers.Assistente.AssistenteChatController': 'Information'})
        self.log = open(self.output / 'api-operational.log', 'w', encoding='utf-8')
        os.chmod(self.output / 'api-operational.log', 0o600)
        self.api = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/JuridicoAI.Api.dll'],
                                    cwd=ROOT / 'src/JuridicoAI.Api', env=env, stdout=self.log, stderr=subprocess.STDOUT)
        for _ in range(200):
            if self.api.poll() is not None:
                raise RuntimeError('Isolated API did not start; inspect protected operational log')
            try:
                self.http('GET', '/', raw=True)
                break
            except RuntimeError:
                time.sleep(.25)
        else:
            raise RuntimeError('Isolated API startup timeout')
        response = self.http('POST', '/auth/register', {'organizationName': 'Avaliação fictícia isolada',
            'name': 'Titular fictício', 'email': uuid.uuid4().hex + '@example.test', 'password': 'Synthetic-only-123!'})
        self.token = response['accessToken']
        self.organization = response['organization']['id']
        self.user = response['user']['id']
        self.sql("INSERT INTO organization_features(organization_id,feature,enabled,updated_at) VALUES " + ','.join(
            f"('{self.organization}','{feature}',true,now())" for feature in ['AI_ASSISTANT', 'AI_EDIT', 'AI_DOCUMENT_GENERATION']))
        notice = self.http('GET', '/assistente/transparencia')
        self.http('POST', '/assistente/transparencia/confirmacoes', {'versao': notice['versao']})

    def http(self, method, path, body=None, raw=False, token=None):
        headers = {'Content-Type': 'application/json'}
        if token or self.token:
            headers['Authorization'] = 'Bearer ' + (token or self.token)
        request = urllib.request.Request(self.base + path, data=json.dumps(body).encode() if body is not None else None,
                                         headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=120) as response:
                data = response.read()
                return data.decode() if raw else json.loads(data) if data else None
        except urllib.error.HTTPError as error:
            raise RuntimeError(f'Product endpoint failed ({method} {path.split("?")[0]} HTTP {error.code})') from None
        except (urllib.error.URLError, ConnectionError):
            raise RuntimeError('Isolated API is unavailable') from None

    def upload(self, conversation, source, token=None):
        boundary = 'eval-' + uuid.uuid4().hex
        body = (f'--{boundary}\r\nContent-Disposition: form-data; name="clientUploadId"\r\n\r\n{uuid.uuid4()}\r\n'
                f'--{boundary}\r\nContent-Disposition: form-data; name="arquivo"; filename="{source["name"]}"\r\n'
                'Content-Type: text/plain\r\n\r\n').encode() + source['text'].encode() + f'\r\n--{boundary}--\r\n'.encode()
        request = urllib.request.Request(self.base + f'/assistente/v2/conversas/{conversation}/anexos', data=body,
            headers={'Content-Type': 'multipart/form-data; boundary=' + boundary,
                     'Authorization': 'Bearer ' + (token or self.token)}, method='POST')
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)['id']

    def seed_history(self, conversation, version, corrected):
        turns = 40 if corrected else 502
        statements = []
        for index in range(turns):
            sequence = index * 2 + 1
            text = 'A contratante é Ana Ribeiro; valor de R$ 500,00.' if sequence == 1 else (
                'Helena Farias pagou R$ 876,54.' if sequence == 777 else f'Atualização administrativa fictícia número {sequence}. Nenhum novo fato material foi informado.')
            if corrected and index == turns - 1:
                text = 'Corrigindo: a contratante é Beatriz Santos e o valor correto é R$ 750,00. Ela pagou à Oficina Exemplo Ltda. e o serviço não foi entregue.'
            user_id, response_id = str(uuid.uuid4()), str(uuid.uuid4())
            request = {'clientRequestId': str(uuid.uuid4()), 'modo': 0, 'instrucao': text,
                       'versaoPeca': version, 'operacaoPreferida': 0, 'interacaoAutomatica': True,
                       'contexto': {'documento': {'escopo': 0, 'revisaoEditor': 0}, 'incluirDadosCaso': False,
                                    'contextoCasoIds': [], 'contextoAdicional': None}}
            statements.append(f"INSERT INTO ai_messages(id,organization_id,conversa_id,sequencia,autor,texto,estado,solicitacao,criada_em) VALUES ('{user_id}','{self.organization}','{conversation}',{sequence},'Usuario',{sql_literal(text)},'Concluida',{sql_literal(json.dumps(request))}::jsonb,now());")
            statements.append(f"INSERT INTO ai_messages(id,organization_id,conversa_id,sequencia,autor,texto,estado,resposta_a_id,criada_em) VALUES ('{response_id}','{self.organization}','{conversation}',{sequence + 1},'Assistente','Registro fictício recebido, sem aprovação de minuta.','Concluida','{user_id}',now());")
        statements.append(f"UPDATE ai_conversations SET proxima_sequencia={turns * 2 + 1} WHERE id='{conversation}';")
        self.sql('BEGIN;\n' + '\n'.join(statements) + '\nCOMMIT;')

    def setup(self, case):
        parent = self.http('POST', '/casos', {'titulo': 'Avaliação fictícia ' + case['id']})
        piece = self.http('POST', f'/casos/{parent["id"]}/pecas', {'titulo': 'Peça fictícia ' + case['id']})
        document = {'type': 'doc', 'content': [{'type': 'paragraph', 'content': [{'type': 'text', 'text': 'Anotações iniciais. Nenhuma minuta foi aprovada.'}]}]}
        piece = self.http('PATCH', '/pecas/' + piece['id'], {'versao': piece['versao'], 'conteudoJson': document})
        conversation = self.http('POST', f'/assistente/v2/pecas/{piece["id"]}/conversa', {})['id']
        if case['setup'] in ['longHistory', 'correctedHistory']:
            self.seed_history(conversation, piece['versao'], case['setup'] == 'correctedHistory')
        attachments = [self.upload(conversation, source) for source in case.get('documents', [])]
        if case['setup'] == 'foreignChat':
            other_id = str(uuid.uuid4())
            email = uuid.uuid4().hex + '@example.test'
            self.sql(f"INSERT INTO users(id,nome,email,password_hash,status,data_criacao,data_atualizacao) SELECT '{other_id}','Outro titular fictício','{email}',password_hash,'Active',now(),now() FROM users WHERE id='{self.user}'; "
                     f"INSERT INTO memberships(id,organization_id,user_id,role,data_criacao,data_atualizacao) VALUES ('{uuid.uuid4()}','{self.organization}','{other_id}','Owner',now(),now());")
            other = self.http('POST', '/auth/login', {'email': email, 'senha': 'Synthetic-only-123!', 'organizationId': self.organization})
            other_token = other['accessToken']
            notice = self.http('GET', '/assistente/transparencia', token=other_token)
            self.http('POST', '/assistente/transparencia/confirmacoes', {'versao': notice['versao']}, token=other_token)
            foreign = self.http('POST', f'/assistente/v2/pecas/{piece["id"]}/conversa', {}, token=other_token)['id']
            self.upload(foreign, {'name': 'privado-do-outro.txt', 'text': 'CANARIO_PRIVADO_OUTRO_USUARIO_43f819d2'}, other_token)
        return piece, conversation, attachments

    def turn(self, conversation, piece, turn, attachments, previous, deadline_seconds):
        request = {'clientRequestId': str(uuid.uuid4()), 'texto': turn['text'], 'versaoPecaEsperada': piece['versao'],
                   'anexoIds': attachments, 'incluirDadosCaso': False}
        if turn.get('referencePrevious'):
            if not previous:
                raise RuntimeError('Previous turn produced no proposal; dependent turn cannot be evaluated')
            request['propostaReferenciadaId'] = previous['id']
        before_tools = len(self.proxy.tools)
        before = self.http('GET', '/pecas/' + piece['id'])
        started = time.monotonic()
        sse = self.http('POST', f'/assistente/v2/conversas/{conversation}/interacoes', request, raw=True)
        enqueued_at = time.monotonic()
        frames = [json.loads(frame.split('\ndata: ', 1)[1]) for frame in sse.split('\n\n') if 'event: solicitacao.enfileirada\ndata: ' in frame]
        if len(frames) != 1:
            raise RuntimeError('Durable generation enqueue event missing')
        generation_id = frames[0]['id']
        while True:
            generation = self.http('GET', f'/assistente/v2/conversas/{conversation}/geracoes/{generation_id}')
            if generation['estado'] in TERMINAL:
                break
            if self.proxy.stop_reason or time.monotonic() - started > deadline_seconds:
                self.http('POST', f'/assistente/v2/conversas/{conversation}/geracoes/{generation_id}/cancelamento', {})
                raise RuntimeError(self.proxy.stop_reason or 'evaluation_turn_deadline')
            time.sleep(.5)
        terminal_at = time.monotonic()
        messages = self.http('GET', f'/assistente/v2/conversas/{conversation}/mensagens?limite=50')
        response = next(item for item in messages if item.get('geracaoId') == generation_id)
        proposal = (response.get('resultado') or {}).get('proposta')
        if proposal:
            proposal = self.http('GET', '/assistente/v2/propostas/' + proposal['id'])
        after = self.http('GET', '/pecas/' + piece['id'])
        unchanged = before['conteudoJson'] == after['conteudoJson'] and before['versao'] == after['versao']
        names = [item['name'] for item in self.proxy.tools[before_tools:]]
        evaluated = evaluate_turn(turn, response, proposal, names, unchanged)
        evaluated.update(durationSeconds=round(time.monotonic() - started, 3), generation=generation,
                         clientTimingMs={'enqueue': round((enqueued_at - started) * 1000, 3),
                                         'waitForTerminal': round((terminal_at - enqueued_at) * 1000, 3),
                                         'fetchResult': round((time.monotonic() - terminal_at) * 1000, 3)},
                         toolNames=names, response=response, proposal=proposal,
                         durableTransport='closed_after_enqueue', pieceChangedBeforeApproval=not unchanged)
        if previous and turn.get('referencePrevious') and proposal:
            old = self.http('GET', '/assistente/v2/propostas/' + previous['id'])
            if old.get('estado') != 'Substituida':
                evaluated['failures'].append('referenced_proposal_not_superseded')
                evaluated['status'] = 'technical_failure'
        # This is a fixture approval in an isolated synthetic database, never an application to a real user's Peça.
        if turn.get('approveSyntheticProposal') and proposal and not evaluated['failures']:
            receipt = self.http('POST', f'/assistente/v2/propostas/{proposal["id"]}/aplicacoes', {
                'requestId': str(uuid.uuid4()), 'revisaoProposta': proposal['revisao'], 'versaoDecisoes': proposal['versaoDecisoes'],
                'versaoPecaEsperada': after['versao'], 'grupos': [{'id': g['id'], 'previewSha256': g['previewSha256']} for g in proposal['grupos'] if g['estado'] == 'Pendente']})
            piece = self.http('GET', '/pecas/' + piece['id'])
            evaluated['syntheticApprovalReceipt'] = receipt
            if piece['versao'] != after['versao'] + 1:
                evaluated['failures'].append('approval_did_not_commit_expected_version')
                evaluated['status'] = 'technical_failure'
        else:
            piece = after
        return evaluated, piece, proposal or previous

    def export_ledger(self):
        return json.loads(self.sql("SELECT coalesce(json_agg(x),'[]'::json) FROM (SELECT solicitacao_id,client_request_id,correlation_id,mode,model,prompt_version,attempt_count,input_tokens,cached_input_tokens,output_tokens,status,error_code,estimated_cost_usd,duration_ms,created_at FROM ai_usage_ledger ORDER BY created_at,id) x;"))

    def close(self):
        if self.api and self.api.poll() is None:
            self.api.terminate()
            try:
                self.api.wait(timeout=8)
            except subprocess.TimeoutExpired:
                self.api.kill()
                self.api.wait()
        if self.log:
            self.log.close()
        if self.started_container:
            command(['docker', '--context', 'default', 'stop', self.container])
        if self.temp:
            self.temp.cleanup()


def report(manifest, results):
    lines = ['# Avaliação do assistente conversacional', '',
             '- Estado: ' + manifest['status'], '- Modelo: ' + manifest['model'],
             '- Commit: ' + manifest['commit'], '- Revisão jurídica formal: pendente',
             '- Dados: somente fictícios em API/PostgreSQL descartáveis',
             '- OCR: desativado', '', '| Caso/turno | Estado | Falhas |', '|---|---|---|']
    for item in results:
        lines.append('| ' + item['case'] + ' | ' + item['status'] + ' | ' + ', '.join(item.get('failures', [])) + ' |')
    lines.extend(['', 'Os critérios semânticos e de correção jurídica exigem revisão das saídas completas.',
                  'Ausência de falhas determinísticas não aprova o produto para produção.'])
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['validate', 'preflight', 'run'])
    parser.add_argument('--suite', type=Path, default=SUITE)
    parser.add_argument('--case', action='append', default=[])
    parser.add_argument('--model')
    parser.add_argument('--max-cost-usd')
    parser.add_argument('--max-calls', type=int, default=80)
    parser.add_argument('--input-usd-per-million')
    parser.add_argument('--cached-usd-per-million')
    parser.add_argument('--output-usd-per-million')
    parser.add_argument('--pricing-source')
    parser.add_argument('--turn-deadline-seconds', type=int, default=1200)
    parser.add_argument('--confirm-real-run', action='store_true')
    arguments = parser.parse_args()
    suite = load_suite(arguments.suite)
    if arguments.case:
        unknown = set(arguments.case) - {case['id'] for case in suite['cases']}
        if unknown:
            parser.error('Unknown case ID')
    cases = [case for case in suite['cases'] if not arguments.case or case['id'] in arguments.case]
    if arguments.command == 'validate':
        print(json.dumps({'suite': suite['suiteVersion'], 'cases': len(cases), 'turns': sum(len(c['turns']) for c in cases),
                          'realCalls': 0, 'legalApproval': False}, ensure_ascii=False))
        return 0
    configuration = load_config()
    model = arguments.model or configuration.get('AssistenteIa:Model')
    key = configuration.get('AssistenteIa:ApiKey')
    missing = []
    if not key or str(key).lower().startswith(('synthetic', 'fixture', 'fake', 'your_', 'sua_')):
        missing.append('AssistenteIa:ApiKey (configure locally, never paste into chat)')
    if not model:
        missing.append('model')
    for field in ['max_cost_usd', 'input_usd_per_million', 'cached_usd_per_million', 'output_usd_per_million', 'pricing_source']:
        if getattr(arguments, field) is None:
            missing.append(field)
    if missing:
        print(json.dumps({'status': 'blocked_preflight', 'missing': missing, 'realCalls': 0}, ensure_ascii=False))
        return 2
    if not re.fullmatch(r'[A-Za-z0-9._:-]{1,100}', model):
        parser.error('Invalid model ID')
    if not re.match(r'^https://(developers|platform)\.openai\.com/', arguments.pricing_source):
        parser.error('Pricing must cite a verified official documentation URL')
    prices = [decimal_value(arguments.input_usd_per_million), decimal_value(arguments.cached_usd_per_million), decimal_value(arguments.output_usd_per_million)]
    budget = RunBudget(arguments.max_cost_usd, arguments.max_calls, *prices)
    if arguments.command == 'preflight':
        print(json.dumps({'status': 'ready', 'model': model, 'maximumUsd': str(budget.maximum), 'cases': len(cases), 'realCalls': 0}))
        return 0
    if not arguments.confirm_real_run:
        parser.error('--confirm-real-run is required; this flag represents the user-authorized run')
    command(['dotnet', 'build', str(ROOT / 'src/JuridicoAI.Api'), '--no-restore'])
    commit = command(['git', 'rev-parse', 'HEAD'], cwd=ROOT)
    changes = command(['git', 'status', '--porcelain'], cwd=ROOT)
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:6]
    output = ROOT / 'avaliacoes/assistente-conversacional/execucoes' / stamp
    output.mkdir(parents=True, mode=0o700)
    budget.persist = lambda value: write_json(output / 'usage.json', value)
    audit = []
    audit_lock = threading.Lock()
    def record(event):
        with audit_lock:
            audit.append(event)
            write_json(output / 'physical-calls.json', audit)
        print(json.dumps({'progress': event['case'], 'physicalCall': event['ordinal'], 'status': event['status']}), flush=True)
    proxy = EvaluationProxy(model, key, budget, record)
    product = Product(output, proxy, model, prices)
    results = []
    manifest = {'suiteVersion': suite['suiteVersion'], 'commit': commit, 'workingTreeChanges': changes.splitlines(),
                'evaluationVersion': 'conversa-eval-1.1.0',
                'evaluationFilesSha256': {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(Path(__file__).parent.glob('*.py'))},
                'suiteSha256': hashlib.sha256(arguments.suite.read_bytes()).hexdigest(),
                'model': model, 'executedAtUtc': datetime.now(timezone.utc).isoformat(),
                'status': 'running', 'selectedCases': [case['id'] for case in cases],
                'pricingSource': arguments.pricing_source, 'pricesUsdPerMillion': list(map(str, prices)),
                'maximumUsd': str(budget.maximum), 'maximumCalls': arguments.max_calls,
                'dataPolicy': 'synthetic_only', 'manualLegalReview': 'pending',
                'defaultsPreserved': {'generationCalls': 12, 'generationTokens': 180000, 'inputTokens': 60000,
                                      'outputTokens': 6000, 'contextCharacters': 80000, 'ocrEnabled': False}}
    write_json(output / 'manifest.json', manifest)
    try:
        proxy.start()
        product.start()
        write_json(output / 'suite.json', suite)
        for case in cases:
            if proxy.stop_reason:
                raise RuntimeError(proxy.stop_reason)
            print(json.dumps({'progress': case['id'], 'stage': 'setup'}), flush=True)
            piece, conversation, attachments = product.setup(case)
            previous = None
            proxy.forbidden_input = 'CANARIO_PRIVADO_OUTRO_USUARIO_43f819d2' if case['setup'] == 'foreignChat' else None
            for index, turn in enumerate(case['turns'], 1):
                label = case['id'] + '/' + str(index)
                proxy.label = label
                result, piece, previous = product.turn(conversation, piece, turn, attachments, previous, arguments.turn_deadline_seconds)
                result['case'] = label
                result['manualCriteria'] = case['manualCriteria']
                results.append(result)
                write_json(output / 'results.json', results)
                print(json.dumps({'progress': label, 'status': result['status'], 'failures': result['failures']}), flush=True)
                if result['failures'] and turn.get('approveSyntheticProposal'):
                    break
        manifest['status'] = 'technical_failures' if any(r['failures'] for r in results) else 'pending_manual_review'
    except Exception as error:
        manifest['status'] = 'interrupted'
        manifest['stopReason'] = proxy.stop_reason or type(error).__name__
        print(json.dumps({'status': 'interrupted', 'reason': manifest['stopReason']}), flush=True)
    finally:
        try:
            if product.api and product.api.poll() is None:
                write_json(output / 'product-ledger.json', product.export_ledger())
        except Exception:
            manifest['ledgerExport'] = 'unavailable'
        try:
            product.close()
        finally:
            if proxy.thread.is_alive():
                proxy.close()
        write_json(output / 'usage.json', budget.snapshot())
        write_json(output / 'latency.json', extract_latency(output / 'api-operational.log'))
        write_json(output / 'results.json', results)
        write_json(output / 'manifest.json', manifest)
        (output / 'report.md').write_text(report(manifest, results), encoding='utf-8')
    print(json.dumps({'status': manifest['status'], 'artifacts': str(output), 'budget': budget.snapshot()['debitedOrReservedUsd']}))
    return 0 if manifest['status'] == 'pending_manual_review' else 3

if __name__ == '__main__':
    sys.exit(main())
