# Tópico 14 — Observabilidade e métricas

> **Organização em 2026-10-23:** planejamento em espera (`standBy`).
> Consulte o [índice de pendências](../../README.md) para a prioridade atual.

> **Estado da decisão:** estratégia aprovada
>
> **Estado da implementação:** parcial
>
> **Data da decisão:** 2026-09-27
>
> **Ponto relacionado:** `PA-14` permanece aberto e bloqueia produção

Este documento define como responder continuamente a três perguntas:

```text
Está funcionando?
Está sendo útil?
Quanto está custando?
```

Toda observabilidade é construída sem armazenar prompt, resposta ou conteúdo
jurídico. O `PA-14` só poderá ser encerrado quando coleta, dashboards, alertas,
responsáveis e política de acesso existirem e forem testados.

## 1. Princípios

- métricas e traces usam somente campos previamente permitidos;
- conteúdo nunca vira label, atributo, mensagem ou nome de span;
- `OrganizationId` não vira label de métrica de alta cardinalidade;
- custos por Organization ficam em ledger técnico consultável;
- eventos de produto são explícitos e idempotentes;
- salvar uma Peça não permite inferir que uma sugestão foi aplicada;
- falha de telemetria não pode quebrar geração, edição ou autosave;
- correlação usa identificadores opacos, nunca texto jurídico;
- dashboards agregados respeitam autorização e isolamento de tenant.

A política de campos proibidos do
[Tópico 11](pa-11-seguranca-isolamento-logs.md) também se aplica a métricas,
traces, eventos de produto, exportadores e ferramentas de APM.

## 2. Identificadores de correlação

Cada solicitação iniciada pelo frontend recebe um UUID opaco:

```text
CorrelationId
```

Fluxo:

```text
Frontend
  CorrelationId
       ↓
POST da solicitação
       ↓
Backend / Activity / logs / métricas
       ↓
HttpClient do provider
       ↓
evento SSE / erro / evento de produto posterior
```

Identificadores têm responsabilidades distintas:

| Identificador | Origem | Finalidade |
|---|---|---|
| `CorrelationId` | frontend; backend cria se ausente | correlacionar a jornada ponta a ponta |
| `clientRequestId` | frontend | deduplicação e identidade da tentativa lógica |
| `solicitacaoId` | backend | identificar a execução interna da IA |
| `TraceId` | infraestrutura | tracing distribuído do processamento HTTP |
| provider request ID | provider, quando disponível | suporte técnico com o fornecedor |
| `suggestionId` | backend | vincular eventos explícitos à sugestão |

O frontend envia `X-Correlation-ID`. O backend valida formato e tamanho; se o
header estiver ausente ou inválido, gera outro UUID. O valor aceito é devolvido
no header da resposta e disponibilizado nos eventos SSE relevantes.

`CorrelationId` não é segredo nem autorização. Ele não pode selecionar tenant,
Caso, Peça ou histórico. O trace da chamada ao provider herda a Activity do
backend, sem enviar Organization, User ou conteúdo no corpo apenas para fins de
correlação.

Erros apresentados ao usuário podem exibir um código curto de suporte derivado
do `CorrelationId`, permitindo investigação sem pedir a Peça jurídica.

## 3. Métricas técnicas — “Está funcionando?”

### Instrumentos

| Métrica | Tipo | Descrição |
|---|---|---|
| `ai.requests` | counter | solicitações recebidas |
| `ai.requests.completed` | counter | resultados por status final |
| `ai.request.ttft_ms` | histogram | tempo até primeiro conteúdo renderizável |
| `ai.request.duration_ms` | histogram | tempo total até estado terminal |
| `ai.request.active` | up/down counter | solicitações atualmente em execução |
| `ai.provider.attempts` | counter | tentativas no provider, incluindo retries |
| `ai.provider.failures` | counter | falhas por código controlado |
| `ai.rate_limit.rejections` | counter | bloqueios por usuário ou Organization |
| `ai.budget.rejections` | counter | bloqueios por orçamento |

Dimensões permitidas e de cardinalidade controlada:

```text
environment
provider
model
mode
operation
status
error_code
retryable
rollout_stage
```

