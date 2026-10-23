# Tópico 13 — Feature flags e rollout

> **Organização em 2026-10-23:** planejamento em espera (`standBy`).
> Consulte o [índice de pendências](../../README.md) para a prioridade atual.

> **Estado da decisão:** estratégia aprovada
>
> **Estado da implementação:** controles operacionais implementados; execução do rollout pendente
>
> **Data da decisão:** 2026-09-27
>
> **Ponto relacionado:** `PA-13` permanece aberto e bloqueia produção

Este documento define como as funcionalidades de IA serão ativadas, desativadas
e liberadas gradualmente. Esconder controles no frontend não constitui controle
de acesso: toda decisão efetiva ocorre novamente no backend.

## 1. Flags aprovadas

### Kill switch global

```text
AI_ENABLED
```

`AI_ENABLED=false` desabilita todas as operações de IA em todas as
Organizations. Essa flag é o kill switch operacional e tem precedência sobre
qualquer liberação por tenant.

`AssistenteIa:Enabled` é a trava superior de bootstrap. Se estiver desligada,
nenhum valor persistido pode habilitar a IA. Quando estiver ligada, o estado
operacional `AI_ENABLED` é consultado no banco em cada nova verificação e pode
ser alterado pelo comando `set-ai-enabled` sem novo deploy ou restart.

Na ausência inicial do registro persistido, o estado operacional é considerado
habilitado e ainda depende da trava de bootstrap e das flags por Organization.
Depois da primeira alteração, o valor atual permanece em
`ai_operational_controls`.

### Flags por Organization

```text
AI_ASSISTANT
AI_EDIT
AI_REVIEW
AI_DOCUMENT_GENERATION
```

| Flag | Capacidade liberada |
|---|---|
| `AI_ASSISTANT` | acesso base ao assistente e modo de conversa/pergunta |
| `AI_EDIT` | redação, reescrita, inserção, substituição e criação de seção |
| `AI_REVIEW` | revisão e análise estruturada da Peça |
| `AI_DOCUMENT_GENERATION` | fluxo próprio de geração integral definido no `PA-16` |

As flags específicas nunca ativam o produto sozinhas. A regra efetiva é:

```text
AI_ENABLED
  AND AI_ASSISTANT da Organization
  AND flag específica da operação, quando aplicável
```

Exemplos:

| Global | Assistant | Edit | Review | Resultado |
|:---:|:---:|:---:|:---:|---|
| não | sim | sim | sim | nenhuma operação disponível |
| sim | não | sim | sim | nenhuma operação disponível |
| sim | sim | não | não | somente conversa/pergunta |
| sim | sim | sim | não | conversa, redação e reescrita |
| sim | sim | não | sim | conversa e revisão |

Ausência de registro equivale a `Enabled=false`. O sistema falha fechado.

## 2. Modelo de persistência

Estrutura inicial:

```text
OrganizationFeature
├── OrganizationId
├── Feature
├── Enabled
├── UpdatedAt
└── UpdatedById

AiOperationalControl
├── Feature = AI_ENABLED
├── Enabled
├── UpdatedAt
├── UpdatedById
└── Reason

AiFeatureChangeAudit
├── Scope = Global | Organization
├── OrganizationId, quando aplicável
├── Feature
├── Enabled
├── ChangedAt
├── ChangedById
└── Reason
```

Regras:

- chave única composta por `OrganizationId + Feature`;
- `Feature` é enum/conjunto fechado no backend;
- `OrganizationId` precisa referenciar Organization existente;
- alteração registra data e responsável;
- exclusão ou ausência do registro desabilita a feature;
- nunca aceitar `OrganizationId` do corpo como autoridade para consultar flags;
- alterações administrativas precisam de auditoria sem conteúdo jurídico.
- cada mudança gera um registro append-only; atualizar o estado corrente não
  apaga o histórico;
- a justificativa é técnica, limitada a 500 caracteres e não pode conter dados
  do Caso, da Peça ou das partes.

No MVP, não haverá condição percentual, segmentação aleatória ou expressão de
regras. A liberação será explícita para Organizations nomeadas.

## 3. Validação no backend

O backend é a fonte de verdade. Para cada operação:

1. resolver a Organization pelo contexto autenticado;
2. verificar `AI_ENABLED`;
3. verificar `AI_ASSISTANT` para a Organization;
4. mapear modo/operação para a flag específica;
5. recusar antes de montar contexto, contabilizar tokens ou chamar o provider;
6. registrar apenas IDs técnicos, flag e resultado permitido/negado.

Mapeamento inicial:

| Modo/fluxo | Flags exigidas |
|---|---|
| `Perguntar` / `ResponderNoPainel` | `AI_ENABLED` + `AI_ASSISTANT` |
| `Redigir` | `AI_ENABLED` + `AI_ASSISTANT` + `AI_EDIT` |
| `Reescrever` | `AI_ENABLED` + `AI_ASSISTANT` + `AI_EDIT` |
| `Revisar` | `AI_ENABLED` + `AI_ASSISTANT` + `AI_REVIEW` |
| geração integral | `AI_ENABLED` + `AI_ASSISTANT` + `AI_DOCUMENT_GENERATION` |

