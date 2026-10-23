# Conferência documental do estado implementado — 2026-10-18

> **Tipo:** inspeção estática de código, testes e registros anteriores
>
> **Resultado:** documentação reconciliada; PA-18 e gates de produção abertos
>
> **Backend inspecionado:** `JuridicoAi`, commit `597bf29` de 2026-10-06
>
> **Frontend inspecionado:** `JuridicoAi-frontend`, commit `23a8e3c` de 2026-10-06

## Escopo e critério

A conferência comparou a documentação com o código e os testes existentes nos
dois repositórios. Itens marcados indicam implementação identificada, apoiada
pelos testes existentes e pelas execuções anteriores quando registradas. Não
houve nova execução de testes, build, avaliação paga do modelo, auditoria de
acessibilidade ou validação visual/manual nesta conferência.

Decisão aprovada, código implementado, teste existente e homologação são estados
distintos. A confirmação do código não encerra os gates operacionais ou
jurídicos. A evidência manual de 2026-10-06 avalia o painel anterior ao chat.

## Frontend: entregas confirmadas

Os caminhos abaixo são relativos ao repositório `JuridicoAi-frontend`.

| Entrega | Evidência no código/testes |
|---|---|
| Chat, composer único e mensagens com preparação, resposta, erro e cancelamento | `src/features/ai-assistant/components/assistant-panel.tsx`: `ConversationTurn`, `submit`, `ConversationTurnView` |
| Seleção automática, snapshot removível e ausência de conteúdo do editor por padrão | mesmo arquivo: `captureSelection`, `captureSelectionOnAssistantFocus`, `selectDocumentScope`, montagem de `AssistantRequest`; testes de chip automático, nova seleção, remoção e payload em `assistant-panel.test.tsx` |
| Contextos opcionais por chips e menu progressivo | mesmo arquivo: `ContextChip`, menu `Adicionar contexto`; testes de inclusão explícita e remoção de contextos |
| Respostas, achados e cartões de proposta dentro da conversa | `ConversationTurnView` e `SuggestionResult`; teste de aplicação explícita com edição da proposta |
| Comparação recolhível, cópia, rejeição e regeneração sem apagar proposta anterior | `SuggestionResult`, `reject`, `copySuggestion`, `submit(turn)`; teste de cópia/regeneração/rejeição com dois turnos |
| Bloqueio de aplicação duplicada | fase `applied` e permissão `canApply` no cartão; cenário existente em `e2e/assistant-integrity.spec.ts` |
| Larguras predefinidas e expansão | estados `wide`/`expanded` e teste de alternância em `assistant-panel.test.tsx`; não há resize livre por arraste |
| Preservação ao fechar e reabrir o painel | `conversations` em memória e teste `preserva a conversa ao fechar e reabrir o painel` |
| Aplicação somente em `SALVO` e com âncora válida | `src/features/ai-assistant/editor.ts`: `canApplySuggestion`/`applySuggestion`; matriz de quatro estados bloqueados em `editor.test.ts` e no teste do painel |
| Undo/redo e recuperação de autosave | `editor.test.ts`, `src/features/pecas/hooks/use-peca-autosave.ts`, cenários de retry e conflito em `e2e/assistant-resilience.spec.ts` |

### Limites observados

- `src/pages/CaseWorkspacePage.tsx` usa `LegalDocumentEditor key={peca.id}`.
  Trocar de Peça desmonta o editor e o painel. O mapa local de turnos por
  `pecaId` não preserva conversas ao navegar para outra Peça e retornar.
- Recarregar a página descarta as conversas. Não existem persistência ou
  endpoints de histórico, e capacidades anunciam `SuportaHistorico: false`.
- `submit` envia somente a instrução e os contextos selecionados do pedido;
  mensagens anteriores não são incluídas como histórico conversacional.
- O modo inicial é `Perguntar`. Ações rápidas alteram o modo; o contrato não
  implementa intenção automática para pedido livre.
- `Tentar novamente` chama `submit(turn)` e reaproveita `selectionSnapshot`.
  Não corresponde ao fluxo guiado de selecionar outro trecho do PA-07.
  Um novo envio pelo composer usa o snapshot atualizado da seleção.
- O campo de instrução e o de contexto adicional têm `maxLength`; seleção e
  informação adicional exibem dimensão. Feedback completo dos limites de todos
  os contextos antes do envio permanece em E2.
- Alertas retornados são exibidos em `Verifique antes de usar`. O painel ainda
  não contém aviso permanente de ausência de pesquisa jurídica externa.
- Funções de renderização da conversa existem no mesmo arquivo do painel;
  a separação estrutural prevista em E1 permanece pendente.

## Backend: entregas confirmadas

Os caminhos abaixo são relativos ao repositório `JuridicoAi`.

