"""Isolated release rehearsal. Uses only Docker, Git, Python and OpenSSL on the host."""
import argparse
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import time

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]

def command(args, **kwargs):
    return subprocess.run([str(x) for x in args], check=True, capture_output=True, **kwargs)

def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")
    path.chmod(0o600)

def snapshot(repo, destination, prefix=""):
    destination.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256()
    files = command(["git", "-C", repo, "ls-files", "-z"], text=True).stdout.split("\0")
    for name in sorted(filter(None, files)):
        if prefix and not name.startswith(prefix):
            continue
        source = repo / name
        if source.is_symlink():
            raise RuntimeError("Source snapshots must not contain symlinks: " + name)
        if source.name == ".env" or source.name.startswith(".env.") and source.name != ".env.example":
            raise RuntimeError("Refusing environment file in build context")
        data = source.read_bytes()
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        digest.update(name.encode() + b"\0" + data)
    (destination / ".dockerignore").write_text("**/bin\n**/obj\n**/node_modules\n**/.git\n**/.env\n**/data\n")
    return {"commit": command(["git", "-C", repo, "rev-parse", "HEAD"], text=True).stdout.strip(),
            "sourceSha256": digest.hexdigest(), "dirty": bool(command(["git", "-C", repo, "status", "--porcelain"], text=True).stdout)}

def prepare(state, frontend):
    state.mkdir(parents=True, exist_ok=True)
    state.chmod(0o700)
    if (state / "state.json").exists():
        return
    if list(state.iterdir()):
        raise RuntimeError("Use an empty state directory")
    frontend = frontend.resolve()
    backend = snapshot(ROOT, state / "context-api", "src/JuridicoAI.Api/")
    front = snapshot(frontend, state / "context-web")
    (state / "context-web/.rehearsal").mkdir()
    shutil.copyfile(HERE / "nginx.conf", state / "context-web/.rehearsal/nginx.conf")
    config = state / "config"
    shutil.copytree(HERE, config, ignore=shutil.ignore_patterns("__pycache__"))
    certs = config / "certs"
    certs.mkdir(mode=0o700)
    command(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "7",
             "-subj", "/CN=juridicoai-local-rehearsal", "-addext",
             "subjectAltName=DNS:app.juridicoai.test,DNS:api.juridicoai.test,IP:127.0.0.1",
             "-keyout", certs / "local.key", "-out", certs / "local.crt"])
    (certs / "local.key").chmod(0o600)
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    networks = command(["docker", "network", "ls", "-q"], text=True).stdout.split()
    existing = json.loads(command(["docker", "network", "inspect", *networks], text=True).stdout) if networks else []
    used = [ipaddress.ip_network(item["Subnet"]) for n in existing for item in (n.get("IPAM", {}).get("Config") or []) if item.get("Subnet")]
    subnet = next(ipaddress.ip_network(f"172.29.{n}.0/24") for n in range(20, 250)
                  if all(not ipaddress.ip_network(f"172.29.{n}.0/24").overlaps(x) for x in used if x.version == 4))
    identifier = secrets.token_hex(4)
    cfg = {"project": "juridicoai-rehearsal-" + identifier, "frontend": str(frontend), "backend": backend, "web": front,
           "DATABASE_PASSWORD": secrets.token_hex(24), "JWT_SECRET": secrets.token_hex(40),
           "PILOT_PASSWORD": "Synthetic-" + secrets.token_hex(16), "HTTPS_PORT": str(port),
           "PROXY_IP": str(subnet.network_address + 10), "SUBNET": str(subnet),
           "API_IMAGE": "juridicoai-rehearsal-api:" + backend["sourceSha256"][:12],
           "WEB_IMAGE": "juridicoai-rehearsal-web:" + front["sourceSha256"][:12] + "-" + str(port),
           "FAKE_IMAGE": "juridicoai-rehearsal-fake:" + identifier}
    save(state / "state.json", cfg)
    (state / "rehearsal.env").write_text("\n".join(k + "=" + json.dumps(v) for k, v in cfg.items() if isinstance(v, str)) + "\n")
    (state / "rehearsal.env").chmod(0o600)
    print("Ambiente preparado: " + str(state), flush=True)

