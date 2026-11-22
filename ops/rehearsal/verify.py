"""Checks against the real packaged API; only the model is a deterministic fixture."""
import hashlib
import base64
import io
import json
import ssl
import subprocess
import time
import urllib.error
import urllib.request
import uuid
import zipfile
import xml.etree.ElementTree as ET

TEXT = "Minuta fictícia: a Empresa Exemplo solicita o cumprimento do contrato. Revisão do advogado necessária."

def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")
    path.chmod(0o600)

class Client:
    def __init__(self, run):
        self.run = run
        self.token = None
        self.base = "https://127.0.0.1:" + run.cfg["HTTPS_PORT"]
        self.context = ssl.create_default_context(cafile=str(run.state / "config/certs/local.crt"))

    def request(self, method, path, body=None, raw=False, expected=200, headers=None):
        values = {"Host": "api.juridicoai.test:" + self.run.cfg["HTTPS_PORT"],
                  "Origin": "https://app.juridicoai.test:" + self.run.cfg["HTTPS_PORT"]}
        if self.token:
            values["Authorization"] = "Bearer " + self.token
        if body is not None and not isinstance(body, bytes):
            body = json.dumps(body, ensure_ascii=False).encode()
            values["Content-Type"] = "application/json"
        values.update(headers or {})
        request = urllib.request.Request(self.base + path, data=body, headers=values, method=method)
        try:
            response = urllib.request.urlopen(request, context=self.context, timeout=100)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            data = response.read()
            if response.status != expected:
                raise RuntimeError(f"{method} {path}: HTTP {response.status}; " + data.decode(errors="replace")[:500])
            self.headers = response.headers
            return data if raw else json.loads(data) if data else None

    def login(self):
        result = self.request("POST", "/auth/login", {"email": "pilot@example.test", "senha": self.run.cfg["PILOT_PASSWORD"]})
        cookie = self.headers.get("Set-Cookie", "")
        assert "secure" in cookie.lower() and "httponly" in cookie.lower()
        self.token = result["accessToken"]
        return result

    def use_expected(self, expected):
        self.token = expected["accessToken"]
        claims = self.token.split(".")[1]
        expires = json.loads(base64.urlsafe_b64decode(claims + "=" * (-len(claims) % 4)))["exp"]
        if expires <= time.time() + 30:
            self.login()
            expected["accessToken"] = self.token
            write(self.run.state / "expected.json", expected)

def poll(action, accepted, seconds=75):
    deadline = time.monotonic() + seconds
    value = None
    while time.monotonic() < deadline:
        value = action()
        if accepted(value):
            return value
        time.sleep(.5)
    raise RuntimeError("Expected terminal state was not reached: " + json.dumps(value, ensure_ascii=False)[:600])

def paragraph(text):
    return {"type": "paragraph", "content": [{"type": "text", "text": text}]}

def export(client, run, piece, directory):
    directory.mkdir(mode=0o700, exist_ok=True)
    for extension in ["docx", "pdf"]:
        data = client.request("POST", f"/pecas/{piece['id']}/exportar", {"formato": extension, "versao": piece["versao"]}, raw=True)
        (directory / ("piece." + extension)).write_bytes(data)
        if extension == "pdf":
            assert data.startswith(b"%PDF-")
            text = run.compose("exec", "-T", "api", "pdftotext", "-", "-", input=data).stdout.decode()
            assert "ESCRITORIO FICTICIO" in text and "Rodape de ensaio" in text
        else:
            with zipfile.ZipFile(io.BytesIO(data)) as doc:
                text = " ".join("".join(ET.fromstring(doc.read(name)).itertext()) for name in doc.namelist() if name.startswith("word/") and name.endswith(".xml"))
                assert "ESCRITORIO FICTICIO" in text and "Rodape de ensaio" in text
        for block in piece["conteudoJson"]["content"]:
            for node in block.get("content", []):
                if node.get("text"):
                    assert " ".join(node["text"].split()) in " ".join(text.split()), (extension, node["text"])

