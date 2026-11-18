# API e execução durável — assistente-v2

> Desenho original de 2026-11-11, preservado como referência arquitetural.
> **Não usar as tabelas abaixo como inventário das rotas atuais.** Contratos,
> códigos HTTP, estados e regras de retomada foram concretizados com diferenças.
> A referência de integração é [contratos implementados](07-contratos-implementados.md).

## Situação atual e diferenças do desenho

A conversa livre usa `POST /conversas/{id}/interacoes` com
`EnviarInteracaoLivreRequest` e SSE 200. Com geração durável, o pedido/job é
persistido e o SSE emite `solicitacao.enfileirada`; o acompanhamento usa
`GET /conversas/{id}/geracoes/{geracaoId}` e o histórico persistido.

Não foram implementadas as rotas genéricas `/execucoes`, o journal SSE
`/conversas/{id}/eventos` ou a resposta `MensagemAceita` HTTP 202 de envio prevista
abaixo. Abertura retorna 200, mensagens são um array com cursor exclusivo e
rejeição de grupos não recebe requestId. Exclusão está nas rotas pessoais.
Essas diferenças não tornam ausentes o worker ou a durabilidade já entregues.

O worker geral tem lease default de **180 s** e prazo de **1.800 s**. Chamada
paga de resultado indeterminado **não é repetida automaticamente**; exige retomada
explícita e orçamento disponível. Os números de lease/tentativas do desenho
original abaixo não substituem essa política implementada.

Ver [geração durável](../evidencias/e2-e3-geracao-duravel-2026-11-12.md),
[histórico pessoal/exclusão](../evidencias/e1-historico-pessoal-exclusao-2026-11-12.md)
e [estado atual](../estado-atual.md).

## Desenho original — referência histórica

As regras e envelopes a seguir registram a proposta inicial; itens divergentes
não são funcionalidades anunciadas nem pendências implícitas adicionais ao TODO.

## Regras comuns

JSON camelCase, enums PascalCase, UUID string, UTC ISO-8601. OrganizationId e
UserId não são parâmetros de autoridade. JSON desconhecido é rejeitado nos
comandos de mutação. Erros em `application/problem+json`, mantendo o formato atual
e acrescentando code, retryable, correlationId e campos de conflito sem conteúdo.

Headers: `Authorization: Bearer`; mutações usam ClientRequestId/requestId no
corpo para idempotência. Mesmo ID + mesmo hash canônico retorna a operação
original; mesmo ID + payload diferente retorna 409 `AI_IDEMPOTENCY_MISMATCH`.
Downloads e SSE usam `Cache-Control: no-store`; CORS mantém origem explícita.

## Rotas organizacionais

Todas usam o JWT atual. IDs inacessíveis retornam 404 para não revelar existência.

| Método/rota relativa a `/assistente/v2` | Entrada | Resultado |
|---|---|---|
| `GET /capacidades?pecaId={id}` | Peça acessível | 200 operações, leitura documental e limites efetivos; não anuncia implementação ausente |
| `POST /pecas/{pecaId}/conversa` | `{}` | 200 existente ou 201 criada; conversa própria e capabilities |
| `GET /conversas/{id}` | — | 200 metadados, estado, última sequência, execução ativa e acesso atual |
| `GET /conversas/{id}/mensagens?antes={seq}&limite=50` | cursor exclusivo | 200 items em ordem, proximoAntes; máximo 100 |
| `POST /conversas/{id}/mensagens` | `EnviarMensagem` | 202 mensagemId, execucaoId, sequencia e eventosUrl |
| `POST /conversas/{id}/anexos` | multipart arquivo + clientUploadId | 202 anexo com estado Recebido; processamento durável |
| `GET /conversas/{id}/anexos` | paginação | 200 estados e cobertura |
| `GET /conversas/{id}/anexos/{anexoId}` | — | 200 estado, páginas disponíveis e problemas de qualidade |
| `GET /conversas/{id}/anexos/{anexoId}/arquivo` | — | 200 stream privado; MIME validado, nome seguro e Content-Disposition attachment |
| `GET /conversas/{id}/eventos?apos={seq}` | último ID | SSE, resumível |
| `GET /execucoes/{id}` | — | 200 estado, etapa, progresso e resultados já publicados |
| `POST /execucoes/{id}/cancelamento` | requestId | 202 ou 200 cancelamento já concluído |
| `GET /propostas/{id}` | — | 200 proposta completa para revisão |
| `POST /propostas/{id}/aplicacoes` | `AplicarGrupos` | 200 recibo, documento canônico e proposta remanescente |
| `GET /conversas/{id}/aplicacoes/{requestId}` | — | 200 recibo ou 404 ainda não confirmado |
| `POST /propostas/{id}/rejeicoes` | revisão, decisionVersion, grupos, requestId | 200 nova decisionVersion e proposta |
| `DELETE /conversas/{id}` | requestId, confirmarExclusao=true | 202 protocolo de exclusão; UI pede autorização para essa ação destrutiva |
| `GET /exclusoes/{requestId}` | — | 200 fase e conclusão da solicitação do titular |