Não usar como labels de métricas:

```text
CorrelationId
RequestId
UserId
OrganizationId
PecaId
CasoId
SuggestionId
```

Esses identificadores podem existir em logs estruturados ou traces com acesso
restrito, mas não em séries temporais de alta cardinalidade.

### Semântica

- `request count`: incrementa uma vez quando a API aceita a solicitação para
  validação;
- `success`: existe `solicitacao.concluida` válido;
- `failure`: existe falha controlada antes ou durante o stream;
- `cancelled`: processamento interrompido antes do terminal, com origem
  identificada quando possível;
- `status`: conjunto fechado, como `success`, `failure`, `cancelled`, `timeout`,
  `rate_limited`, `budget_blocked` ou `feature_disabled`;
- `model`: modelo efetivamente informado pelo provider, sem deployment secreto;
- `TTFT`: do aceite da requisição até o primeiro conteúdo que o frontend pode
  renderizar; se a primeira resposta útil for o evento final, ele define TTFT;
- `duration`: do aceite até conclusão, falha ou cancelamento.

`solicitacao.iniciada` não conta como primeiro token. O provider atual acumula a
saída estruturada antes de emitir o delta externo; portanto o TTFT atual mede a
experiência real do usuário, não uma promessa de streaming interno.

## 4. Métricas financeiras — “Quanto está custando?”

Cada execução ou tentativa cobrada alimenta um ledger técnico com:

```text
OrganizationId
CorrelationId
SolicitacaoId
Operation
Mode
Provider
Model
PromptVersion
AttemptNumber
InputTokens
CachedInputTokens, quando disponível
OutputTokens
EstimatedCostUsd
PricingVersion
Status
CreatedAt
```

Não entram no ledger instrução, contexto, resposta, nomes de partes ou conteúdo
da Peça. `OrganizationId` é obtido do contexto autenticado.

O custo estimado usa tabela de preços versionada e vigente no instante da
tentativa. Mudanças futuras de preço não reescrevem silenciosamente o custo
histórico. Retries potencialmente cobrados também entram no custo, mesmo quando
a solicitação final falhar.

Quando o provider não devolver uso de uma tentativa incompleta, registrar
`usage_unavailable` e não fabricar uma precisão inexistente. Estimativas devem
ser identificadas como estimativas e comparadas periodicamente com a fatura do
provider.

Indicadores mínimos:

- custo estimado total e diário;
- custo por Organization;
- custo por modo/operação;
- custo por modelo;
- custo médio por solicitação bem-sucedida;
- tokens de entrada e saída por operação;
- consumo do orçamento mensal e faixas de 70%, 90% e 100%;
- diferença entre custo estimado e faturado, quando disponível.

O ledger e o orçamento seguem o
[Tópico 1 — Modelo e operação](pa-01-modelo-e-operacao.md).

## 5. Métricas de produto — “Está sendo útil?”

Eventos iniciais:

| Evento | Momento exato |
|---|---|
| `suggestion_generated` | backend conclui uma sugestão válida |
| `suggestion_applied` | transação TipTap solicitada pelo usuário retorna sucesso |
| `suggestion_rejected` | usuário descarta explicitamente a sugestão |
| `suggestion_regenerated` | usuário inicia nova geração a partir da anterior |
| `suggestion_expired` | âncora passa a inválida; emitir apenas uma vez |
| `request_cancelled` | usuário ou navegação cancela geração em andamento |

`suggestion_applied` representa aplicação local explícita, não persistência. O
resultado do autosave deve ser observado separadamente para não afirmar que uma
alteração local foi salva quando o autosave falhou.

Eventos originados no frontend são enviados a endpoint autenticado, com payload
fechado e sem conteúdo:

```json
{
  "eventId": "07825937-ed23-46aa-9048-70597888a02e",
  "correlationId": "e423067d-93a9-40d2-9699-a688cf21c58a",
  "suggestionId": "0c31dd95-fcc7-44b4-ab86-28f04fe6dd90",
  "type": "suggestion_applied",
  "parentSuggestionId": null,
  "occurredAt": "2026-09-27T18:30:00Z"
}
```

