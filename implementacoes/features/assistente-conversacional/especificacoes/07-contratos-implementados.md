# Contratos e configuração implementados

Conferência do código em **2026-11-12**, baseline backend `4547485` com a correção
local do setup do avaliador, e frontend `50aa439`. Esta página distingue o
contrato efetivo do desenho original das especificações 01–04 e dos exemplos.
O [estado atual](../estado-atual.md) reúne resultados e limitações; o
[TODO](../TODO.md) acompanha implementação, validações e melhorias restantes.

## Fonte dos contratos

DTOs, controllers e configurações EF são a referência para integração. Os
exemplos de `EnviarMensagem`, `MensagemAceita` e `Evento` do catálogo inicial
não representam integralmente os endpoints atuais. Em particular, execução
durável não exige uma resposta HTTP 202 de envio nem um journal SSE no banco.

- [Controllers](../../../../src/JuridicoAI.Api/src/Controllers/Assistente/).
- [DTOs de conversa, propostas, recibos e geração](../../../../src/JuridicoAI.Api/src/Application/DTOs/AssistenteWorkspaceDtos.cs).
- [DTOs documentais](../../../../src/JuridicoAI.Api/src/Application/DTOs/AssistenteDocumentDtos.cs).
- [Configuração de IA](../../../../src/JuridicoAI.Api/src/Infrastructure/Settings/AssistenteIaSettings.cs).
- [Configuração documental](../../../../src/JuridicoAI.Api/src/Infrastructure/Settings/AssistenteDocumentSettings.cs).

## Conversa organizacional e aplicação

Estas rotas usam a identidade organizacional e verificam titularidade, acesso à
Peça/Caso e capacidades. Prefixo `/assistente/v2`, exceto onde indicado:

| Método/rota | Contrato implementado |
|---|---|
| `GET /assistente/capacidades` | Capacidades e limites; não existe uma segunda rota `/assistente/v2/capacidades` |
| `GET /assistente/transparencia` | Aviso versionado e estado de aceite do titular |
| `POST /assistente/transparencia/confirmacoes` | Confirma a versão informada |
| `POST /pecas/{pecaId}/conversa` | Retorna 200 com a conversa própria, criada ou recuperada |
| `GET /conversas/{id}/mensagens?antes={seq}&limite=50` | Array de mensagens com cursor exclusivo; não envelope `items/proximoAntes` |
| `POST /conversas/{id}/interacoes` | `EnviarInteracaoLivreRequest`, sem modo técnico; resposta SSE 200 |
| `POST /conversas/{id}/mensagens` | Adaptador `EnviarMensagemChatRequest`, com modo/contexto legados; SSE 200 |
| `POST /conversas/{id}/mensagens/{mensagemId}/rejeicao` | Rejeita resposta, 204 |
| `GET /conversas/{id}/propostas` | Propostas persistidas da própria conversa |
| `POST /conversas/{id}/propostas` | Ponte de registro de sugestão legada, com `RegistrarPropostaRequest` |
| `GET /propostas/{id}` | Proposta completa e documento-base para revisão |
| `POST /propostas/{id}/aplicacoes` | `AplicarGruposRequest`; retorna recibo e documento canônico |
| `GET /conversas/{id}/aplicacoes/{requestId}` | Reconciliação do recibo por ID |
| `POST /propostas/{id}/rejeicoes` | Revisão, versão de decisões e IDs de grupos; o DTO não contém `requestId` |

O envio livre contém `clientRequestId`, `texto`, `versaoPecaEsperada`, seleção,
proposta referenciada e contexto/anexos opcionais. A interface sincroniza a Peça
antes do envio. O modelo não recebe permissão para aplicar propostas.

Uma aplicação recebe `requestId`, `revisaoProposta`, `versaoDecisoes`,
`versaoPecaEsperada` e pares de ID/hash de preview dos grupos. O commit da Peça,
decisões e recibo é transacional e idempotente. Rejeitar não grava a Peça.
O aviso não confirmado retorna HTTP **428**, `AI_TRANSPARENCY_ACK_REQUIRED`.

## Geração e transporte

Com `DurableGenerationEnabled`, pedido, resposta e job são persistidos antes do
trabalho de contexto/modelo. O envio SSE emite `solicitacao.iniciada` e
`solicitacao.enfileirada` e encerra o transporte. O frontend acompanha o job e
recupera o resultado pelo histórico, inclusive após reabrir a tela.

