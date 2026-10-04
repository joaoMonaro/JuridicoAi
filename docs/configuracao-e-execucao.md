# Configuração e execução

## Pré-requisitos

- SDK do .NET 10;
- MongoDB 7 ou compatível;
- PostgreSQL 17 ou compatível;
- Chromium/Chrome, necessário para a sessão automatizada do TJSP;
- acesso de rede ao TJSP.

## Restaurar e compilar

Na raiz do repositório:

```bash
dotnet restore
dotnet build
```

Se não houver Chromium/Chrome em um dos caminhos reconhecidos pelo código,
instale o navegador do Playwright após a compilação:

```bash
pwsh src/JuridicoAI.Scrapper/bin/Debug/net10.0/playwright.ps1 install chromium
```

Em Linux, o ambiente também precisa das bibliotecas de sistema exigidas pelo
Chromium.

## Bancos com Docker Compose

O Compose cria MongoDB e PostgreSQL e mantém os dados nos volumes `mongo_data` e `postgres_data`.

Crie o arquivo local `.env` a partir do modelo versionado:

```bash
cp .env.example .env
```

Edite o `.env` e defina:

```dotenv
COMPOSE_PROJECT_NAME=juridicoai
MONGO_ROOT_USERNAME=juridico
MONGO_ROOT_PASSWORD=defina-uma-senha-local
POSTGRES_DB=juridicoai
POSTGRES_USER=juridico
POSTGRES_PASSWORD=defina-uma-senha-local
```

Se `COMPOSE_PROJECT_NAME` estiver exportado globalmente no terminal, ele tem
prioridade sobre o valor do `.env`. Remova-o antes de iniciar o projeto:

```bash
unset COMPOSE_PROJECT_NAME
```

Em seguida, inicie os bancos:

```bash
docker compose up -d
```

O nome `COMPOSE_PROJECT_NAME=juridicoai` isola os containers, redes e volumes
deste repositório dos demais projetos Compose da máquina.

Evite `docker compose down -v` quando quiser preservar os dados. A opção `-v`
remove também os volumes do MongoDB e do PostgreSQL.

## Configurações da API

As chaves podem ser definidas em `appsettings*.json`, variáveis de ambiente ou
user-secrets. Em variáveis de ambiente, use `__` no lugar de `:`.

| Chave | Padrão | Finalidade |
|---|---:|---|
| `Mongo:ConnectionString` | vazio | conexão obrigatória para operações com MongoDB |
| `Postgres:ConnectionString` | vazio | conexão obrigatória para operações de casos |
| `Postgres:RunMigrations` | `true` | aplica migrations pendentes do EF Core ao iniciar |
| `Auth:JwtSecret` | vazio | segredo JWT obrigatório, com ao menos 32 caracteres |
| `Auth:Issuer` | `JuridicoAI` | emissor dos access tokens |
| `Auth:Audience` | `JuridicoAI.Web` | audiência dos access tokens |
| `Auth:AccessTokenTtlMinutes` | `15` | duração do access token |
| `Auth:RefreshTokenTtlDays` | `30` | duração máxima do refresh token |
| `DocumentStorage:RootPath` | `data/documentos` | diretório do storage local |
| `DocumentStorage:MaxFileSizeBytes` | `20971520` | limite por upload |
| `AssistenteIa:Enabled` | `false` | trava de configuração superior do assistente |
| `AssistenteIa:Provider` | `OpenAI` | provider selecionado |
| `AssistenteIa:Model` | vazio | modelo enviado ao provider |
| `AssistenteIa:ApiKey` | vazio | chave sensível do provider |
| `AssistenteIa:MaxInputTokens` | `60000` | teto de entrada após montagem do contexto |
| `AssistenteIa:MaxRequestBodyBytes` | `524288` | limite do corpo HTTP do assistente |
| `AssistenteIa:RequestsPerUserPerMinute` | `10` | limite por usuário |
| `AssistenteIa:ConcurrentRequestsPerUser` | `2` | concorrência por usuário |
| `AssistenteIa:RequestsPerOrganizationPerMinute` | `30` | limite agregado por Organization |
| `AssistenteIa:ConcurrentRequestsPerOrganization` | `6` | concorrência por Organization |
| `Mongo:DatabaseName` | `JuridicoAi` | nome do banco |
| `Mongo:RawCollectionName` | `jurisprudencias_raw` | dados coletados |
| `Mongo:JurisprudenciasCollectionName` | `jurisprudencias` | dados processados |
| `Mongo:ServerSelectionTimeoutSeconds` | `10` | timeout de seleção/conexão do servidor |
| `Scraper:TimeoutSeconds` | `120` | timeout total da coleta iniciada pela API |
| `Scraper:MaxResults` | `30` | máximo padrão quando a requisição omite o valor |