O endpoint de capacidades não substitui a validação da rota de geração. Um
cliente pode ignorar o frontend, reutilizar resposta em cache ou chamar a API
diretamente; a requisição ainda deve ser negada.

Respostas previstas:

| Situação | HTTP | Código |
|---|---:|---|
| kill switch global desligado | 503 | `AI_UNAVAILABLE` |
| `AI_ASSISTANT` ausente/desligado | 403 | `AI_FEATURE_NOT_ENABLED` |
| flag específica ausente/desligada | 403 | `AI_FEATURE_NOT_ENABLED` |

A mensagem ao usuário é genérica e não revela provider, API key, configuração
interna ou motivo comercial da indisponibilidade.

## 4. Endpoint de capacidades

`GET /assistente/capacidades` deve responder para a Organization autenticada e
expor somente capacidades efetivas:

```json
{
  "habilitado": true,
  "features": [
    "AI_ASSISTANT",
    "AI_EDIT",
    "AI_REVIEW"
  ],
  "modos": [
    "Perguntar",
    "Redigir",
    "Reescrever",
    "Revisar"
  ],
  "limites": {
    "instrucaoCaracteres": 4000,
    "contextoDocumentoCaracteres": 60000,
    "contextoAdicionalCaracteres": 8000
  },
  "suportaStreaming": true,
  "suportaHistorico": false
}
```

Quando `AI_ASSISTANT` não estiver disponível, retornar `habilitado=false`, lista
de features vazia e nenhum modo utilizável. Quando somente uma capacidade
específica estiver desligada, remover seus modos e ações da resposta.

`AI_DOCUMENT_GENERATION` deve ser exposta apenas quando o fluxo do `PA-16`
existir. A flag não transforma o modo atual `Redigir` em geração integral.

## 5. Comportamento do frontend

O frontend consulta capacidades antes de renderizar entradas da IA:

- sem `AI_ASSISTANT`, não mostra botão, atalho, ação contextual nem painel;
- sem `AI_EDIT`, não mostra ações de redação, reescrita ou aplicação editável;
- sem `AI_REVIEW`, não mostra o modo de revisão;
- sem `AI_DOCUMENT_GENERATION`, não mostra a geração integral;
- falha ao carregar capacidades não presume acesso;
- uma recusa do backend invalida o cache local e atualiza a interface;
- nenhum nome de provider, chave ou instrução de configuração é mostrado ao
  usuário final.

Ocultar a interface reduz confusão, mas não é autorização. A API continua
responsável por impedir chamadas diretas.

O atalho não pode abrir um painel oculto. Para isso, as capacidades precisam ser
carregadas no workspace ou em contexto superior, e não somente depois que o
painel já foi aberto.

## 6. Kill switch e rollback

Ao desligar `AI_ENABLED`:

- novas solicitações são recusadas antes do provider;
- capacidades passam a informar indisponibilidade;
- o frontend remove as entradas após atualizar capacidades;
- streams em andamento devem ser cancelados quando a infraestrutura de
  configuração dinâmica suportar notificação;
- nenhuma alteração é aplicada automaticamente à Peça;
- histórico e conteúdo existente permanecem conforme a política do Tópico 3.

Ao desligar uma flag por Organization, o comportamento equivalente vale apenas
para aquele tenant. No mínimo, novas solicitações precisam ser bloqueadas assim
que a alteração for observada. Para o MVP, a verificação pode consultar o banco
em cada solicitação; otimização por cache só será aceita com TTL curto,
invalidação definida e testes de revogação.

Rollback não apaga dados nem desfaz texto já aplicado pelo usuário. Ele
interrompe novas utilizações da capacidade.

No MVP, a propagação para novas operações ocorre na próxima consulta ao
backend, pois o estado persistido não utiliza cache. Uma geração já em stream
não é interrompida à força; sua sugestão continua dependendo de aplicação
explícita pelo usuário. O frontend observa a revogação na próxima resposta da
API ou atualização de capacidades.

## 7. Rollout do MVP

Não haverá rollout percentual. A sequência será:

```text
1. desenvolvimento
       ↓
2. tenant interno
       ↓
3. um cliente piloto
       ↓
4. grupo de 2 a 5 clientes
       ↓
5. disponibilidade geral
```

### 1. Desenvolvimento

- flags habilitadas apenas em ambiente local/controlado;
- provider fake nos testes e chave real somente em ambiente autorizado;
- PA-11 e PA-12 ainda podem estar em construção, sem uso de cliente; o gate
  técnico do PA-09 deve permanecer válido para a configuração em execução.

### 2. Tenant interno

- Organization interna explicitamente habilitada;
- executar suíte jurídica e fluxos ponta a ponta;
- validar custo, logs, suporte e kill switch.

