# Tópico 1 — Modelo e operação

> **Estado da decisão:** aprovado
>
> **Estado da implementação:** pendente
>
> **Data da decisão:** 2026-09-27
>
> **Pontos encerrados:** `PA-01` e `PA-02`

Este documento consolida as decisões de modelo, limites, orçamento, rate limit,
concorrência e resiliência do MVP. A aprovação deste tópico define o alvo da
implementação; não significa que todos os controles já estejam ativos no código.

## 1. Decisões aprovadas

### Modelo

O modelo principal será:

```text
gpt-5.6-terra
```

Ele atenderá conversa sobre a Peça, reescrita, criação de seção, revisão e o
fluxo de geração de Peça previsto para o MVP. Não haverá roteamento automático
entre modelos no MVP e nenhum fallback silencioso para Luna, Sol ou outro modelo.

O identificador foi conferido na
[documentação oficial do GPT-5.6 Terra](https://developers.openai.com/api/docs/models/gpt-5.6-terra)
em 2026-09-27. O modelo suporta Responses API, streaming e saída estruturada.
A escolha continua configurável por ambiente; o identificador não deve ser
espalhado como constante no domínio ou nas regras de negócio.

### Limites de entrada e saída

O limite total de entrada será:

```text
MAX_INPUT_CONTEXT = 60_000 tokens
```

Esse total considera o payload efetivamente enviado ao modelo, incluindo
instruções, contexto da Peça, dados autorizados do Caso e a sobrecarga do formato
da requisição. Limites em caracteres continuam úteis como proteção antecipada,
mas não substituem a contabilização em tokens.

Os limites máximos de saída serão escolhidos pelo perfil da operação:

| Perfil | Fluxo atual ou planejado | Máximo |
|---|---|---:|
| `Chat` | `Perguntar` / `ResponderNoPainel` | 2.000 tokens |
| `Edit` | `Reescrever` / `SubstituirSelecao` | 2.000 tokens |
| `Insert` | `Redigir` / inserir trecho ou criar seção | 4.000 tokens |
| `Review` | `Revisar` / `ResponderNoPainel` | 4.000 tokens |
| `Document` | geração integral de Peça | 8.000 tokens |

O parâmetro `max_output_tokens` contabiliza também tokens não visíveis usados
pelo modelo. Os valores serão avaliados com operações reais para verificar se a
quantidade de texto visível é suficiente.

O perfil `Document` fica reservado para o fluxo do MVP ainda não implementado.
A geração integral ainda não possui modo, contrato de aplicação ou experiência
de revisão no produto e não será encaixada implicitamente em `Redigir`. Esses
detalhes são tratados em `PA-16`, preservando revisão humana e aplicação explícita.

### Orçamento

O orçamento financeiro pertence à Organization e é compartilhado pelos seus
usuários:

```text
MonthlyOrganizationBudgetUsd = 10.00
```

O valor é uma proteção técnica inicial, configurável por Organization, e não
representa a precificação comercial definitiva.

| Consumo do orçamento | Comportamento |
|---:|---|
| 70% | alerta |
| 90% | alerta crítico |
| 100% | bloqueio de novas solicitações de IA |

Não haverá orçamento financeiro individual no MVP. Por usuário, serão aplicados:

```text
10 solicitações por minuto
2 solicitações simultâneas
```

Também serão aplicados limites agregados e configuráveis por Organization:

```text
30 solicitações por minuto
6 solicitações simultâneas
```

Os limites de usuário e Organization são cumulativos. A solicitação precisa ter
capacidade disponível nos dois níveis. A política detalhada de isolamento e
liberação das vagas está no
[Tópico 11 — Segurança, isolamento e logs](topico-11-seguranca-isolamento-logs.md).

### Retry e fallback

Uma solicitação poderá executar a tentativa inicial e, no máximo, duas novas
tentativas:

```text
GPT-5.6 Terra -> Retry 1 -> Retry 2 -> erro controlado
```

As novas tentativas usam o mesmo modelo. Não haverá fallback automático nem
troca para um modelo mais caro.

Retry será permitido somente para falha transitória e antes de qualquer delta
do provider ter sido consumido. Devem ser tratados como transitórios: sobrecarga
do modelo, rate limit temporário, timeout de rede e falhas 5xx recuperáveis.
Cancelamento, autenticação, payload inválido, orçamento esgotado e saída
estrutural inválida não geram retry automático.

Quando houver `Retry-After`, o valor informado pelo provider será respeitado.
Sem esse cabeçalho, será usado backoff exponencial com jitter. A soma das
tentativas não pode ultrapassar o deadline total da solicitação. A orientação é
compatível com os [códigos de erro oficiais da OpenAI](https://developers.openai.com/api/docs/guides/error-codes).

Nenhuma tentativa parcial altera a Peça. Depois das falhas permitidas, o usuário
recebe erro controlado e pode iniciar uma nova solicitação manualmente.

## 2. Diferenças para a implementação atual

| Área | Estado atual | Alvo aprovado |
|---|---|---|
| Modelo | configurável, sem valor padrão | `gpt-5.6-terra` por configuração |
| Entrada | limites por caracteres | limite adicional de 60.000 tokens reais |
| Saída | único `MaxOutputTokens=6000` | limite por perfil de 2k, 4k ou 8k |
| Rate limit | 6 solicitações/minuto por usuário | 10 solicitações/minuto por usuário |
| Concorrência | deduplicação por `clientRequestId` | máximo de 2 solicitações simultâneas por usuário |
| Rate limit da Organization | não implementado | 30 solicitações/minuto por Organization |
| Concorrência da Organization | não implementada | máximo de 6 solicitações simultâneas por Organization |
| Orçamento | não implementado | US$ 10/mês por Organization, configurável |
| Alertas | não implementados | 70%, 90% e bloqueio em 100% |
| Retry | não implementado no adapter | até 2 retries transitórios, sem fallback |
| Auditoria de custo | uso de tokens apenas em log | ledger técnico por Organization e solicitação |

## 3. Configuração alvo

Os nomes abaixo orientam a implementação e podem ser refinados mantendo o mesmo
significado:

```json
{
  "AssistenteIa": {
    "Model": "gpt-5.6-terra",
    "MaxInputTokens": 60000,
    "MaxOutputTokens": {
      "Chat": 2000,
      "Edit": 2000,
      "Insert": 4000,
      "Review": 4000,
      "Document": 8000
    },
    "RequestsPerUserPerMinute": 10,
    "ConcurrentRequestsPerUser": 2,
    "RequestsPerOrganizationPerMinute": 30,
    "ConcurrentRequestsPerOrganization": 6,
    "Retry": {
      "MaxRetries": 2,
      "UseExponentialBackoff": true,
      "RespectRetryAfter": true
    },
    "Budget": {
      "DefaultMonthlyOrganizationBudgetUsd": 10.00,
      "WarningPercentage": 70,
      "CriticalPercentage": 90,
      "BlockPercentage": 100
    }
  }
}
```

Preços do provider precisam ficar em tabela versionada ou configuração
operacional com vigência, nunca espalhados na regra de negócio. Em 2026-09-27,
a página oficial do modelo informava US$ 2 por milhão de tokens de entrada,
US$ 0,20 por milhão de entrada em cache e US$ 12 por milhão de tokens de saída.
Esses valores são referência inicial e devem poder mudar sem deploy da regra de
orçamento.

## 4. Plano técnico de implementação

### Etapa A — perfis e contabilização de tokens

- criar `AssistenteIaOutputProfile` e mapear modo/operação para um perfil;
- substituir o único limite de saída pela configuração por perfil;
- criar `IAssistenteTokenCounter` na camada de aplicação;
- no adapter OpenAI, contar o payload equivalente antes da geração usando
  `POST /v1/responses/input_tokens`;
- rejeitar acima de 60.000 tokens com código `AI_INPUT_TOO_LARGE`;
- manter limites de caracteres como guarda barata antes da contagem remota;
- registrar contagem estimada, contagem retornada no uso e divergência.

A OpenAI recomenda a contagem com o mesmo formato da Responses API porque ela
inclui instruções e a sobrecarga estrutural. Consulte
[Counting tokens](https://developers.openai.com/api/docs/guides/token-counting).

### Etapa B — retry controlado

- introduzir uma política de retry ao redor da chamada do provider;
- limitar o conjunto de erros elegíveis e o total a duas novas tentativas;
- respeitar `Retry-After` e aplicar backoff exponencial com jitter;
- impedir retry depois do primeiro delta recebido do provider;
- preservar cancelamento e deadline global;
- registrar número de tentativas e código final, sem prompt ou resposta;
- testar sucesso após retry, esgotamento, cancelamento e erro não elegível.

### Etapa C — rate limit e concorrência

- alterar o limite configurado para 10 requisições por minuto;
- adicionar limite de 30 requisições por minuto por Organization;
- adicionar limites de concorrência de 2 por usuário e 6 por Organization;
- exigir aquisição cumulativa das partições de Organization e usuário;
- adquirir a vaga antes da contagem remota e liberá-la em `finally`;
- responder 429 com códigos distintos para frequência e concorrência;
- manter a deduplicação de `clientRequestId` como proteção independente.

### Etapa D — orçamento por Organization

- adicionar `Organization.MonthlyAiBudgetUsd` como `decimal`, com valor inicial
  de US$ 10 e possibilidade de ajuste administrativo;
- criar migration e validação que impeçam orçamento negativo;
- criar ledger mensal sem prompt, resposta ou dados pessoais;
- armazenar modelo, modo, tokens, custo, tentativa, estado e IDs técnicos;
- calcular uma reserva antes da geração usando entrada contada e saída máxima;
- reservar de forma transacional para evitar estouro por concorrência;
- reconciliar reserva com o uso real devolvido pelo provider;
- bloquear nova geração em 100% com `AI_BUDGET_EXCEEDED`;
- expor estado de alerta sem revelar dados financeiros a perfis não autorizados;
- tratar virada do mês em UTC e definir moeda como USD no MVP.

### Etapa E — frontend e operação

- mapear novos códigos de limite, concorrência e orçamento para mensagens claras;
- exibir alerta de 70% e alerta crítico de 90% conforme autorização;
- não perder instrução ou contexto quando uma solicitação for recusada;
- criar métricas de tokens, custo, retries, bloqueios e latência sem conteúdo;
- implementar o ledger e os indicadores conforme o
  [Tópico 14 — Observabilidade e métricas](topico-14-observabilidade-metricas.md);
- documentar ajuste de orçamento e preços por ambiente;
- executar carga controlada e amostra de operações jurídicas antes do piloto.

## 5. Critérios de aceite

- [ ] Todas as operações atuais enviam `gpt-5.6-terra` por configuração.
- [ ] Cada modo/operação recebe exatamente o perfil de saída definido.
- [ ] Payload acima de 60.000 tokens é recusado antes da geração.
- [ ] O uso real de entrada e saída é registrado sem conteúdo jurídico.
- [ ] A 70% e 90% são produzidos alertas distintos.
- [ ] Em 100%, nenhuma nova chamada ao provider é iniciada.
- [ ] Cada usuário é limitado a 10 solicitações/minuto e 2 simultâneas.
- [ ] Cada Organization é limitada a 30 solicitações/minuto e 6 simultâneas.
- [ ] Erros transitórios executam no máximo duas novas tentativas.
- [ ] Não há retry após delta, cancelamento ou erro não transitório.
- [ ] Nenhum fluxo seleciona outro modelo automaticamente.
- [ ] Falha final não altera a Peça nem descarta o pedido digitado.
- [ ] Custos médios por operação e por Organization podem ser medidos.

## 6. Medição pós-implementação

Depois que os controles estiverem funcionais, executar amostras representativas
de conversa, alteração, inserção, revisão e, quando existir, geração integral.
Medir tokens de entrada e saída, custo e latência por operação, custo por Peça e
consumo mensal por Organization. Esses dados orientarão a revisão do orçamento e
a futura precificação comercial.