Não versione chaves, senhas ou connection strings reais. Para desenvolvimento,
o projeto da API possui `UserSecretsId` e carrega automaticamente o Secret
Manager do .NET quando o ambiente é `Development`.

O guia completo, incluindo chave da OpenAI, flags por Organization e kill
switch, está em [User Secrets no desenvolvimento](user-secrets.md).

Configure a connection string com o mesmo usuário e senha definidos no `.env`:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "Mongo:ConnectionString" \
  "mongodb://juridico:defina-uma-senha-local@localhost:27017/?authSource=admin"

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Postgres:ConnectionString" \
  "Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=defina-uma-senha-local"

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Auth:JwtSecret" \
  "um-segredo-aleatorio-com-pelo-menos-32-caracteres"
```

Também é possível sobrescrever configurações operacionais:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "Scraper:TimeoutSeconds" "180"

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Scraper:MaxResults" "50"
```

Para listar o que está configurado:

```bash
dotnet user-secrets --project src/JuridicoAI.Api list
```

Esse comando mostra os valores completos. Não compartilhe sua saída. Os
user-secrets ficam fora do repositório, mas não são criptografados e devem ser
usados somente em desenvolvimento.

Variáveis exportadas no terminal têm prioridade sobre user-secrets. A conversão
de nomes usa dois sublinhados:

```text
Mongo:ConnectionString -> Mongo__ConnectionString
Postgres:ConnectionString -> Postgres__ConnectionString
```

Se a senha do MongoDB possuir caracteres reservados de URL, como `@`, `:`, `/`,
`?` ou `#`, codifique esses caracteres na connection string.

## Executar a API

```bash
dotnet run --project src/JuridicoAI.Api/JuridicoAI.Api.csproj
```

O perfil HTTP de desenvolvimento usa `http://localhost:5299`. O endpoint raiz
`GET /` pode ser usado como verificação básica e retorna `Hello World!`.

O CORS aceita origens cujo host seja `localhost` ou `127.0.0.1`, em qualquer
porta. Isso não é uma política pronta para produção.

## Executar o scraper isoladamente

```bash
dotnet run --project src/JuridicoAI.Scrapper/JuridicoAI.Scrapper.csproj -- \
  --termo "negativação indevida dano moral" \
  --max 20 \
  --output resultado.json
```

Consulte [Uso pela linha de comando](scraper/cli.md) para todas as opções.

## Configuração mínima por funcionalidade

| Funcionalidade | Requisitos |
|---|---|
| Scraper pela CLI | .NET, Chromium e acesso ao TJSP |
| Ingestão pela API | MongoDB, Chromium e acesso ao TJSP |
| Consulta de jurisprudências | MongoDB |
| Reprocessamento | MongoDB |
| Fundação do caso, autenticação e Peças | PostgreSQL, segredo JWT e diretório de storage gravável |
| Assistente de IA | requisitos de Peças, chave/modelo do provider, configuração habilitada, kill switch e flags da Organization |

## Sequência recomendada

1. Execute `cp .env.example .env` e altere a senha.
2. Remova um eventual projeto global com `unset COMPOSE_PROJECT_NAME`.
3. Inicie os bancos com `docker compose up -d`.
4. Configure `Mongo:ConnectionString`, `Postgres:ConnectionString` e
   `Auth:JwtSecret` nos user-secrets.
5. Execute `dotnet restore` e `dotnet build`.
6. Instale o Chromium do Playwright, se necessário.
7. Inicie a API; as migrations do EF Core serão aplicadas antes de a aplicação
   começar a atender requisições.
8. Valide `GET http://localhost:5299/`.

Para usar a IA, configure separadamente o provider e libere apenas a
Organization desejada conforme [User Secrets](user-secrets.md). A configuração
local não substitui o aviso de transparência apresentado no primeiro uso.
