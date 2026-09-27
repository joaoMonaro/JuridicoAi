# JuridicoAI

## Pipeline de jurisprudencias

O fluxo de ingestao da API e:

```text
TJSP -> dado bruto -> normalizacao -> classificacao por regras -> MongoDB
```

Configure os segredos por variaveis de ambiente (ou user-secrets em desenvolvimento):

```bash
export Mongo__ConnectionString="mongodb://usuario:senha@localhost:27017/"
export Postgres__ConnectionString="Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=..."
export Auth__JwtSecret="um-segredo-aleatorio-com-pelo-menos-32-caracteres"
```

Para iniciar os bancos pelo Compose, configure no `.env` as credenciais
`MONGO_ROOT_*` e `POSTGRES_*`. A API usa as collections
`jurisprudencias_raw` e `jurisprudencias` no MongoDB e o schema da fundação do
caso no PostgreSQL.

Os exemplos de ingestao, consulta e reprocessamento estao em
`src/JuridicoAI.Api/JuridicoAI.http`.

Os dados de casos, envolvidos, contextos, metadados de documentos, Peças e
templates de Peça são persistidos no PostgreSQL por meio do Entity Framework
Core. As migrations
pendentes são aplicadas na inicialização da API quando
`Postgres:RunMigrations` está habilitado.

A documentação dos endpoints de Peças, templates e concorrência otimista está
em [`docs/backend/pecas.md`](docs/backend/pecas.md).

A primeira fatia do assistente de IA para redação jurídica, seu contrato SSE e
os planos de backend/frontend estão em
[`implementacoes/features/assistente-ia/README.md`](implementacoes/features/assistente-ia/README.md).
Ela permanece desabilitada por padrão até que provider, modelo e chave sejam
configurados com segurança.

Autenticação JWT, refresh token, provisionamento e isolamento por Organization
estão documentados em
[`docs/backend/autenticacao-e-multitenancy.md`](docs/backend/autenticacao-e-multitenancy.md).