Upload não é uma mensagem por si só: o composer associa attachmentIds no envio.
Só anexos da própria conversa são aceitos. Arquivos enviados e ainda não enviados
em mensagem permanecem no composer/histórico de anexos; não são apagados por TTL.
Upload interrompido sem arquivo completo remove somente o temporário e não cria
uma mensagem fantasma. Cliente HTTP deve deixar o browser montar Content-Type
multipart; `apiFetch` atual força application/json quando há body e será ajustado
para reconhecer FormData.

## Rotas pessoais

`POST /auth/pessoal/login`, `POST /auth/pessoal/refresh`,
`POST /auth/pessoal/logout`: esquema descrito em [autorização](01-dados-e-autorizacao.md).

Sob `/assistente/v2/pessoal`: `GET /conversas?antes=...`,
`GET /conversas/{id}`, `GET /conversas/{id}/mensagens`,
`GET /conversas/{id}/anexos/{anexoId}/arquivo`, `GET /propostas/{id}`,
`DELETE /conversas/{id}`, `GET /exclusoes/{requestId}`.

As rotas pessoais leem somente o material histórico já persistido do titular;
não expõem estado de trabalhos atuais, ferramentas ou dados atuais do Caso.
Não há POST de mensagem/aplicação/upload nessas rotas. Nova UI `HistoricoAssistentePage`
fora do workspace da Peça torna o chat legível sem abrir um editor inacessível.

## Envio de mensagem

`EnviarMensagem` contém texto, anexoIds, propostaReferenciada opcional e
versaoPecaEsperada. A seleção opcional fornece um auxílio de foco, nunca autoridade
para acesso ou aplicação. Não recebe modo nem instruções de sistema do cliente.

V1 exige sincronizar a Peça antes de enviar pedidos enquanto o editor está sujo,
para não gerar sobre texto antigo. Falha preserva o composer e permite consultar
histórico; não inventa um snapshot salvo. A versão enviada precisa coincidir com
a carregada pelo servidor. Pedido desatualizado retorna 409 sem gravar execução.

Transação: verificar conversa/acesso → reservar sequência → inserir mensagem →
inserir execução/trabalho → evento mensagem.aceita → commit. Exatamente uma
execução não terminal por conversa. Segundo envio retorna `AI_CONVERSATION_BUSY`
sem descartar o texto do composer. Flags de operação são revalidadas ao produzir
a proposta; a execução não dá ao modelo permissão para criar novos direitos.

## Ciclo do worker

Estados não terminais: NaFila, PreparandoContexto, AguardandoDocumentos, Gerando,
Validando. Terminais: Concluida, Incompleta, Falhou, Cancelada.

Regras:

- `NaFila` → `PreparandoContexto`; fontes pendentes → `AguardandoDocumentos`.
- Ao concluir extrações necessárias → nova aquisição → `PreparandoContexto`.
- Contexto elegível → `Gerando` → `Validando` → `Concluida`.
- Resultado recuperável sem completar → `Incompleta`, sem proposta aplicável.
- Exceção técnica após tentativas → `Falhou`; cancelamento → `Cancelada`.
- Conflito da Peça durante geração não elimina resposta: resultado pode ser
  publicado com proposta Desatualizada, aprovação desabilitada e opção de ajustar.

