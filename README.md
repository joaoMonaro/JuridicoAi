# JuridicoAI

## Pipeline de jurisprudencias

O fluxo de ingestao da API e:

```text
TJSP -> dado bruto -> normalizacao -> classificacao por regras -> MongoDB
```

Configure os segredos por variaveis de ambiente (ou user-secrets em desenvolvimento):

```bash
export Mongo__ConnectionString="mongodb://usuario:senha@localhost:27017/"
export OpenAI__ApiKey="..."
```

Para iniciar o Mongo pelo Compose, defina `MONGO_ROOT_USERNAME` e
`MONGO_ROOT_PASSWORD`. A API usa as collections `jurisprudencias_raw` e
`jurisprudencias`.

Os exemplos de ingestao, consulta e reprocessamento estao em
`src/JuridicoAI.Api/JuridicoAI.http`.