def upload(client, conversation, name, data):
    boundary = "fixture-" + uuid.uuid4().hex
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="clientUploadId"\r\n\r\n{uuid.uuid4()}\r\n'
            f'--{boundary}\r\nContent-Disposition: form-data; name="arquivo"; filename="{name}"\r\n'
            'Content-Type: application/octet-stream\r\n\r\n').encode() + data + f"\r\n--{boundary}--\r\n".encode()
    return client.request("POST", f"/assistente/v2/conversas/{conversation}/anexos", body, expected=202,
                          headers={"Content-Type": "multipart/form-data; boundary=" + boundary})

def seed(run):
    assert not (run.state / "expected.json").exists(), "Seed already exists; use verify"
    run.check_network()
    client = Client(run)
    client.request("GET", "/casos", expected=401)
    client.request("POST", "/auth/register", {"organizationName": "Bloqueado", "name": "Fictício",
                   "email": "blocked@example.test", "password": "Synthetic-only-123!"}, expected=403)
    plain_http = run.compose("exec", "-T", "api", "curl", "-s", "-o", "/dev/null", "-w", "%{http_code}", "http://127.0.0.1:8080/health/live", text=True).stdout
    assert plain_http == "400"
    identity = client.login()
    notice = client.request("GET", "/assistente/transparencia")
    client.request("POST", "/assistente/transparencia/confirmacoes", {"versao": notice["versao"]})
    caps = client.request("GET", "/assistente/capacidades")
    assert caps["habilitado"] and not caps["documentos"]["ocrHabilitado"]
    case = client.request("POST", "/casos", {"titulo": "Ensaio local fictício"}, expected=201)
    piece = client.request("POST", f"/casos/{case['id']}/pecas", {"titulo": "Peça de ensaio"}, expected=201)
    path = "/pecas/" + piece["id"]
    client.request("PATCH", path, {"versao": piece["versao"], "conteudoJson": {"type": "doc", "content": [paragraph("Texto inicial fictício do ensaio."), paragraph("Segundo parágrafo fictício, preservado na edição parcial.")]},
        "headerJson": {"type": "doc", "content": [paragraph("ESCRITORIO FICTICIO")]},
        "footerJson": {"type": "doc", "content": [paragraph("Rodape de ensaio")]} })
    piece = client.request("GET", path)
    export(client, run, piece, run.state / "fixtures")
    conversation = client.request("POST", f"/assistente/v2/pecas/{piece['id']}/conversa")["id"]
    attachments = []
    for name, data in [("fatos.txt", "Contrato fictício da Empresa Exemplo. Nenhum dado real.\n".encode()),
                       ("documento.docx", (run.state / "fixtures/piece.docx").read_bytes()),
                       ("documento.pdf", (run.state / "fixtures/piece.pdf").read_bytes())]:
        item = upload(client, conversation, name, data)
        attachments.append({"id": item["id"], "name": name, "sha256": hashlib.sha256(data).hexdigest()})
    documents = poll(lambda: client.request("GET", f"/assistente/v2/conversas/{conversation}/anexos"),
                     lambda rows: len(rows) == 3 and all(r["estado"] not in ["Recebido", "Processando"] for r in rows))
    assert all(d["estado"] == "Concluido" and d["trechosConfiaveis"] > 0 for d in documents), documents
    request_id = str(uuid.uuid4())
    body = {"clientRequestId": request_id, "texto": "Reescreva o primeiro parágrafo para conferir uma proposta fictícia.",
            "versaoPecaEsperada": piece["versao"], "anexoIds": [d["id"] for d in attachments]}
    stream = client.request("POST", f"/assistente/v2/conversas/{conversation}/interacoes", body, raw=True).decode()
    assert "solicitacao.enfileirada" in stream
    messages = poll(lambda: client.request("GET", f"/assistente/v2/conversas/{conversation}/mensagens?limite=50"),
                    lambda rows: any(m["autor"] == "Assistente" and m["estado"] in ["Concluida", "Falhou", "Interrompida", "Incompleta"] for m in rows))
    answer = next(m for m in messages if m["autor"] == "Assistente")
    assert answer["estado"] == "Concluida", answer
    proposal = answer["resultado"]["proposta"]
    assert proposal and client.request("GET", path)["versao"] == piece["versao"], "Proposal applied without approval"
    approval = {"requestId": str(uuid.uuid4()), "revisaoProposta": proposal["revisao"], "versaoDecisoes": proposal["versaoDecisoes"],
                "versaoPecaEsperada": piece["versao"], "grupos": [{"id": g["id"], "previewSha256": g["previewSha256"]} for g in proposal["grupos"]]}
    route = "/assistente/v2/propostas/" + proposal["id"] + "/aplicacoes"
    receipt = client.request("POST", route, approval)
    assert client.request("POST", route, approval)["versaoResultante"] == receipt["versaoResultante"]
    piece = client.request("GET", path)
    assert piece["versao"] == receipt["versaoResultante"] and TEXT in json.dumps(piece["conteudoJson"], ensure_ascii=False)
    export(client, run, piece, run.state / "exports-original")
    expected = {"accessToken": client.token, "userId": identity["user"]["id"], "organizationId": identity["organization"]["id"],
                "caseId": case["id"], "pieceId": piece["id"], "conversationId": conversation,
                "version": piece["versao"], "document": piece["conteudoJson"], "attachments": attachments, "messages": len(messages)}
    write(run.state / "expected.json", expected)
    write(run.state / "seed-result.json", {"status": "passed", "documents": documents, "approvalIdempotent": True, "realAiCalls": 0})
    print("Fluxo real: criação, leitura PDF/DOCX/TXT, chat simulado, aprovação e exportações: OK", flush=True)

