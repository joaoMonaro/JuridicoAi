# Configuração e execução

## Pré-requisitos

- SDK do .NET 10;
- MongoDB 7 ou compatível;
- PostgreSQL 17 ou compatível;
- Chromium/Chrome, necessário para a sessão automatizada do TJSP;
- chave da API da OpenAI, necessária somente para gerar petições;
- acesso de rede ao TJSP e à OpenAI.

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
```

Se `COMPOSE_PROJECT_NAME` estiver exportado globalmente no terminal, ele tem
prioridade sobre o valor do `.env`. Remova-o antes de iniciar o projeto:

```bash
unset COMPOSE_PROJECT_NAME
```

Em seguida, inicie o MongoDB:

```bash
docker compose up -d
```

O nome `COMPOSE_PROJECT_NAME=juridicoai` isola os containers, redes e volumes
deste repositório dos demais projetos Compose da máquina.

Evite `docker compose down -v` quando quiser preservar os dados. A opção `-v`
remove também o volume do MongoDB.

## Configurações da API

As chaves podem ser definidas em `appsettings*.json`, variáveis de ambiente ou
user-secrets. Em variáveis de ambiente, use `__` no lugar de `:`.

| Chave | Padrão | Finalidade |
|---|---:|---|
| `Mongo:ConnectionString` | vazio | conexão obrigatória para operações com MongoDB |
| `Postgres:ConnectionString` | vazio | conexão obrigatória para operações de casos |
| `Postgres:RunMigrations` | `true` | aplica migrations pendentes ao iniciar |
| `DocumentStorage:RootPath` | `data/documentos` | diretório do storage local |
| `DocumentStorage:MaxFileSizeBytes` | `20971520` | limite por upload |
| `Mongo:DatabaseName` | `JuridicoAi` | nome do banco |
| `Mongo:RawCollectionName` | `jurisprudencias_raw` | dados coletados |
| `Mongo:JurisprudenciasCollectionName` | `jurisprudencias` | dados processados |
| `Mongo:ServerSelectionTimeoutSeconds` | `10` | timeout de seleção/conexão do servidor |
| `Scraper:TimeoutSeconds` | `120` | timeout total da coleta iniciada pela API |
| `Scraper:MaxResults` | `30` | máximo padrão quando a requisição omite o valor |
| `OpenAI:ApiKey` | sem padrão | chave obrigatória para `/peticoes/gerar` |
| `OpenAI:Model` | `gpt-4.1` | modelo enviado à Responses API |

Não versione chaves, senhas ou connection strings reais. Para desenvolvimento,
o projeto da API possui `UserSecretsId` e carrega automaticamente o Secret
Manager do .NET quando o ambiente é `Development`.

Configure a connection string com o mesmo usuário e senha definidos no `.env`:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "Mongo:ConnectionString" \
  "mongodb://juridico:defina-uma-senha-local@localhost:27017/?authSource=admin"
```

Para habilitar a geração de petições, configure a chave da OpenAI:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "OpenAI:ApiKey" \
  "sua-chave-da-openai"
```

O modelo é opcional:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "OpenAI:Model" \
  "gpt-4.1"
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
OpenAI:ApiKey           -> OpenAI__ApiKey
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
| Geração de petições | MongoDB, `OpenAI:ApiKey` e acesso à OpenAI |
| Fundação do caso | PostgreSQL e diretório de storage gravável |

## Sequência recomendada

1. Execute `cp .env.example .env` e altere a senha.
2. Remova um eventual projeto global com `unset COMPOSE_PROJECT_NAME`.
3. Inicie os bancos com `docker compose up -d`.
4. Configure `Mongo:ConnectionString` nos user-secrets.
5. Configure `OpenAI:ApiKey` se for gerar petições.
6. Execute `dotnet restore` e `dotnet build`.
7. Instale o Chromium do Playwright, se necessário.
8. Inicie a API e valide `GET http://localhost:5299/`.