O backend deriva Organization e usuário da identidade autenticada, valida a
Peça/sugestão no tenant e deduplica por `eventId`. Telemetria de produto não é
uma autorização nem uma fonte para reconstruir o documento.

## 6. Indicadores derivados

As taxas usam sugestões distintas e eventos deduplicados:

```text
ApplyRate = sugestões editáveis aplicadas
            / sugestões editáveis geradas

RejectRate = sugestões explicitamente rejeitadas
             / sugestões editáveis geradas

RegenerateRate = sugestões que originaram nova geração
                 / sugestões geradas

AnchorExpirationRate = sugestões ancoradas expiradas
                       / sugestões ancoradas geradas

CancellationRate = solicitações canceladas
                   / solicitações iniciadas

SuccessRate = solicitações concluídas
              / solicitações com estado terminal
```

Respostas `ResponderNoPainel` não entram no denominador de `ApplyRate` e
`RejectRate`, pois não oferecem escrita. As taxas não precisam somar 100%: uma
sugestão pode permanecer sem decisão, ser copiada ou expirar antes de uma ação.

Segmentações permitidas:

- período;
- modo e operação;
- modelo e versão do prompt;
- estágio do rollout;
- Organization em visualização autorizada e não como label global.

Não criar ranking individual de usuários no MVP.

## 7. Dashboards

### Saúde técnica

- volume e solicitações ativas;
- `SuccessRate`, falhas, cancelamentos e timeouts;
- TTFT e duração em p50, p95 e p99;
- falhas por código e provider;
- rate limit, orçamento e feature flag bloqueando chamadas;
- retries e indisponibilidade.

### Utilidade

- sugestões geradas por modo/operação;
- `ApplyRate`, `RejectRate`, `RegenerateRate` e `AnchorExpirationRate`;
- aplicação local seguida de sucesso ou erro de autosave;
- comparação por versão do prompt/modelo e estágio de rollout.

### Custo

- tokens e custo estimado por dia;
- custo por Organization, operação e modelo;
- consumo mensal e projeção;
- Organizations nas faixas de 70%, 90% e 100%;
- solicitações bloqueadas por orçamento.

## 8. Alertas e operação

Alertas mínimos:

- aumento sustentado da taxa de falha;
- indisponibilidade ou sequência de timeouts do provider;
- p95 de TTFT ou duração acima do limite operacional;
- crescimento anormal de cancelamentos;
- ausência inesperada de sucessos durante uso ativo;
- orçamento em 70%, 90% e 100%;
- custo por hora/dia fora do comportamento esperado;
- aumento abrupto de `AnchorExpirationRate`;
- queda relevante de `ApplyRate` ou crescimento de `RegenerateRate` após mudança
  de prompt/modelo.

Os valores numéricos de alerta serão calibrados no tenant interno e registrados
antes do piloto. Cada alerta precisa de:

```text
limiar e janela
severidade
responsável
canal de notificação
runbook
condição de resolução
```

Métricas de produto orientam investigação, mas não disparam rollback sozinhas
sem considerar volume e contexto. Falhas impeditivas de segurança continuam
tendo precedência.

## 9. Privacidade, retenção e acesso

- nenhum conteúdo jurídico é coletado;
- nenhum atributo livre fornecido pelo usuário vira telemetria;
- nomes de operação, status e códigos usam enums controlados;
- logs e eventos técnicos brutos seguem retenção inicial de 90 dias;
- acesso a custo por Organization é restrito a perfis autorizados;
- dashboards gerais usam agregação e não expõem dados de outro tenant;
- exportadores e traces seguem as mesmas regras de redaction do backend;
- IDs sintéticos da suíte de testes não são misturados com produção.

Retenção superior ou uso financeiro/contratual do ledger deve ser alinhado com
o `PA-17`. A política de logs e exclusão está no
[Tópico 3](pa-03-retencao-exclusao-lgpd.md).

## 10. Implementação técnica sugerida

### Backend

- instrumentar com `Meter` e `ActivitySource` do .NET;
- exportar métricas e traces por configuração, sem acoplar domínio ao backend de
  observabilidade;
