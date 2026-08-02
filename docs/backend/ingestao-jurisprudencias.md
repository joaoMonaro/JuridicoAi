# Ingestão e reprocessamento de jurisprudências

## Responsabilidade

`JurisprudenciaIngestionService` coordena coleta, persistência bruta,
normalização, classificação e persistência processada.

## Coleta iniciada pela API

A API adapta a entrada para uma `TjspSearchRequest` fixa:

| Campo | Valor |
|---|---|
| `Termo` | termo recebido, sem espaços nas extremidades |
| `QuantidadeMaxima` | valor solicitado ou `Scraper:MaxResults`, entre 1 e 200 |
| `TipoDecisao` | `A` (acórdãos) |
| `IncluirSinonimos` | `true` |
| `SalvarHtmlBruto` | `true` |

Portanto, a API não oferece atualmente seleção do tipo de decisão nem opção
para desativar sinônimos. Esses recursos só estão disponíveis na CLI do
scraper.

## Processamento de cada item

1. Os índices das duas collections são garantidos.
2. O scraper coleta até o limite configurado.
3. O raw é procurado pela combinação `fonte + numeroProcesso + link`.
4. Se já existir, o item é contado como duplicado e não é reprocessado.
5. Um item novo é normalizado e classificado.
6. A versão processada é gravada por upsert usando o `rawId`.
7. O raw recebe status `processado`.

Se uma etapa individual falhar, o erro é registrado no log. Quando já existe
um raw persistido, seu status muda para `erro_processamento` e
`erroProcessamento` recebe a mensagem da exceção.

## Significado do resumo

| Campo | Significado |
|---|---|
| `termoBuscado` | termo efetivamente enviado ao scraper |
| `coletados` | quantidade retornada pelo scraper |
| `novosRaw` | raws inseridos nesta execução |
| `duplicados` | raws que já existiam |
| `classificados` | itens novos processados até o fim |
| `falhas` | falhas individuais após a coleta |

`coletados` não representa o total informado pelo TJSP; representa apenas os
itens devolvidos dentro do limite.

## Idempotência

A ingestão é idempotente para a chave composta:

```text
fonte + numeroProcesso + link
```

Há uma consulta antes da inserção e também um índice único no MongoDB. A captura
de `DuplicateKey` protege contra corridas entre execuções concorrentes.

Uma alteração na ementa do TJSP não atualiza automaticamente um raw existente
se a chave composta continuar igual. O reprocessamento reaplica as regras ao
conteúdo já salvo, mas também não recolhe o conteúdo remoto.

## Reprocessamento

O reprocessamento é útil após mudanças no normalizador ou classificador:

```http
POST /api/jurisprudencias/reprocess?limite=500
```

Ele seleciona raws por data de coleta decrescente e, opcionalmente, por
`termoBuscado` exato. A versão processada mantém seu `_id` quando já existe,
pois o repositório preserva o identificador antes do `ReplaceOne` com upsert.

Ao ocorrer uma falha no reprocessamento, ela é contada e registrada no log. O
código atual não altera o raw para `erro_processamento` nesse fluxo.

## Cancelamento e timeout

O token de cancelamento HTTP percorre todo o pipeline. A coleta usa ainda um
token ligado com timeout de `Scraper:TimeoutSeconds`, no mínimo 1 segundo.

Quando somente esse timeout interno expira, o serviço lança `TimeoutException`
e o controller responde com HTTP 504. Cancelamento originado pelo cliente
permanece como `OperationCanceledException`.
