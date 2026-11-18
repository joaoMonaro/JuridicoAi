# P03 — Controle financeiro mensal por organização (2026-11-18)

Registro técnico, operacional e de validação da entrega P03 no backend e no frontend, sem declaração de homologação do piloto ou ativação em produção.

## Resumo do implementado

- **Modelo e persistência (`20261118174519_AddAiMonthlyFinancialControl` e `20261118181259_AllowOrphanMarkIndeterminateAudit`):**
  - `ai_organization_monthly_limits`: override persistido opcional por organização (`numeric(18,8)`), auditado em `ai_financial_audit_logs`.
  - `ai_monthly_accounts`: conta agregada por `(organization_id, period_year, period_month)` no **mês calendário UTC**, com `limit_usd`, `consumed_usd` e `reserved_usd` separados (`numeric(18,8)`).
  - `ai_usage_reservations`: reserva por chamada física com unicidade `(solicitacao_id, attempt_count)`, congelamento de modelo, versão de preço, tarifas e período de origem, estados `Reserved`, `Settled`, `Released` e `Indeterminate`.
  - `ai_financial_audit_logs`: trilha imutável das alterações de limite (`SetMonthlyLimit`), reconciliações de `Indeterminate` (`ReconcileSettle`, `ReconcileRelease`) e recuperação operacional de reservas `Reserved` órfãs (`MarkIndeterminate`, `ReconcileSettle`, `ReconcileRelease`).
- **Concorrência, snapshot e integridade de contrato:**
  - Serialização por ordem consistente de lock por organização (`ai_monthly_accounts` -> `organizations` -> `ai_organization_monthly_limits`) tanto em `ReservarAsync` quanto em `DefinirLimiteOrganizacaoAsync`, evitando lost update administrativo e impedindo reserva baseada em leitura antiga de limite.
  - `AssistenteUsageRecorder` preserva integralmente `FrozenEstimatedCostUsd` (inclusive `null` quando o `usage` retornado é incompleto) sempre que `FrozenPricingVersion` estiver preenchido, sem recalcular parcialmente com tarifas atualizadas após o congelamento.
  - `OpenAiAssistenteIaProvider` valida `ExpectedModel` contra o snapshot congelado antes do despacho HTTP e `AssistenteFinancialMeter` revalida `reservation.Model` antes da chamada física, bloqueando com `AI_CONFIGURATION_DIVERGED` caso haja divergência entre `IOptionsMonitor` e a instância do provedor até reconfiguração/reinício.
  - Removida qualquer reserva retroativa após ferramentas (`fallbackReservation`): interações bloqueadas antes de chamar o provedor (`LimitedAsync` com zero tentativas físicas) não gravam reservas nem ledger; provedores sem telemetria/callbacks sob controle ativo falham por contrato (`AI_FINANCIAL_CONTRACT_VIOLATION`); falhas de persistência do ledger preservam `AI_USAGE_PERSISTENCE_FAILED` sem tentar inserção agregada duplicada.
- **Configuração operacional:**
  - `AssistenteIa:DefaultMonthlyLimitUsd = 5.0` (US$ 5,00 como configuração inicial para o piloto, sem limite já implantado em ambiente ativo).
  - Tarifas (`InputCostPerMillionTokensUsd`, `CachedInputCostPerMillionTokensUsd`, `OutputCostPerMillionTokensUsd`) permanecem `0` e `PricingVersion = "unconfigured"` por padrão na configuração versionada, sem inventar preços.
  - Quando controle financeiro está ativo e preços/versão estão ausentes ou inválidos, chamadas ao provedor são bloqueadas antes do envio sem expor detalhes financeiros na mensagem.
  - Opt-out explícito `AssistenteIa:AllowSimulatedWithoutFinancialControl` (padrão `false`) é aceito apenas fora de `Production` para testes simulados com fakes; em `Production`, a inicialização (`ValidateOnStart`) e o serviço rejeitam o opt-out.