- criar middleware para validar/gerar/ecoar `X-Correlation-ID`;
- propagar Activity pela chamada `HttpClient` do provider;
- criar `IAssistenteTelemetry` com métodos tipados, sem dicionário arbitrário;
- persistir ledger financeiro de forma transacional com orçamento;
- criar endpoint idempotente para eventos de produto;
- garantir que falha do exportador não interrompa a solicitação.

### Frontend

- gerar `CorrelationId` no envio e preservá-lo durante o stream;
- incluí-lo em retry somente quando for a mesma tentativa técnica; nova geração
  recebe novo `CorrelationId` e novo `clientRequestId`;
- emitir somente eventos explícitos e enums conhecidos;
- nunca enviar texto, seleção, diff, instrução ou resposta à telemetria;
- exibir referência de suporte em erros controlados;
- evitar duplicação ao remontar, reabrir ou repetir callbacks.

## 11. Situação encontrada

Já existe:

- `clientRequestId` criado no frontend;
- `solicitacaoId` criado no backend;
- middleware que valida ou gera `CorrelationId`, define
  `HttpContext.TraceIdentifier`, adiciona scope de log e devolve o header;
- `CorrelationId` no evento SSE inicial e no ledger;
- logs estruturados com Organization, usuário, Peça, modo e IDs técnicos;
- log de conclusão com modelo, tokens e duração;
- uso de tokens, inclusive entrada em cache quando informada, retornado no
  evento final;
- migration e ledger persistente com custo estimado e tabela de preços
  configurável/versionada; enquanto as tarifas estiverem como
  `unconfigured`, tokens são registrados e o custo permanece `null`;
- registro dos estados `succeeded`, `failed` e `cancelled` sem conteúdo
  jurídico;
- testes de correlação, custo e ausência de conteúdo sensível nos logs.

Ainda falta:

- `CorrelationId` criado e enviado explicitamente pelo frontend;
- métricas com `Meter`, traces próprios e exportador;
- medição formal de TTFT;
- reconciliação do ledger com a fatura do provider;
- eventos explícitos de produto;
- endpoint e deduplicação desses eventos;
- dashboards técnicos, financeiros e de produto;
- alertas, runbooks, responsáveis e controle de acesso;
- testes de integração que inspecionem exportadores e persistência real.

## 12. Testes obrigatórios

- header ausente ou inválido gera `CorrelationId` seguro no backend;
- o mesmo ID aparece no response, trace, logs e chamada instrumentada ao
  provider;
- IDs não concedem acesso nem atravessam Organizations;
- sucesso, falha, timeout e cancelamento geram exatamente um estado terminal;
- TTFT e duração usam relógio monotônico e unidades corretas;
- retries contam tentativas e custos sem duplicar a solicitação lógica;
- eventos de produto repetidos com o mesmo `eventId` são idempotentes;
- `ApplyRate` exclui respostas sem operação de escrita;
- âncora expirada é contada uma única vez;
- autosave em erro não é registrado como persistência bem-sucedida;
- preços versionados produzem custo esperado;
- falha do coletor/exportador não falha a geração;
- marcadores de prompt, resposta, CPF, JWT e API key não aparecem em métricas,
  traces, logs ou ledger;
- dashboard por Organization não exibe outra Organization.

## 13. Critérios para encerrar o PA-14

- [ ] `CorrelationId` funciona ponta a ponta.
- [ ] Métricas técnicas, financeiras e de produto estão sendo coletadas.
- [ ] Ledger de tokens e custo está implementado e reconciliável.
- [ ] Eventos de produto são explícitos, autenticados e idempotentes.
- [ ] Os três dashboards existem e possuem controle de acesso.
- [ ] Alertas têm limiar, responsável, canal e runbook.
- [ ] Testes comprovam que nenhum conteúdo proibido é coletado.
- [ ] Retenção e acesso foram validados com PA-03/PA-17.
- [ ] Dados do tenant interno foram revisados antes do piloto.
- [ ] Existe evidência de que falha da telemetria não afeta o produto.

A estratégia aprovada, isoladamente, não encerra o ponto.