def verify(run):
    expected = json.loads((run.state / "expected.json").read_text())
    client = Client(run); client.use_expected(expected)
    run.check_network()
    piece = client.request("GET", "/pecas/" + expected["pieceId"])
    assert piece["versao"] == expected["version"] and piece["conteudoJson"] == expected["document"]
    messages = client.request("GET", f"/assistente/v2/conversas/{expected['conversationId']}/mensagens?limite=50")
    assert len(messages) == expected["messages"]
    for item in expected["attachments"]:
        data = client.request("GET", f"/assistente/v2/conversas/{expected['conversationId']}/anexos/{item['id']}/arquivo", raw=True)
        assert hashlib.sha256(data).hexdigest() == item["sha256"]
    export(client, run, piece, run.state / ("exports-restored" if run.project.endswith("-restored") else "exports-verified"))
    assert run.sql("SELECT count(*) FROM ai_usage_reservations WHERE status IN ('Reserved','Indeterminate');") == "0"
    write(run.state / ("verify-restored.json" if run.project.endswith("-restored") else "verify-original.json"),
          {"status": "passed", "pieceVersion": piece["versao"], "messages": len(messages), "attachmentsVerified": len(expected["attachments"]), "pdfAndDocx": True})
    print("Texto, versão, conversa, anexos por SHA-256 e exportações: OK", flush=True)

def browser(run):
    print("Conferindo frontend compilado e cookies pelo navegador...", flush=True)
    script = run.state / "config/browser.mjs"
    run.logged("browser-" + ("restored" if run.project.endswith("-restored") else "original"),
               ["node", script, str(run.state), *(["--read-only"] if run.project.endswith("-restored") else [])])