- **Fluxos cobertos:**
  - Chamada direta sem ferramentas (`AssistenteIaApplicationService.GerarAsync`), loop de ferramentas/recuperação documental/compactação de memória (`OpenAiAssistenteIaProvider.GerarComFerramentasAsync` via `AssistenteFinancialMeter`), geração durável por seções e revisão (`AssistenteGenerationWorker`) e análise documental em lotes (`AssistenteDocumentAnalysisWorker`).
  - Pré-validação em `AssistenteIaApplicationService.PrepararAsync` e `AssistenteChatService.IniciarAsync` (retornando HTTP 429/503 antes do SSE quando já insuficiente ou mal configurado) e reserva atômica em PostgreSQL antes de cada chamada física paga, sem segurar lock durante I/O de rede.
  - Liquidação pelo `usage` retornado (inclusive quando há erro de formato após `usage` conhecido, e tratando cache ausente como input sem desconto). Falha comprovadamente antes do envio libera a reserva; cancelamento/timeout/erro após despacho sem `usage` mantém a reserva como `Indeterminate` no período de origem (inclusive após reinício ou virada de mês UTC).
- **Endpoint, E2E fake e frontend:**
  - `GET /assistente/limite-mensal` autenticado, lendo apenas `authContext.OrganizationId` e retornando `AssistenteLimiteMensalDto` somente com período UTC, estado e percentuais (`PercentualUtilizado`, `PercentualReservado`, `PercentualDisponivel` limitados a `[0, 100]`), sem valores monetários.
  - Painel existente (`AssistantPanel`, `MonthlyLimitBanner`, `useAssistantMonthlyLimit`) exibe estado/percentuais separando utilizado vs. reservado em andamento, alertas de 70% e 90%, estados de carregamento/erro/indisponibilidade sem fingir 0% usado, ação explícita `"Atualizar"` em bloqueio/indisponibilidade, polling enquanto aberto apenas durante operação durável local ou `possuiReservasEmAndamento`, reação a foco/visibilidade e virada de mês UTC (zero polling fechado, microtask cancelada no cleanup), preservando texto digitado, histórico, propostas, edição manual e exportação no bloqueio.
  - Contrato fake E2E (`e2e/support/fake-api.mjs`) atualizado com `GET /api/assistente/limite-mensal` e cenários de bloqueio 429 (`assistant-monthly-limit.spec.ts`).

## Operação, reconciliação e recuperação de reservas órfãs

Não existe endpoint público para liberar reservas indeterminadas ou órfãs. Reservas em `Indeterminate` ou `Reserved` (órfãs após queda abrupta de processo entre a gravação da reserva e a conclusão/transição pós-envio) permanecem descontadas do saldo disponível do período de origem até intervenção operacional auditada por CLI (executada via `dotnet run --project src/JuridicoAI.Api -- ...` ou binário publicado):

- **Definir/alterar override mensal da organização:**
  ```bash
  AI_LIMIT_ORGANIZATION_ID=<org-uuid> \
  AI_LIMIT_USD=5.00 \
  AI_OPERATION_UPDATED_BY_ID=<user-uuid> \
  AI_OPERATION_REASON="Ajuste autorizado para organização piloto" \
  dotnet JuridicoAI.Api.dll set-ai-monthly-limit
  ```
- **Reconciliar reserva `Indeterminate` confirmando consumo (com usage real apurado no provedor, ou conservador sem tokens):**
  ```bash
  AI_RESERVATION_ID=<reservation-uuid> \
  AI_RECONCILE_ACTION=settle \
  AI_RECONCILE_INPUT_TOKENS=1200 \
  AI_RECONCILE_CACHED_INPUT_TOKENS=200 \
  AI_RECONCILE_OUTPUT_TOKENS=350 \
  AI_OPERATION_UPDATED_BY_ID=<user-uuid> \
  AI_OPERATION_REASON="Conferido no painel do provedor após timeout de rede" \
  dotnet JuridicoAI.Api.dll reconcile-ai-reservation
  ```