Aquisição em PostgreSQL com `FOR UPDATE SKIP LOCKED`, atualização de lease e
commit antes de processamento. SKIP LOCKED é apropriado a filas, não a uma leitura
consistente de conteúdo. Base: [PostgreSQL 17 SELECT](https://www.postgresql.org/docs/17/sql-select.html).

Lease inicial 60s, heartbeat 15s, polling ocioso 1s com jitter, no máximo 3 tentativas
por etapa transitória, delays 2s e 8s respeitando Retry-After. UPDATE de publicação
exige leaseToken e deletionEpoch atuais. Reinício retoma checkpoint; falhas
indeterminadas foram inicialmente consideradas para retry externo. Essa hipótese
foi substituída na implementação pela interrupção sem repetição paga automática,
conforme a política atual descrita acima.

Erro de schema, fonte não autorizada ou qualidade insuficiente não é resolvido
por repetir indefinidamente. Uma correção guiada do resultado consome orçamento
e também passa por validação; após isso falha explícita. Cancelar não apaga o chat.

## Eventos e retomada

SSE por fetch autenticado, reaproveitando apiFetch sem token em URL. Novo parser
aceita event/id/data e deduplica por sequência; `AbortController` fecha a assinatura,
não cancela o trabalho. Um comando distinto solicita cancelamento.

Envelope: schemaVersion, conversaId, sequencia, tipo, execucaoId nullable, data UTC,
payload. `id:` é a sequência decimal. Reconectar envia `apos`; o servidor primeiro
reproduz eventos > cursor e depois aguarda novos. Ler o banco após cada notificação
evita perder eventos no intervalo entre replay e assinatura. O banco é a fonte;
notificações são otimização. Heartbeat de transporte a cada 15s sem persistir texto.

Tipos: mensagem.aceita, anexo.progresso, execucao.progresso, resposta.delta,
resposta.concluida, proposta.disponivel, proposta.atualizada, execucao.falhou,
execucao.cancelada. Deltas agrupados em até 250ms ou 512 caracteres; a mensagem
final é canônica, não soma cega de deltas após reconexão.

Eventos são conteúdo de produto e seguem retenção do chat; não expiram por TTL.
Página inicial usa último cursor e histórico paginado para evitar replay infinito.
Após revogação, encerrar SSE e usar consulta pessoal somente histórica. Auth é
revalidada durante conexões longas, antes de emitir conteúdo; renovar a assinatura
a cada 5min ou antes de expirar o token. Ausência de JWT válido encerra o stream.

## Aprovação e rejeição

`AplicarGrupos`: requestId, revisaoProposta, versaoDecisoes, versaoPecaEsperada,
grupos `[id, previewSha256]`. Não há texto livre, flags de confirmação genéricas
ou operação indicada pelo modelo no comando de aplicação.

Um hash/revisão divergente exige atualizar a visualização. Versão da Peça diferente
da base aplicável retorna `AI_PROPOSAL_STALE`. Grupos dependentes são uma única
unidade de decisão. Recibo e alteração são atômicos, conforme
[especificação do editor](03-editor-e-aplicacao.md).

Rejeitar incrementa versaoDecisoes, não toca na Peça. Ajustar pelo chat cria nova
revisão da proposta e substitui pendências anteriores; não recria grupos aplicados.

## Erros e limites HTTP

| HTTP | Code | Tratamento |
|---|---|---|
| 400 | AI_INVALID_REQUEST | Corrigir contrato, nenhum retry automático |
| 401 | AUTH_REQUIRED | Renovar sessão ou solicitar login |
| 403 | AI_FEATURE_NOT_ENABLED / AI_TRANSPARENCY_REQUIRED | Atualizar capacidades ou aviso |
| 404 | AI_RESOURCE_NOT_FOUND | Recurso ausente/inacessível, sem revelar titular |
| 409 | AI_IDEMPOTENCY_MISMATCH | Não reutilizar ID com outro payload |
| 409 | AI_CONVERSATION_BUSY | Manter composer até execução terminal |
| 409 | AI_PIECE_VERSION_CONFLICT / AI_PROPOSAL_STALE | Recarregar sem sobrescrever edição local |
| 409 | AI_PROPOSAL_REVISION_CONFLICT | Refazer comparação com revisão atual |
| 410 | AI_CONVERSATION_DELETING | Não reabrir ou recriar trabalho |
| 413 | AI_UPLOAD_TOO_LARGE / AI_CONTEXT_TOO_LARGE | Mostrar limite e reduzir entrada |
| 415 | AI_FILE_TYPE_UNSUPPORTED | Orientar formatos aceitos |
| 422 | AI_DOCUMENT_UNUSABLE / AI_SCHEMA_UNSUPPORTED | Reenviar material ou ajustar proposta |
| 429 | AI_RATE_LIMITED / AI_BUDGET_EXCEEDED | Retry-After quando aplicável; sem loop |
| 503 | AI_UNAVAILABLE | Preservar entrada e permitir tentativa posterior |

Falhas após aceitar a mensagem ficam na execução e em evento, não num HTTP
fictício do SSE. `ApiExceptionMiddleware` deve reconhecer também o prefixo pessoal
e a autenticação específica para não registrar conteúdo de erros como exceção livre.