| Entrega | Evidência no código/testes |
|---|---|
| Contagem exata e limite de entrada | `AssistenteIaApplicationService`, `IAssistenteTokenCounter` e `OpenAiAssistenteIaProvider`; testes do contador e rejeição antes da geração |
| Limites cumulativos de usuário e Organization | `AssistenteUsageLimiter`: quatro limitadores em memória; testes em `AssistenteIaGovernanceTests` e `AssistenteIaHttpSecurityTests` |
| SSE, IDs monotônicos e falhas controladas pela API | `AssistenteController` e `AssistenteIaHttpSecurityTests`, inclusive `Http_StreamPublicaIdsMonotonicosPorSolicitacao` |
| Provider sem armazenamento de Response | `OpenAiAssistenteIaProvider` envia `store=false`; teste `Gerar_UsaResponsesSemRetencaoELeStreamEstruturado` confere o payload, sem comprovar retenção zero contratual |
| Isolamento e transparência versionada | testes HTTP de acesso cruzado e exigência do aviso; `AssistenteTransparencyService` e repositório de confirmações |
| Kill switch, flags e auditoria | `AssistenteFeatureAccessService`, `AiOperationalControlRepository`, migrations e testes de governança |
| Consumo e custo estimado sem conteúdo | `AssistenteUsageRecorder`, `AiUsageLedgerRepository` e testes de governança; custo fica `null` quando tarifas estão sem configuração |
| Prompt injection e proteção de logs | `AssistentePromptInjectionTests`, testes do serviço de aplicação e testes HTTP com marcadores sensíveis |

### Limites observados

- Rate limit e concorrência são locais à instância, sem coordenação distribuída.
- Há teto único configurável de saída (`MaxOutputTokens`, padrão 6.000).
  Perfis por operação, retry automático, orçamento com bloqueio e alertas não
  estão implementados.
- `IAssistenteIaProvider.GerarAsync` retorna um resultado completo. O adapter
  acumula o stream estruturado; a API emite o texto em um único `conteudo.delta`
  após validação. A transmissão externa é SSE, mas não há texto incremental
  durante a geração.
- A validação de saída confere estrutura e operações permitidas; não identifica
  semanticamente toda referência sem fonte. O alerta correspondente depende do
  prompt/modelo e das avaliações.
- Testes HTTP usam a API real com dependências de teste. Não equivalem a teste
  integrado com PostgreSQL, OpenAI e proxy/APM reais.
- Histórico, purga/restauração, retenção automatizada, pseudonimização adicional,
  métricas/exportador, dashboards, eventos de produto e rollout nominal seguem
  pendentes nos respectivos planos.

## Evidências anteriores preservadas

- [PA-12](../../../standBy/assistente-ia/decisoes/pa-12-testes-acessibilidade.md): registro de execução da
  fatia vertical com 57 testes Vitest, 110 de backend e 24 Playwright.
- [Plano do redesign](../../../standBy/assistente-ia/planejamento/redesign-conversacional.md): registro de
  2026-10-06 com 65 testes Vitest, 23 do painel, lint e build aprovados para a
  fatia ali descrita; o conjunto do painel faz parte da suíte Vitest.
- [PA-09](../decisoes/pa-09-avaliacao-qualidade-juridica.md): baseline jurídica
  técnica `1.0.3`, 30 casos; homologação jurídica formal permanece em PA-17.
- [Segurança da IA](../../../../avaliacoes/seguranca-ia/README.md): registro de
  aprovação técnica dos seis casos da baseline `1.0.0`.

Esses números pertencem aos registros anteriores. Não são contagens de testes
executados em 2026-10-18 nem comprovação de regressão completa do chat atual.

## Pendências mantidas abertas

A primeira entrega do PA-18 ainda exige regressão E5 e aprovação visual/manual.
E4 (intenção automática) e E6 (histórico persistente) podem seguir depois,
conforme o plano. Recuperação guiada da âncora, preservação entre trocas de Peça
e feedback completo de limites também permanecem identificados.

O [checklist manual de acessibilidade](../../../standBy/assistente-ia/evidencias/checklist-manual-acessibilidade.md), a
[homologação de infraestrutura](../../../standBy/assistente-ia/evidencias/seguranca-operacional-producao.md), o orçamento,
a retenção, a observabilidade, o rollout e a revisão jurídica/contratual não
foram encerrados por esta atualização documental.

## Revisão anterior ao commit — 2026-10-20

Os commits de referência dos dois repositórios permanecem os mesmos. A revisão
reconferiu os limites da conversa local, o snapshot reutilizado no retry, as
configurações e o transporte SSE. Também corrigiu a descrição do tratamento de
HTML: o backend não remove tags do texto; o frontend insere texto literal no
TipTap. Operações incompatíveis são rejeitadas, sem conversão silenciosa para
outra operação.

Os 148 links locais dos 16 documentos foram verificados, assim como os blocos
Markdown, o JSON de configuração da especificação backend e `git diff --check`.
Nenhuma suíte da aplicação foi executada nesta revisão exclusivamente documental.