- **Reconciliar reserva `Indeterminate` liberando após comprovar ausência de cobrança:**
  ```bash
  AI_RESERVATION_ID=<reservation-uuid> \
  AI_RECONCILE_ACTION=release \
  AI_OPERATION_UPDATED_BY_ID=<user-uuid> \
  AI_OPERATION_REASON="Confirmado no provedor que a requisição não foi faturada" \
  dotnet JuridicoAI.Api.dll reconcile-ai-reservation
  ```
- **Recuperar reserva órfã em estado `Reserved` (exige workers drenados e evidência explícita; não há expiração automática por tempo):**
  - Quando a chamada pode ter sido despachada antes da queda e ainda não foi auditada no provedor, converter conservadoramente para `Indeterminate` (`mark-indeterminate`):
    ```bash
    AI_RESERVATION_ID=<reservation-uuid> \
    AI_RECONCILE_ACTION=mark-indeterminate \
    AI_CONFIRM_WORKERS_DRAINED=true \
    AI_DRAIN_EVIDENCE="Deploy #42 drenado; pods antigos encerrados às 18:10Z sem requests ativos" \
    AI_OPERATION_UPDATED_BY_ID=<user-uuid> \
    AI_OPERATION_REASON="Reserva órfã movida conservadoramente para Indeterminate após queda do worker" \
    dotnet JuridicoAI.Api.dll recover-orphan-ai-reservation
    ```
  - Também aceita `settle` (com usage conferido no provedor) ou `release` (com comprovação de não envio/não cobrança), sempre rejeitando execução sem `AI_CONFIRM_WORKERS_DRAINED=true` e `AI_DRAIN_EVIDENCE` preenchido.

## Validação executada (sem chamadas pagas ao provedor)

- **Banco e harness independente de concorrência (PostgreSQL descartável):**
  - `dotnet ef migrations has-pending-model-changes`: aprovado (`No changes have been made to the model since the last migration`).
  - Harness `/tmp/juridicoai-p03-independent-review/review.csproj`: aprovado (`RACE_SAFE: orgLimit=1.00000000; accountLimit=1.00000000; reserved=0.00000000; loweredBeforeReservation=True; rejection=AI_MONTHLY_LIMIT_INSUFFICIENT`).
- **Backend (`dotnet test JuridicoAI.sln --no-restore` com PostgreSQL descartável em `JURIDICOAI_REVIEW_TEST_POSTGRES`):**
  - **324 aprovados**, **1 ignorado** (`OcrLocal_ExecutaPipelineRealESinalizaBaixaConfianca`, OCR fora do escopo ativo), **0 falhas** (325 total).
  - Inclui 23 testes em `AssistenteMonthlyLimitPostgresTests` cobrindo corrida entre DbContexts, barreira determinística entre leitura de limite e lock/reserva contra `DefinirLimiteOrganizacaoAsync`, criação concorrente de conta mensal, isolamento entre organizações, virada de mês UTC mantendo reserva antiga na origem, bloqueio antes de chamar provedor, idempotência de liquidação/reconciliação/recuperação de órfã e bloqueio de chave duplicada, recuperação explícita de `Reserved` órfã entre `DbContext`s rejeitando falta de confirmação/evidência, estado `Indeterminate` após despacho e reinício, liberação segura em falha comprovada antes do envio, preservação de `FrozenEstimatedCostUsd=null` com `FrozenPricingVersion`, ausência de reserva/ledger após `LimitedAsync`, falha de persistência de ledger em múltiplos turnos sem duplicar agregado e mantendo `AI_USAGE_PERSISTENCE_FAILED`, bloqueio por divergência de modelo entre monitor e provedor (`AI_CONFIGURATION_DIVERGED`), preços inválidos bloqueando mesmo em `Production` com opt-out, cobrança após erro de formato com `usage` conhecido, múltiplas chamadas físicas em ferramentas, geração durável e análise por lotes, e contrato HTTP/auth de `GET /assistente/limite-mensal`.