### 3. Cliente piloto

- uma Organization nomeada;
- aceite operacional e comunicação de limitações;
- monitoramento reforçado e canal de feedback;
- rollback ensaiado antes da liberação.

### 4. Grupo de 2 a 5 clientes

- habilitação explícita por Organization;
- revisão dos indicadores antes de adicionar cada tenant;
- nenhuma seleção aleatória ou percentual.

### 5. Disponibilidade geral

- habilitar Organizations elegíveis por processo administrativo/provisionamento;
- preservar `AI_ENABLED` como kill switch;
- manter flags específicas para planos, capacidades ainda experimentais ou
  desativação seletiva.

## 8. Gate entre etapas

Antes de avançar de uma etapa para outra:

- não existem bloqueadores de segurança ou isolamento conhecidos;
- suíte jurídica aplicável foi executada sem falha impeditiva;
- testes frontend e backend críticos passam;
- orçamento, rate limit e observabilidade estão ativos;
- logs foram inspecionados sem conteúdo proibido;
- responsável registra decisão de avançar;
- procedimento de rollback foi testado.

Uma etapa pode ser interrompida ou revertida sem habilitar a seguinte.

## 9. Situação encontrada

Já existe:

- flag global `AssistenteIa:Enabled`, desabilitada por padrão;
- validação da flag global no backend antes da geração;
- entidade, migration e repositório `OrganizationFeature`;
- reconhecimento das quatro flags e política compartilhada por capacidades e
  geração;
- endpoint de capacidades filtrando features e modos pelo tenant autenticado;
- comando operacional `set-ai-feature` para habilitar ou desabilitar uma flag
  de uma Organization;
- kill switch persistido e comando `set-ai-enabled`, sem necessidade de
  reiniciar a API;
- auditoria append-only das alterações globais e por Organization, exigindo
  operador e justificativa;
- frontend carregando capacidades no editor antes de expor botão ou atalho;
- ações e modos do painel filtrados pelas capacidades efetivas;
- revalidação das capacidades após recusa do backend e quando a janela volta a
  receber foco;
- comportamento fechado quando a consulta de capacidades falha;
- testes unitários e E2E do kill switch, permissões específicas, isolamento,
  revogação e rollback.

Ainda falta:

- interface administrativa autenticada e auditável; o comando atual não possui
  autenticação própria e é apenas uma ferramenta de operação controlada;
- execução do rollout por etapas.

A trava de configuração continua sendo lida por `IOptionsMonitor`, mas o kill
switch cotidiano é persistido e consultado em cada nova verificação. As flags
por Organization seguem o mesmo comportamento sem cache. O frontend atualiza
capacidades após recusa da API ou retorno do foco à janela. Nenhuma mensagem ao
usuário revela provider, modelo, chave ou configuração interna.

## 10. Testes obrigatórios

### Backend

- `AI_ENABLED=false` bloqueia todas as Organizations;
- ausência de `AI_ASSISTANT` bloqueia chamada direta à API;
- cada flag específica libera apenas seus modos e operações;
- flag de A nunca habilita B;
- `OrganizationId` do corpo não influencia a decisão;
- capacidades refletem exatamente as flags da identidade autenticada;
- alteração de flag é observada no prazo definido;
- negação ocorre antes de carregar contexto ou chamar provider;
- mudanças administrativas possuem auditoria;
- rollback não remove dados nem altera Peça.

### Frontend

- sem `AI_ASSISTANT`, toolbar, atalho e painel não aparecem;
- modos sem flag específica não aparecem;
- falha de capacidades mantém a funcionalidade fechada;
- 403 invalida capacidades antigas e não perde texto manual;
- nenhuma chamada de geração parte de um controle desabilitado;
- mensagem não revela provider, modelo, chave ou configuração interna.

### Rollout

- tenant interno habilitado enquanto outro tenant permanece bloqueado;
- piloto pode ser desligado sem afetar tenant interno;
- grupo de 2 a 5 Organizations mantém isolamento de flags;
- kill switch global bloqueia todos os estágios;
- não existe seleção percentual ou aleatória no MVP.

## 11. Critérios para encerrar o PA-13

- [x] `OrganizationFeature` e migration existem.
- [x] As quatro flags por Organization são reconhecidas pelo backend.
- [x] `AI_ENABLED` bloqueia novas solicitações como kill switch.
- [x] A alteração do kill switch propaga pelo mecanismo e prazo operacionais
      documentados.
- [x] Capacidades e rotas usam a mesma política de autorização.
- [x] Frontend esconde somente o que o backend também recusa.
- [x] Testes comprovam isolamento das flags entre Organizations.
- [x] Habilitação e desabilitação possuem auditoria append-only.
- [x] Revogação, rollback e kill switch possuem testes automatizados.
- [ ] Tenant interno e piloto foram executados com evidência registrada.
- [ ] Existe autorização explícita para avançar ao próximo estágio.

A estratégia aprovada, isoladamente, não encerra o ponto.
