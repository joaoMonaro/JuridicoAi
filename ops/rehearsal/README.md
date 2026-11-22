# Ensaio local de publicação do MVP

Executa frontend compilado, API Release em `Production`, PostgreSQL 17 e um
simulador determinístico de IA em containers. Banco, documentos e certificados
têm volumes exclusivos, com nomes derivados de um projeto aleatório.
O Compose de desenvolvimento da raiz e seus bancos não são utilizados.

As imagens são construídas a partir de cópias dos arquivos rastreados pelo Git
no estado atual de cada repositório, incluindo modificações locais. Não recebem
`.env`, user-secrets, `.git`, `bin`, `obj`, `node_modules` ou dados locais.
O resultado registra commits, hashes do conteúdo e IDs das imagens. A versão
do frontend fica compilada para os nomes/porta deste ensaio; para a nuvem,
reconstruir com o endereço real da API e suas configurações.

## Executar

Pré-requisitos do operador: Docker/Compose, Git, Python 3, OpenSSL e espaço para
as imagens. O cliente de teste de navegador usa Node e o Playwright/Chromium
instalados no repositório frontend. Esses programas são ferramentas de teste;
os serviços não usam os runtimes .NET, Node, fontes ou LibreOffice do host.
Downloads de imagens, NuGet, npm e pacotes do sistema ocorrem durante o build.

Na raiz do backend:

```bash
python3 ops/rehearsal/run.py all \
  --state /tmp/juridicoai-release-ensaio \
  --frontend ../JuridicoAi-frontend
```

Use uma pasta vazia e exclusiva. Ela recebe credenciais sintéticas, chave TLS
temporária, fontes copiadas, logs e backups; mantenha-a privada e fora do Git.
As credenciais não são apropriadas para produção. Certificado de teste não é
instalado como autoridade no computador. O navegador de teste aceita somente
esse ambiente local com certificado de ensaio; a API mantém HTTPS obrigatório,
cookies seguros, CORS explícito e confiança somente no IP do proxy local.

Somente o servidor web publica uma porta em `127.0.0.1`. API, PostgreSQL e
simulador ficam exclusivamente numa rede Docker `internal`, sem saída para a
internet. O servidor web também participa de uma rede de entrada para permitir
o encaminhamento local no Docker Desktop. A IA recebe chave/modelo fictícios,
tarifas identificadas como sintéticas e usa o controle financeiro existente;
nenhum caminho do simulador chama serviços externos. OCR fica desativado.

## O que é comprovado

- Compilação limpa das imagens e instalação das dependências documentais.
- Migrations por comando explícito `migrate-database`, antes do servidor;
  runtime com `Postgres:RunMigrations=false`, cadastro público fechado e
  provisionamento por CLI. Este recorte não implementa separação de usuários
  SQL de migrations/runtime; a credencial do banco é exclusiva do ensaio.
- API real, extração de PDF com texto/DOCX/TXT, chat com resposta sintética,
  proposta sem aplicação automática, aprovação idempotente e Word/PDF reais.
- Navegador usando o frontend compilado, HTTPS, API real, renovação por cookie,
  edição manual, recarga, histórico e download dos dois formatos.
- Reinício e recriação de API/web, sem reutilizar o filesystem dos containers.
- Backup de banco e documentos com API/web interrompidos, restauração em
  projeto/volumes novos e conferência do texto, histórico e SHA-256 dos anexos.
- Bloqueio financeiro antes do provedor, exclusão autorizada no ambiente ativo
  e recuperação após recusar um candidato com configuração insegura.

O reinício e a substituição usam o mesmo pacote validado. A recuperação ensaia
falha de configuração; não afirma compatibilidade com uma migration futura nem
faz downgrade destrutivo de schema. Backups deste ensaio ficam no mesmo host;
produção exige destino independente e política de retenção. P05, OCR, CI/CD
completo e novas funcionalidades permanecem fora deste trabalho.

## Operar por etapas

As ações disponíveis são `prepare`, `build`, `start`, `seed`, `verify`, `browser`,
`recreate`, `backup`, `restore`, `operational`, `failed-release` e `stop`.
`seed` exige um estado sem `expected.json`; não repete uma geração já confirmada.
Para os serviços restaurados, acrescente `--restored`. `restore` exige banco
vazio e volumes exclusivos do projeto de restauração.

```bash
python3 ops/rehearsal/run.py verify --state /tmp/juridicoai-release-ensaio --restored
python3 ops/rehearsal/run.py stop --state /tmp/juridicoai-release-ensaio --restored
```

`stop` remove os containers e redes, preservando os volumes. O procedimento
normal não usa `down -v`, não remove volumes de desenvolvimento e não altera
o arquivo de hosts. O navegador automatizado resolve os nomes `.test` somente
no próprio processo. Para inspeção manual, os nomes `app.juridicoai.test` e
`api.juridicoai.test` precisam resolver para `127.0.0.1`; a porta está no estado
privado do ensaio.

O aceite jurídico, medições com modelo real e validação da hospedagem escolhida
continuam pendentes após este ensaio. Não é uma liberação para produção.