- **Frontend unitário, lint e build (`npm test -- --run`, `npm run lint`, `npm run build`):**
  - **19 arquivos / 150 testes aprovados**, **0 falhas** (inclui `monthly-limit.test.tsx` cobrindo percentuais sem valores monetários, polling com reserva externa aberta, reação a foco/virada de mês UTC, zero polling quando fechado e guarda de microtask após fechamento).
  - `eslint .` aprovado sem erros; `tsc -b && vite build` concluído com sucesso.
- **Frontend E2E (`npm run test:e2e`, suíte completa com API simulada):**
  - **70 testes aprovados**, **0 falhas**, incluindo acessibilidade, integridade, salvamento, histórico pessoal e os cenários de limite mensal.
  - Na execução inicial foram observadas falta de nome acessível no editor, região de conversa sem acesso por teclado e contraste insuficiente no indicador. O editor recebeu `aria-label`/`aria-multiline`, a conversa passou a ser focável e o indicador usa cores com contraste adequado. O cenário de troca de Peça agora abre o explorador quando recolhido, preservando as verificações de cancelamento e resposta tardia.
  - Uma rodada intermediária teve 69 aprovados e um timeout durante uma recarga do Vite no cenário de recuperação de salvamento. Esse cenário passou em três execuções isoladas; a rodada completa final passou com 70/70, sem reduzir verificações ou elevar o timeout.

## Revisão final do supervisor

A implementação foi delegada ao `agy` com `gemini-4-hydrogen-high`, em três rodadas de implementação e correção, seguida de revisão do código e verificações independentes. As branches de backend e frontend são `feature/p03-limite-mensal-ia`, originadas das respectivas `homologacao`. Esta evidência registra validação técnica local; não representa commit, publicação ou ativação em produção.

- A corrida entre alteração administrativa e reserva foi reproduzida antes da correção: limite da organização reduzido para 1, mas conta restaurada indevidamente para 5 e reserva de 4 aceita. O teste independente final retornou `RACE_SAFE`, mantendo organização e conta em 1 e rejeitando a reserva de 4. O cenário também está coberto por barreira determinística nos testes PostgreSQL do repositório.
- Foram reproduzidos três caminhos que gravavam consumo fictício antes de qualquer envio: saldo esgotado entre preparação e geração, recusa na primeira reserva com ferramentas e falha no primeiro `BeforeCall`. Após a correção, a recusa preserva o código de erro, não cria ledger financeiro nem consome o limite técnico de retomada. O registro agregado fica restrito aos testes simulados com opt-out; produção usa callbacks por chamada física. Os três cenários integram a suíte de 324 testes aprovados.
- A revisão do frontend reproduziu e corrigiu cancelamento contínuo de consultas lentas pelo polling e encerramento prematuro do temporizador em meses maiores que o timeout máximo do navegador. Dois testes de hook verificam resposta lenta e virada real de mês; as fixtures de período acompanham a data do teste.
- Cinco cenários independentes no navegador conferiram percentuais sem valores monetários, separação de consumo/reserva, preservação do pedido e edição manual no bloqueio, atualização após reserva externa, ausência de consultas ao fechar e virada de mês. Foram usados apenas dados fictícios e respostas simuladas; os estados disponível e bloqueado também foram inspecionados visualmente.
- O build do backend terminou sem erros/avisos. A suíte completa usou PostgreSQL descartável e ignorou somente OCR; a conferência EF não encontrou diferenças entre modelo e migrations. Frontend: 150 unitários, lint e build aprovados; permanece o aviso anterior de bundle acima de 500 kB.

Comandos finais principais:

```bash
dotnet build JuridicoAI.sln --no-restore
# Definir JURIDICOAI_REVIEW_TEST_POSTGRES para um PostgreSQL descartável.
dotnet test JuridicoAI.sln --no-build --no-restore
dotnet ef migrations has-pending-model-changes --project src/JuridicoAI.Api/JuridicoAI.Api.csproj --no-build
# No repositório frontend:
npm run test:run
npm run lint
npm run build
npm run test:e2e
```
