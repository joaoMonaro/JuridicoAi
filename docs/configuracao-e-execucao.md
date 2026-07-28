# Configuração e execução

## Pré-requisitos

- SDK do .NET 10;
- MongoDB 7 ou compatível;
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

## MongoDB com Docker Compose

O Compose cria apenas o MongoDB e mantém os dados no volume `mongo_data`.

```bash
export MONGO_ROOT_USERNAME="juridico"
export MONGO_ROOT_PASSWORD="troque-esta-senha"
docker compose up -d
```

Exemplo de conexão da API:

```bash
export Mongo__ConnectionString="mongodb://juridico:troque-esta-senha@localhost:27017/?authSource=admin"
```

## Configurações da API

As chaves podem ser definidas em `appsettings*.json`, variáveis de ambiente ou
user-secrets. Em variáveis de ambiente, use `__` no lugar de `:`.

| Chave | Padrão | Finalidade |
|---|---:|---|
| `Mongo:ConnectionString` | vazio | conexão obrigatória para operações com MongoDB |
| `Mongo:DatabaseName` | `JuridicoAi` | nome do banco |
| `Mongo:RawCollectionName` | `jurisprudencias_raw` | dados coletados |
| `Mongo:JurisprudenciasCollectionName` | `jurisprudencias` | dados processados |
| `Mongo:ServerSelectionTimeoutSeconds` | `10` | timeout de seleção/conexão do servidor |
| `Scraper:TimeoutSeconds` | `120` | timeout total da coleta iniciada pela API |
| `Scraper:MaxResults` | `30` | máximo padrão quando a requisição omite o valor |
| `OpenAI:ApiKey` | sem padrão | chave obrigatória para `/peticoes/gerar` |
| `OpenAI:Model` | `gpt-4.1` | modelo enviado à Responses API |

Exemplo:

```bash
export Mongo__ConnectionString="mongodb://localhost:27017/"
export OpenAI__ApiKey="sua-chave"
export OpenAI__Model="gpt-4.1"
export Scraper__TimeoutSeconds="120"
export Scraper__MaxResults="30"
```

Não versione chaves, senhas ou connection strings reais. Para desenvolvimento,
o projeto da API possui `UserSecretsId` e aceita:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set "OpenAI:ApiKey" "sua-chave"
dotnet user-secrets --project src/JuridicoAI.Api set "Mongo:ConnectionString" "sua-conexao"
```

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