def operational(run):
    """Existing financial and deletion behavior, with synthetic data only."""
    expected = json.loads((run.state / "expected.json").read_text())
    client = Client(run); client.use_expected(expected)
    run.logged("limit-zero", [*run.base, "run", "--rm", "--no-deps",
        "-e", "AI_LIMIT_ORGANIZATION_ID=" + expected["organizationId"], "-e", "AI_LIMIT_USD=0",
        "-e", "AI_OPERATION_UPDATED_BY_ID=" + expected["userId"], "-e", "AI_OPERATION_REASON=Synthetic rehearsal", "api", "set-ai-monthly-limit"])
    count = run.sql("SELECT count(*) FROM ai_usage_ledger;")
    result = client.request("POST", f"/assistente/v2/conversas/{expected['conversationId']}/interacoes",
        {"clientRequestId": str(uuid.uuid4()), "texto": "Teste fictício de bloqueio de orçamento.", "versaoPecaEsperada": expected["version"]}, expected=429)
    assert result["code"] == "AI_MONTHLY_LIMIT_REACHED" and run.sql("SELECT count(*) FROM ai_usage_ledger;") == count
    run.logged("limit-restore", [*run.base, "run", "--rm", "--no-deps",
        "-e", "AI_LIMIT_ORGANIZATION_ID=" + expected["organizationId"], "-e", "AI_LIMIT_USD=5",
        "-e", "AI_OPERATION_UPDATED_BY_ID=" + expected["userId"], "-e", "AI_OPERATION_REASON=Synthetic rehearsal completed", "api", "set-ai-monthly-limit"])
    piece = client.request("POST", f"/casos/{expected['caseId']}/pecas", {"titulo": "Descarte fictício"}, expected=201)
    conversation = client.request("POST", f"/assistente/v2/pecas/{piece['id']}/conversa")["id"]
    attachment = upload(client, conversation, "descarte.txt", b"Documento ficticio destinado a exclusao.")
    personal = client.request("POST", "/auth/pessoal/login", {"email": "pilot@example.test", "senha": run.cfg["PILOT_PASSWORD"]})
    client.token = personal["accessToken"]
    request_id = str(uuid.uuid4())
    route = f"/assistente/v2/pessoal/conversas/{conversation}"
    protocol = client.request("DELETE", route, {"requestId": request_id, "confirmarExclusao": True}, expected=202)
    assert protocol["requestId"] == request_id
    terminal = poll(lambda: client.request("GET", "/assistente/v2/pessoal/exclusoes/" + request_id),
                    lambda value: value["estado"] == "RemovidaDoAmbienteAtivo")
    client.request("GET", route, expected=404)
    client.request("GET", route + f"/anexos/{attachment['id']}/arquivo", expected=404)
    assert run.sql("SELECT count(*) FROM ai_documents;") == "3"
    client.token = expected["accessToken"]
    assert client.request("GET", "/pecas/" + expected["pieceId"])["conteudoJson"] == expected["document"]
    write(run.state / "operational-result.json", {"status": "passed", "budgetBlockedBeforeProvider": True,
          "deletion": terminal["estado"], "otherPiecePreserved": True, "realAiCalls": 0})
    print("Bloqueio por orçamento e exclusão persistida de conversa/anexo: OK", flush=True)

def failed_release(run):
    import subprocess
    run.compose("stop", "-t", "45", "api")
    try:
        result = subprocess.run([str(x) for x in [*run.base, "run", "--rm", "--no-deps", "-e", "Auth__SecureRefreshCookie=false", "api"]],
                                env=run.env, capture_output=True, timeout=45)
        assert result.returncode != 0, "Unsafe candidate unexpectedly started"
        (run.state / "failed-candidate.private.log").write_bytes(result.stdout + result.stderr)
    finally:
        run.compose("up", "-d", "api")
        run.wait()
    verify(run)
    write(run.state / "recovery-result.json", {"status": "passed", "unsafeStartupRefused": True,
          "validImageRecovered": True, "destructiveMigrationDowngrade": False})
    print("Candidato com configuração inválida recusado; versão válida recuperada: OK", flush=True)
