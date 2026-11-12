"""Disposable HTTP proof. Uses only a newly created database and synthetic accounts.
Build API first; supply an already running, disposable PostgreSQL 17 container.
python3 tools/smoke-tests/personal-history-http.py --postgres-container NAME --postgres-port PORT
"""
import argparse
import base64
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--postgres-container', required=True)
parser.add_argument('--postgres-port', required=True, type=int)
parser.add_argument('--postgres-password', default='personal-local-test')
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
database = 'personal_smoke_' + uuid.uuid4().hex
api = None

def sql(query, db=database):
    result = subprocess.run(['docker', '--context', 'default', 'exec', '-i', args.postgres_container,
                             'psql', '-U', 'postgres', '-d', db, '-v', 'ON_ERROR_STOP=1', '-At'],
                            input=query, text=True, capture_output=True, check=False)
    if result.returncode:
        raise RuntimeError(result.stderr[:500])
    return result.stdout.strip()

def http(method, path, body=None, token=None, cookie=None, origin=None, expected=200, raw=False):
    headers = {'Content-Type': 'application/json', 'Origin': origin or base}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    if cookie:
        headers['Cookie'] = cookie
    request = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None,
                                     headers=headers, method=method)
    try:
        response = urllib.request.urlopen(request, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        data = response.read()
        assert response.status == expected, (method, path, response.status, data.decode(errors='replace')[:300])
        value = data if raw else json.loads(data) if data else None
        return value, response.headers

def get(path, token, expected=200):
    return http('GET', path, token=token, expected=expected)[0]

def cookie(headers):
    return headers['Set-Cookie'].split(';')[0]

with tempfile.TemporaryDirectory(prefix='juridicoai-personal-http-') as directory:
    try:
        sql('CREATE DATABASE ' + database, 'postgres')
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0))
            port = sock.getsockname()[1]
        assert port != 5173
        base = 'http://127.0.0.1:' + str(port)
        files = Path(directory) / 'files'
        env = os.environ.copy()
        env.update({'ASPNETCORE_ENVIRONMENT': 'Production', 'ASPNETCORE_URLS': base,
                    'Postgres__ConnectionString': f'Host=127.0.0.1;Port={args.postgres_port};Database={database};Username=postgres;Password={args.postgres_password}',
                    'Auth__JwtSecret': 'synthetic-personal-http-signing-key-1234567890',
                    'AssistenteIa__Enabled': 'true', 'AssistenteIa__ReviewV2Enabled': 'true', 'AssistenteIa__ConversationV2Enabled': 'true',
                    'AssistenteIa__AutomaticConversationEnabled': 'true', 'AssistenteIa__DurableGenerationEnabled': 'false',
                    'AssistenteIa__Model': 'synthetic-no-model-call', 'AssistenteIa__ApiKey': 'synthetic-no-real-key',
                    'AssistenteDocumentos__Enabled': 'true', 'AssistenteDocumentos__OcrEnabled': 'false',
                    'DocumentStorage__RootPath': str(files), 'Logging__LogLevel__Default': 'Warning'})
        with open(Path(directory) / 'api.log', 'w') as log:
            api = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/JuridicoAI.Api.dll'], cwd=root / 'src/JuridicoAI.Api', env=env, stdout=log, stderr=subprocess.STDOUT)
            for _ in range(100):
                if api.poll() is not None:
                    raise RuntimeError('API failed to start: ' + (Path(directory) / 'api.log').read_text()[-1500:])
                try:
                    urllib.request.urlopen(base, timeout=1).close()
                    break
                except (urllib.error.URLError, ConnectionError):
                    time.sleep(.1)
            else:
                raise RuntimeError('API startup timeout')
            email = uuid.uuid4().hex + '@example.test'
            registration, _ = http('POST', '/auth/register', {'organizationName': 'Escritório fictício', 'name': 'Titular fictício', 'email': email, 'password': 'Synthetic-only-123!'}, expected=201)
            organization = registration['organization']['id']
            uid = registration['user']['id']
            regular = registration['accessToken']
            sql(f"INSERT INTO organization_features(organization_id,feature,enabled,updated_at) VALUES ('{organization}','AI_ASSISTANT',true,now()),('{organization}','AI_EDIT',true,now()),('{organization}','AI_DOCUMENT_GENERATION',true,now())")
            case, _ = http('POST', '/casos', {'titulo': 'Caso fictício'}, token=regular, expected=201)
            piece, _ = http('POST', f"/casos/{case['id']}/pecas", {'titulo': 'Peça histórica'}, token=regular, expected=201)
            document = {'type': 'doc', 'content': [{'type': 'paragraph', 'content': [{'type': 'text', 'text': 'Primeiro original.'}]}]}
            piece, _ = http('PATCH', '/pecas/' + piece['id'], {'versao': piece['versao'], 'conteudoJson': document}, token=regular)
            conv, _ = http('POST', f"/assistente/v2/pecas/{piece['id']}/conversa", {}, token=regular)
            cid = conv['id']
            notice = get('/assistente/transparencia', regular)
            http('POST', '/assistente/transparencia/confirmacoes', {'versao': notice['versao']}, token=regular)
            proposal, _ = http('POST', f'/assistente/v2/conversas/{cid}/propostas', {'clientRequestId': str(uuid.uuid4()), 'versaoPecaEsperada': piece['versao'], 'instrucao': 'Melhore a redação.', 'textoResposta': 'Proposta histórica salva.',
                                'grupos': [{'explicacao': 'Ajuste histórico', 'operacoes': [{'startIndex': 0, 'deleteCount': 1, 'novosBlocos': [{'type': 'paragraph', 'content': [{'type': 'text', 'text': 'Texto proposto.'}]}]}]}]}, token=regular)
            boundary = 'boundary-' + uuid.uuid4().hex
            content = b'Anexo historico do titular.'
            multipart = (f'--{boundary}\r\nContent-Disposition: form-data; name="clientUploadId"\r\n\r\n{uuid.uuid4()}\r\n--{boundary}\r\nContent-Disposition: form-data; name="arquivo"; filename="privado.txt"\r\nContent-Type: text/plain\r\n\r\n').encode() + content + f'\r\n--{boundary}--\r\n'.encode()
            request = urllib.request.Request(base + f'/assistente/v2/conversas/{cid}/anexos', data=multipart, headers={'Content-Type': f'multipart/form-data; boundary={boundary}', 'Authorization': 'Bearer ' + regular}, method='POST')
            with urllib.request.urlopen(request) as response:
                attachment = json.load(response)
            shared = files / 'case' / 'shared.txt'
            shared.parent.mkdir(parents=True, exist_ok=True)
            shared.write_text('Documento compartilhado do Caso.')
            shared_id = str(uuid.uuid4())
            sql(f"INSERT INTO documentos_caso(id,caso_id,nome_original,nome_armazenado,tipo,mime_type,tamanho_bytes,storage_id,data_upload) VALUES ('{shared_id}','{case['id']}','shared.txt','shared.txt','Outro','text/plain',30,'case/shared.txt',now())")
            get(f'/assistente/v2/conversas/{cid}/anexos', regular)
            source_id = sql(f"SELECT id FROM ai_documents WHERE documento_caso_id='{shared_id}'")
            personal, headers = http('POST', '/auth/pessoal/login', {'email': email, 'senha': 'Synthetic-only-123!'})
            personal_token = personal['accessToken']
            old_cookie = cookie(headers)
            cookie_header = headers['Set-Cookie'].lower()
            assert 'secure' in cookie_header and 'httponly' in cookie_header and 'samesite=strict' in cookie_header and 'path=/auth/pessoal' in cookie_header
            payload = json.loads(base64.urlsafe_b64decode(personal_token.split('.')[1] + '=='))
            assert payload['aud'] == 'JuridicoAI.History' and 'organizationId' not in payload and 'role' not in payload
            get(f'/assistente/v2/pessoal/conversas/{cid}', regular, 401)
            for path in [f"/pecas/{piece['id']}", f"/casos/{case['id']}", '/assistente/capacidades']:
                get(path, personal_token, 401)
            http('POST', f'/assistente/v2/conversas/{cid}/interacoes', {'clientRequestId': str(uuid.uuid4()), 'texto': 'Não autorizado', 'versaoPecaEsperada': piece['versao']}, token=personal_token, expected=401)
            http('POST', '/auth/pessoal/refresh', cookie=old_cookie, origin='https://forbidden.example', expected=403)
            rotated, headers = http('POST', '/auth/pessoal/refresh', cookie=old_cookie)
            active_cookie = cookie(headers)
            personal_token = rotated['accessToken']
            http('POST', '/auth/pessoal/refresh', cookie=old_cookie, expected=401)
            sql(f"DELETE FROM memberships WHERE user_id='{uid}'")
            get(f"/pecas/{piece['id']}", regular, 401)
            http('POST', '/auth/login', {'email': email, 'senha': 'Synthetic-only-123!'}, expected=401)
            history_root = '/assistente/v2/pessoal'
            personal_page = get(history_root + '/conversas', personal_token)
            assert [c['id'] for c in personal_page['conversas']] == [cid]
            messages = get(history_root + f'/conversas/{cid}/mensagens', personal_token)
            assert len(messages) == 2 and messages[-1]['texto'] == 'Proposta histórica salva.'
            assert not any(k in json.dumps(messages) for k in ['documentoBase', 'fontesDocumentais', 'geracaoId', 'solicitacao'])
            raw, _ = http('GET', history_root + f"/conversas/{cid}/anexos/{attachment['id']}/arquivo", token=personal_token, raw=True)
            assert raw == content
            get(history_root + f'/conversas/{cid}/anexos/{source_id}/arquivo', personal_token, 404)
            assert len(get(history_root + '/propostas/' + proposal['id'], personal_token)['grupos']) == 1
            other_id = str(uuid.uuid4())
            other_email = uuid.uuid4().hex + '@example.test'
            sql(f"INSERT INTO users(id,nome,email,password_hash,status,data_criacao,data_atualizacao) SELECT '{other_id}','Outro titular','{other_email}',password_hash,'Active',now(),now() FROM users WHERE id='{uid}'")
            other, _ = http('POST', '/auth/pessoal/login', {'email': other_email, 'senha': 'Synthetic-only-123!'})
            get(history_root + f'/conversas/{cid}', other['accessToken'], 404)
            http('DELETE', history_root + f'/conversas/{cid}', {'requestId': str(uuid.uuid4()), 'confirmarExclusao': True}, token=other['accessToken'], expected=404)
            rid = str(uuid.uuid4())
            http('DELETE', history_root + f'/conversas/{cid}', {'requestId': rid, 'confirmarExclusao': False}, token=personal_token, expected=400)
            accepted, _ = http('DELETE', history_root + f'/conversas/{cid}', {'requestId': rid, 'confirmarExclusao': True}, token=personal_token, expected=202)
            get(history_root + f'/conversas/{cid}', personal_token, 404)
            for _ in range(100):
                status = get(history_root + '/exclusoes/' + rid, personal_token)
                if status['estado'] == 'RemovidaDoAmbienteAtivo':
                    break
                time.sleep(.1)
            else:
                raise RuntimeError('Authorized purge did not complete: ' + str(status))
            replay, _ = http('DELETE', history_root + f'/conversas/{cid}', {'requestId': rid, 'confirmarExclusao': True}, token=personal_token, expected=202)
            assert replay['requestId'] == accepted['requestId'] and replay['eliminadaDasCopias'] is False
            assert sql(f"SELECT count(*) FROM ai_messages WHERE conversa_id='{cid}'") == '0'
            assert sql(f"SELECT count(*) FROM ai_proposals WHERE conversa_id='{cid}'") == '0'
            assert sql(f"SELECT count(*) FROM ai_documents WHERE conversa_id='{cid}'") == '0'
            assert sql(f"SELECT versao FROM pecas WHERE id='{piece['id']}'") == str(piece['versao'])
            assert shared.exists() and not list(files.glob('assistant/**/*.*'))
            assert sql('SELECT count(*) FROM ai_usage_ledger') == '0'
            sql(f"UPDATE users SET status='Inactive' WHERE id='{uid}'")
            get(history_root + '/conversas', personal_token, 401)
            sql(f"UPDATE users SET status='Active' WHERE id='{uid}'")
            http('POST', '/auth/pessoal/logout', cookie=old_cookie, expected=204)
            get(history_root + '/conversas', personal_token, 401)
            http('POST', '/auth/pessoal/refresh', cookie=active_cookie, expected=401)
            print(json.dumps({'apiReal': True, 'postgresReal': True, 'semChamadasDeIA': True, 'historicoSemMembership': True,
                              'isolamentoDeTokens': True, 'isolamentoDeTitular': True, 'cookieSeguro': True, 'origemVerificada': True,
                              'refreshRotacionado': True, 'contaDesabilitadaBloqueada': True, 'logoutComCookieAnteriorRevogaAccessToken': True,
                              'exclusaoConfirmadaEIdempotente': True, 'pecaEDocumentoCompartilhadoPreservados': True,
                              'protocoloSemConteudo': True, 'eliminacaoDeBackupsConfirmada': False, 'porta': port}, ensure_ascii=False))
    finally:
        if api and api.poll() is None:
            api.terminate()
            try:
                api.wait(timeout=8)
            except subprocess.TimeoutExpired:
                api.kill()
                api.wait()
        sql('DROP DATABASE IF EXISTS ' + database + ' WITH (FORCE)', 'postgres')
