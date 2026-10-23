# JuridicoAI

Backend do JuridicoAI: uma API ASP.NET Core para autenticação e multitenancy,
organização de Casos, edição persistente de Peças, assistência jurídica por IA
e ingestão de jurisprudências.

## Componentes principais

```text
Frontend -> API .NET -> PostgreSQL
                   |-> OpenAI Responses API
                   |-> MongoDB
                   `-> Scraper TJSP
```

- PostgreSQL armazena identidade, organizações, Casos, Peças e governança da IA;
- MongoDB armazena jurisprudências brutas e processadas;
- o assistente gera respostas em SSE e nunca altera uma Peça sem aplicação
  explícita no frontend;
- o scraper do TJSP permanece no código, mas está fora do fluxo do MVP da IA.

## Início rápido

Configure `.env` para os bancos, suba os serviços e defina os segredos locais:

```bash
cp .env.example .env
docker compose up -d

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Postgres:ConnectionString" \
  "Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=..."

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Auth:JwtSecret" \
  "um-segredo-aleatorio-com-pelo-menos-32-caracteres"

dotnet run --project src/JuridicoAI.Api
```

MongoDB é necessário para os fluxos de jurisprudência. A chave e o modelo da
OpenAI são necessários somente para executar o assistente. Não versione
segredos; consulte [User Secrets](docs/user-secrets.md).

As migrations do Entity Framework Core são aplicadas na inicialização quando
`Postgres:RunMigrations=true`.

## Documentação

- [Índice da documentação atual](docs/README.md)
- [Configuração e execução](docs/configuracao-e-execucao.md)
- [API HTTP](docs/backend/api.md)
- [Assistente de IA](docs/backend/assistente-ia.md)
- [Autenticação e multitenancy](docs/backend/autenticacao-e-multitenancy.md)
- [Peças e templates](docs/backend/pecas.md)
- [Implementações em andamento](implementacoes/README.md)
- [Geração de minutas por pedido livre — ainda a implementar](implementacoes/features/geracao-minutas-ia/README.md)

A documentação de `docs/` descreve o comportamento atual. Especificações,
decisões e pendências ficam em `implementacoes/`, separadas entre a feature
ativa em `features/`, entregas registradas em `alreadyDone/` e trabalho em espera
em `standBy/`. As suítes de avaliação da IA ficam em `avaliacoes/`.
