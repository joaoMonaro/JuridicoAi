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
export OpenAI__ApiKey="..."
```

Para iniciar os bancos pelo Compose, configure no `.env` as credenciais
`MONGO_ROOT_*` e `POSTGRES_*`. A API usa as collections
`jurisprudencias_raw` e `jurisprudencias` no MongoDB e o schema da fundação do
caso no PostgreSQL.

Os exemplos de ingestao, consulta e reprocessamento estao em
`src/JuridicoAI.Api/JuridicoAI.http`.

Os dados de casos, envolvidos, contextos e metadados de documentos são
persistidos no PostgreSQL por meio do Entity Framework Core. As migrations
pendentes são aplicadas na inicialização da API quando
`Postgres:RunMigrations` está habilitado.