| Método/rota sob `/assistente/v2/conversas/{conversaId}` | Resultado |
|---|---|
| `GET /geracoes/{id}` | Estado, seções concluídas/total, código de erro e análise associada |
| `POST /geracoes/{id}/cancelamento` | Cancelamento explícito, 204 |
| `POST /geracoes/{id}/retomada` | Solicitação explícita de retomada, 202 |
| `GET /analises/{id}` | Estado, lotes, cobertura e falhas da análise documental |
| `POST /analises/{id}/cancelamento` | Cancelamento, 204; respeita coordenação com o job pai |
| `POST /analises/{id}/retomada` | Retomada, 202; respeita coordenação com o job pai |

Estados do job de geração incluem `NaFila`, `PreparandoContexto`,
`AguardandoDocumentos`, `AguardandoAnalise`, `Executando`, `Redigindo`,
`Verificando` e `Publicando`. Terminais: `Concluida`, `Interrompida`, `Incompleta`,
`Desatualizada` e `Cancelada`. São distintos do estado da mensagem.

O worker usa aquisição PostgreSQL com `SKIP LOCKED`, lease e validação de fence.
Defaults: lease de **180 s**, prazo de **1.800 s**, polling ocioso de **1 s**.
As etapas físicas usam estados como `Chamando`, `Recebida` e `Confirmada`;
resultado de chamada incerto impede repetição paga automática. Retomada requer
comando explícito, fontes/acesso válidos e saldo nos limites da execução.

Não há implementação do journal `GET /conversas/{id}/eventos?apos=...`, da rota
genérica `/execucoes/{id}` ou do envelope durável `Evento` propostos inicialmente.
Isso é uma diferença do desenho, não uma pendência necessária para o polling já
entregue. O endpoint v1 `/pecas/{pecaId}/assistente/solicitacoes` conserva o fluxo
ligado à requisição; o chat v2 sem a flag durável também conserva o transporte
anterior. Não se deve confundir SSE 200 com ausência de persistência do job.

## Documentos

Rotas sob `/assistente/v2/conversas/{conversaId}`:

| Método/rota | Resultado |
|---|---|
| `POST /anexos` | Multipart `arquivo` + `clientUploadId`; 202 com o documento recebido |
| `GET /anexos` | Array de estados/qualidade dos anexos |
| `GET /anexos/{id}` | Estado, progresso e problemas de qualidade |
| `GET /anexos/{id}/arquivo` | Download privado autenticado |
| `POST /anexos/{id}/reprocessar` | Reprocessamento autorizado, 202 |
| `GET /fontes/busca?consulta=...&limite=12` | Busca filtrada nos trechos elegíveis |

TXT/DOCX/PDF têm extração nativa. Poppler é usado para PDF e OpenXml para DOCX.
Tesseract existe no pipeline, mas **OCR permanece desativado e em espera**.
Originais privados não são promovidos automaticamente a arquivos do Caso.

## Histórico pessoal e exclusão

Autenticação separada: `POST /auth/pessoal/login`, `/refresh` e `/logout`.
As consultas usam o esquema `PersonalHistory`, conta ativa e sessão vigente no
banco; não exigem Membership nem habilitação das flags de geração.

Sob `/assistente/v2/pessoal` estão implementados:

- `GET /conversas?antes=...&limite=30`, `/conversas/{id}` e
  `/conversas/{id}/mensagens?antes=...&limite=50`.
- `GET /conversas/{id}/anexos`, `/conversas/{id}/anexos/{anexoId}/arquivo`
  e `/propostas/{id}`.
- `DELETE /conversas/{id}` com `requestId` e `confirmarExclusao:true`: 202 com protocolo.
- `GET /exclusoes/{requestId}` e `/exclusoes`.

A exclusão é oferecida nas rotas pessoais. Não existe o DELETE organizacional
`/assistente/v2/conversas/{id}` proposto originalmente. A área pessoal não oferece
mensagens novas, upload, geração, aplicação ou consulta às fontes atuais do Caso.

O refresh usa cookie Secure/HttpOnly/Strict com caminho `/auth/pessoal` e checagem
de Origin. Logout revoga a sessão; refresh rotativo não amplia direitos do token
organizacional. O worker de exclusão processa autorizações já aceitas mesmo com
IA desligada. Backup/registro externo/reaplicação continuam pendentes, conforme
[procedimento operacional](06-operacao-exclusao-restauracao.md).

