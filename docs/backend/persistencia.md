# Persistência no MongoDB

## Banco e collections

Os nomes padrão são:

```text
Banco: JuridicoAi
Collections:
  - jurisprudencias_raw
  - jurisprudencias
```

Eles podem ser alterados pelas configurações `Mongo:DatabaseName`,
`Mongo:RawCollectionName` e `Mongo:JurisprudenciasCollectionName`.

## Dados brutos

Documento conceitual de `jurisprudencias_raw`:

```json
{
  "_id": "ObjectId",
  "fonte": "TJSP",
  "numeroProcesso": "...",
  "relator": "...",
  "orgaoJulgador": "...",
  "dataJulgamento": "11/06/2026",
  "dataPublicacao": "11/06/2026",
  "ementaBruta": "...",
  "link": "https://...",
  "htmlBruto": "...",
  "termoBuscado": "negativação indevida",
  "dataColeta": "data UTC",
  "status": "coletado | processado | erro_processamento",
  "erroProcessamento": null
}
```

As datas provenientes do TJSP ainda são strings nessa collection. O
`htmlBruto` é o trecho HTML correspondente ao resultado individual e pode
aumentar significativamente o volume armazenado.

Índice:

```text
uq_fonte_processo_link (fonte, numeroProcesso, link) UNIQUE
```

## Dados processados

Documento conceitual de `jurisprudencias`:

```json
{
  "_id": "ObjectId",
  "rawId": "ObjectId",
  "fonte": "TJSP",
  "numeroProcesso": "...",
  "relator": "...",
  "orgaoJulgador": "...",
  "dataJulgamento": "data UTC ou null",
  "dataPublicacao": "data UTC ou null",
  "ementa": "...",
  "link": "https://...",
  "classificacao": {
    "area": "direito_consumidor",
    "tema": "negativacao_indevida",
    "subtema": "debito_inexistente",
    "tipoAcao": "declaratoria_inexistencia_debito_indenizacao",
    "resultado": "provavelmente_favoravel_consumidor",
    "danoMoral": true,
    "valorDanoMoral": null,
    "exclusaoNegativacao": true,
    "palavrasChave": [],
    "confianca": 0.9,
    "statusClassificacao": "classificado",
    "versaoClassificador": "regras-v1"
  },
  "dataProcessamento": "data UTC"
}
```

Índices:

- `rawId` único;
- `fonte`;
- `numeroProcesso`;
- `link`;
- `classificacao.tema`;
- `classificacao.subtema`;
- `classificacao.resultado`;
- `classificacao.danoMoral`;
- `dataJulgamento` decrescente.

## Criação de índices

Os índices são garantidos no começo de cada ingestão ou reprocessamento. Uma
consulta isolada não os cria.

Se já existirem duplicatas incompatíveis com um índice único, a criação do
índice pode falhar antes do processamento. Esse caso precisa ser saneado
manualmente no banco.

## Consistência entre collections

Cada documento processado referencia exatamente um raw por `rawId`. O
repositório processado faz upsert por esse campo e preserva o `_id` existente
durante reprocessamentos.

Não há transação envolvendo as duas collections. Uma interrupção entre salvar
a versão processada e atualizar o status do raw pode deixar dados processados
com raw ainda marcado como `coletado`. Um novo reprocessamento é seguro por
causa do upsert.