class Rehearsal:
    def __init__(self, state, restored=False):
        self.state = state
        self.cfg = json.loads((state / "state.json").read_text())
        self.project = self.cfg["project"] + ("-restored" if restored else "")
        self.env = os.environ.copy()
        self.env.update({k: v for k, v in self.cfg.items() if isinstance(v, str)})
        self.base = ["docker", "compose", "--project-name", self.project, "--env-file", state / "rehearsal.env", "-f", state / "config/compose.yaml"]

    def compose(self, *args, **kwargs):
        return command([*self.base, *args], env=self.env, **kwargs)

    def logged(self, label, args):
        print(label + "...", flush=True)
        with open(self.state / (label + ".log"), "ab") as stream:
            process = subprocess.Popen([str(x) for x in args], stdout=stream, stderr=subprocess.STDOUT, env=self.env)
            while True:
                try:
                    result = process.wait(timeout=20)
                    break
                except subprocess.TimeoutExpired:
                    print(label + ": em execução", flush=True)
        if result:
            raise RuntimeError(label + " falhou; consulte o log privado em " + str(self.state))
        print(label + ": OK", flush=True)

    def build(self):
        for kind, filename, context, extra in [
                ("API_IMAGE", "Api.Dockerfile", self.state / "context-api", []),
                ("WEB_IMAGE", "Web.Dockerfile", self.state / "context-web", ["--build-arg", "API_URL=https://api.juridicoai.test:" + self.cfg["HTTPS_PORT"]]),
                ("FAKE_IMAGE", "Fake.Dockerfile", self.state / "config", [])]:
            self.logged("build-" + kind.lower(), ["docker", "build", "-t", self.cfg[kind], "-f", self.state / "config" / filename, *extra, context])
        self.logged("pull-postgres", ["docker", "pull", "postgres:17"])
        images = json.loads(command(["docker", "image", "inspect", self.cfg["API_IMAGE"], self.cfg["WEB_IMAGE"], self.cfg["FAKE_IMAGE"], "postgres:17"], text=True).stdout)
        save(self.state / "images.json", [{"id": x["Id"], "tags": x["RepoTags"], "digests": x.get("RepoDigests", [])} for x in images])

    def database(self):
        self.compose("up", "-d", "--wait", "postgres")

    def sql(self, query):
        return self.compose("exec", "-T", "postgres", "psql", "-U", "rehearsal", "-d", "rehearsal", "-At", "-v", "ON_ERROR_STOP=1", input=query, text=True).stdout.strip()

    def start(self):
        self.database()
        self.logged("migrations", [*self.base, "run", "--rm", "--no-deps", "api", "migrate-database"])
        if self.sql("SELECT count(*) FROM users WHERE email='pilot@example.test';") == "0":
            self.logged("provision", [*self.base, "run", "--rm", "--no-deps", "-e", "PROVISION_ORGANIZATION_NAME=Ensaio fictício",
                "-e", "PROVISION_OWNER_NAME=Advogado fictício", "-e", "PROVISION_OWNER_EMAIL=pilot@example.test",
                "-e", "PROVISION_OWNER_PASSWORD=" + self.cfg["PILOT_PASSWORD"], "api", "provision-organization"])
        self.install_certificates()
        self.compose("up", "-d", "fake-ai", "api", "web")
        self.wait()
        print("Serviços iniciados em modo Production e rede isolada.", flush=True)

    def install_certificates(self):
        command(["docker", "volume", "create", "--label", "com.docker.compose.project=" + self.project,
                 "--label", "com.docker.compose.volume=certificates", self.project + "_certificates"])
        container = command(["docker", "create", "--network", "none", "--entrypoint", "/bin/true",
                             "-v", self.project + "_certificates:/certs", self.cfg["WEB_IMAGE"]], text=True).stdout.strip()
        try:
            for name in ["local.crt", "local.key"]:
                command(["docker", "cp", self.state / "config/certs" / name, container + ":/certs/" + name])
        finally:
            command(["docker", "rm", container])

    def wait(self):
        from verify import Client
        for _ in range(90):
            try:
                client = Client(self)
                client.request("GET", "/health/live")
                self.sql('SELECT count(*) FROM "__EFMigrationsHistory";')
                if (self.state / "expected.json").exists():
                    expected = json.loads((self.state / "expected.json").read_text())
                    client.use_expected(expected)
                    client.request("GET", "/pecas/" + expected["pieceId"])
                return
            except Exception:
                time.sleep(1)
        raise RuntimeError("HTTPS/API indisponíveis; consulte compose logs")

    def check_network(self):
        info = json.loads(command(["docker", "network", "inspect", self.project + "_isolated"], text=True).stdout)[0]
        assert info["Internal"] is True
        for service in ["api", "postgres", "fake-ai"]:
            cid = self.compose("ps", "-q", service, text=True).stdout.strip()
            container = json.loads(command(["docker", "inspect", cid], text=True).stdout)[0]
            assert not container["HostConfig"]["PortBindings"], service
            assert list(container["NetworkSettings"]["Networks"]) == [self.project + "_isolated"]
        assert self.compose("exec", "-T", "api", "id", "-u", text=True).stdout.strip() != "0"

    def recreate(self):
        self.compose("restart", "postgres", "api", "web", "fake-ai")
        self.wait()
        self.compose("up", "-d", "--force-recreate", "api", "web")
        self.wait()
        print("Reinicialização e substituição dos containers: OK", flush=True)

    def backup(self):
        target = self.state / "backup"
        target.mkdir(mode=0o700, exist_ok=True)
        self.compose("stop", "-t", "45", "web", "api")
        try:
            dump = self.compose("exec", "-T", "postgres", "pg_dump", "-U", "rehearsal", "-d", "rehearsal", "-Fc", "--no-owner", "--no-acl").stdout
            (target / "database.dump").write_bytes(dump)
            archive = command(["docker", "run", "--rm", "--network", "none", "--entrypoint", "tar", "-v", self.project + "_documents:/source:ro",
                               self.cfg["API_IMAGE"], "-C", "/source", "-cf", "-", "."]).stdout
            (target / "documents.tar").write_bytes(archive)
            shutil.copyfile(self.state / "state.json", target / "recovery-config.private.json")
            for path in target.iterdir():
                path.chmod(0o600)
            save(target / "manifest.json", {name: hashlib.sha256((target / name).read_bytes()).hexdigest() for name in ["database.dump", "documents.tar"]})
        finally:
            self.compose("up", "-d", "api", "web")
            self.wait()
        print("Backup consistente de banco e documentos: OK", flush=True)

    def restore(self):
        backup = self.state / "backup"
        manifest = json.loads((backup / "manifest.json").read_text())
        for name, expected in manifest.items():
            assert hashlib.sha256((backup / name).read_bytes()).hexdigest() == expected
        self.database()
        assert self.sql("SELECT count(*) FROM information_schema.tables WHERE table_schema='public';") == "0", "Restore requires a fresh database"
        self.compose("exec", "-T", "postgres", "pg_restore", "-U", "rehearsal", "-d", "rehearsal", "--no-owner", "--no-acl", "--exit-on-error", input=(backup / "database.dump").read_bytes())
        self.compose("run", "--rm", "--no-deps", "--entrypoint", "/bin/true", "api")
        command(["docker", "run", "--rm", "--network", "none", "--user", "0:0", "--entrypoint", "tar", "-i", "-v", self.project + "_documents:/target",
                 self.cfg["API_IMAGE"], "-C", "/target", "-xf", "-"], input=(backup / "documents.tar").read_bytes())
        self.install_certificates()
        self.compose("up", "-d", "fake-ai", "api", "web")
        self.wait()
        print("Restauração em banco e volume novos: OK", flush=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["prepare", "build", "start", "seed", "verify", "recreate", "backup", "restore", "browser", "operational", "failed-release", "stop", "all"])
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--frontend", type=Path, default=ROOT.parent / "JuridicoAi-frontend")
    parser.add_argument("--restored", action="store_true")
    args = parser.parse_args()
    state = args.state.resolve()
    if args.action in ["prepare", "all"]:
        prepare(state, args.frontend)
    if args.action == "prepare":
        return
    run = Rehearsal(state, args.restored)
    if args.action in ["all", "seed", "verify", "browser", "operational", "failed-release"]:
        from verify import seed, verify, browser, operational, failed_release
    if args.action == "all":
        run.build(); run.start(); seed(run); verify(run); browser(run)
        run.recreate(); verify(run); run.backup(); run.compose("down")
        restored = Rehearsal(state, True)
        restored.restore(); verify(restored); browser(restored); operational(restored); failed_release(restored)
        save(state / "result.json", {"status": "passed", "realAiCalls": 0, "originalProject": run.project, "restoredProject": restored.project,
             "backend": run.cfg["backend"], "frontend": run.cfg["web"], "images": json.loads((state / "images.json").read_text()),
             "checks": ["clean_build", "controlled_migrations", "production_https", "private_network", "pdf_docx_txt_reading",
                 "synthetic_chat", "explicit_approval", "idempotent_approval", "word_pdf_export", "browser_cookie_refresh",
                 "restart", "container_replacement", "consistent_backup", "restore_to_new_volumes", "budget_block", "active_deletion", "failed_startup_recovery"]})
    elif args.action == "seed": seed(run)
    elif args.action == "verify": verify(run)
    elif args.action == "browser": browser(run)
    elif args.action == "operational": operational(run)
    elif args.action == "failed-release": failed_release(run)
    elif args.action == "stop": run.compose("down")
    else: getattr(run, args.action)()

if __name__ == "__main__":
    main()