## Persistência efetiva

Os nomes e separações abaixo diferem da projeção inicial da especificação 01:

| Tabelas implementadas | Uso |
|---|---|
| `ai_conversations`, `ai_messages` | Conversas privadas, ordenação, mensagens/pedidos/respostas persistidos |
| `ai_proposals`, `ai_application_receipts` | Propostas com grupos/documento-base em JSONB e recibos de commit |
| `ai_documents`, `ai_document_chunks` | Origens privadas/do Caso, estado de extração, trechos e busca filtrada |
| `ai_memories` | Memória versionada com proveniência |
| `ai_document_analyses`, `ai_document_analysis_batches` | Jobs de análise, lotes e checkpoints |
| `ai_generations`, `ai_generation_steps` | Jobs gerais e checkpoints/chamadas de geração |
| `ai_usage_ledger` | Consumo técnico por chamada, sem conteúdo jurídico |
| `personal_history_sessions`, `ai_deletion_requests` | Sessões pessoais e protocolos/trabalho de exclusão |

Não foram criadas as tabelas genéricas `ai_events`, `ai_jobs`, `ai_executions`,
`ai_snapshots`, `ai_change_groups`, `ai_budget_reservations` ou `ai_provider_calls`
do desenho inicial. Suas responsabilidades foram distribuídas pelos modelos
acima ou permanecem no escopo financeiro/operacional ainda aberto.

Conversa tem unicidade `(organization_id, user_id, peca_id)` sem filtro parcial;
uma conversa em exclusão continua ocupando esse par até o expurgo físico. O
filtro global oculta conversas cujo estado não seja `Ativa`. Mensagem em geração
tem índice parcial para impedir outro pedido ativo na mesma conversa. Os
detalhes e nomes de colunas estão nas configurações EF e migrations, não no
inventário proposto em 2026-11-11.

## Configuração e limites vigentes

Todas as novas flags começam falsas no código/configuração versionada. Model e
ApiKey continuam vazios por padrão: o GPT-6.1 Terra foi escolhido para a avaliação,
sem configurar automaticamente o modelo do ambiente do produto.

| Opção | Default/efeito |
|---|---|
| `AssistenteIa:ReviewV2Enabled` | Revisão persistida |
| `ConversationV2Enabled` | Histórico no contexto; depende da revisão |
| `AutomaticConversationEnabled` | Intenção automática; depende de conversa/revisão |
| `DurableGenerationEnabled` | Chat v2 em worker; requer o fluxo conversacional habilitado |
| `HistoryMemoryEnabled` | Memória versionada/recuperação do histórico |
| `AssistenteDocumentos:Enabled` / `RetrievalEnabled` / `AnalysisEnabled` | Documentos, recuperação e análise extensa |
| `AssistenteDocumentos:OcrEnabled` | Falso; não ativar sem concluir a calibração pausada |
| `MaxInstructionCharacters` / `MaxRequestBodyBytes` | 4.000 caracteres / 512 KiB |
| `MaxInputTokens` / `MaxOutputTokens` | 60.000 / 6.000 por chamada |
| `MaxTotalContextCharacters` | 80.000 caracteres de contexto |
| `GenerationMaxCalls` / `GenerationMaxTotalTokens` | 12 chamadas / 180.000 tokens por execução |
| `GenerationMaxSections` / `GenerationSectionCharacters` | 8 seções / 20.000 caracteres JSON por seção |
| `GenerationMaxDocumentCharacters` | 120.000 caracteres JSON para o corpo montado; não garante caber no contexto seguinte |
| `MaxFileBytes` / `MaxPages` / `MaxPixels` | 20 MiB / 100 páginas / 25 milhões de pixels |
| `MaxAttachments` / `MaxConversationBytes` | 50 anexos / 500 MiB |

Opções de IA sem prefixo na tabela pertencem a `AssistenteIa`; limites de
arquivo/páginas/pixels/anexos pertencem a `AssistenteDocumentos`. Preços para o
ledger são configuráveis. Isso não implementa bloqueio financeiro agregado de
produção. Defaults iniciais diferentes, como mensagem de 20 mil caracteres,
HTTP de 1 MiB, lease de 60 s ou retry pago automático, não descrevem o código atual.

P03–P08 do TODO continuam abertos. A revisão interna por seções e os checks da
rodada reduzida não certificam qualidade jurídica, acessibilidade, exportação ou
preparação operacional completa do piloto.
