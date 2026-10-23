# Plano de implementação — backend

> **Organização em 2026-10-23:** planejamento em espera (`standBy`).
> Consulte o [índice de pendências](../../README.md) para a prioridade atual.

> **Estado:** fatia vertical e controles técnicos principais implementados
>
> **Última atualização:** 2026-10-20
>
> **Projeto:** `src/JuridicoAI.Api`

## Base existente

O backend é uma API ASP.NET Core em .NET 10. Peças são armazenadas no
PostgreSQL como JSON TipTap/ProseMirror, usam `versao` para concorrência
otimista e são filtradas por `organizationId`. O acesso atual passa por
`PecaApplicationService`, `IPecaRepository` e `HttpAuthContext`.

O assistente deve reutilizar autenticação, escopo de Organization, tratamento
de `ProblemDetails`, configuração por Options e DI existentes. Ele não deve ser
colocado dentro de `PecaApplicationService`, pois geração de IA tem ciclo,
falhas, custos e políticas próprios.

## Estado implementado

Em 2026-10-03, estão disponíveis:

- `GET /assistente/capacidades`;
- `GET /assistente/transparencia` e confirmação versionada antes do primeiro uso;
- `POST /pecas/{pecaId}/assistente/solicitacoes` em SSE;
- kill switch global, feature flags por Organization e limites na seção
  `AssistenteIa`;
- alteração operacional do kill switch e das flags com operador, justificativa
  e trilha append-only;
- validação antecipada de Peça, Caso, seleção, contexto e operação;
- isolamento por Organization e autorização dos contextos do Caso;
- deduplicação de `clientRequestId` enquanto uma geração está em andamento;
- prompt versionado em código e saída validada por JSON Schema;
- adapter OpenAI Responses com `store: false`, timeout e cancelamento;
- contador exato de entrada pelo endpoint `responses/input_tokens`, com bloqueio
  configurável antes da geração;
- rate limit e concorrência cumulativos por usuário e Organization;
- `Retry-After` nas recusas por janela dos limitadores locais;
- `CorrelationId` validado/gerado pelo backend e devolvido no header e no SSE;
- sequência monotônica e identidade da solicitação em todos os eventos SSE;
- limite próprio de payload para a rota de IA;
- ledger técnico persistente de tokens, custo estimado, duração e estado final;
- logs técnicos sem prompt, resposta ou PII, inclusive nos caminhos de erro da
  IA; o logging automático do `HttpClient` do provider fica desabilitado;
- testes do serviço de aplicação, do contrato HTTP com o provider e da API
  ASP.NET real para isolamento, limites, resposta inválida, timeout e
  cancelamento;
- matriz de prompt injection para a separação entre instruções do sistema,
  conteúdo não confiável e operações permitidas;
- suíte real focada em segurança em
  [`avaliacoes/seguranca-ia`](../../../../avaliacoes/seguranca-ia/README.md),
  separada da baseline jurídica congelada.

O stream externo já usa os eventos do contrato. Nesta primeira fatia, o
provider acumula e valida o JSON estruturado antes de emitir `conteudo.delta`,
portanto esse evento contém o texto completo em um bloco. Extração incremental
segura do campo `texto` e bloqueio por orçamento continuam pendentes. Já há
testes HTTP com a API ASP.NET real, provider e repositórios de teste, cobrindo
SSE, isolamento, limites, timeout e cancelamento. Esses testes não comprovam
integração com bancos reais nem infraestrutura de produção. A
[conferência de 2026-10-18](../../../alreadyDone/assistente-ia/evidencias/conferencia-documental-2026-10-18.md)
não reexecutou as suítes.

## Arquitetura alvo

```text
AssistenteController
  -> AssistenteIaApplicationService
      -> IPecaRepository              valida Peça, Caso e Organization
      -> ICasoRepository              carrega somente contexto autorizado
      -> AssistenteContextBuilder     limita e delimita dados não confiáveis
      -> AssistentePromptPolicy       instruções e formato de saída
      -> IAssistenteIaProvider        fronteira com o provedor
      -> AssistenteOutputValidator    valida/normaliza resultado
      -> IAssistenteAuditSink         métricas e auditoria sem conteúdo sensível
```

Dependências apontam para abstrações da camada Application. A implementação do
SDK/HTTP do provedor fica em `Infrastructure/AI`. Nenhum tipo do SDK aparece no
controller, nos DTOs públicos ou no domínio.

## Estrutura implementada

```text
src/JuridicoAI.Api/src/
├── Application/
│   ├── Abstractions/
│   │   ├── IAssistenteIaProvider.cs
│   │   └── IAssistenteIaGovernance.cs
│   ├── DTOs/AssistenteIaDtos.cs
│   └── Services/
│       ├── AssistenteIaApplicationService.cs
│       ├── AssistenteContextBuilder.cs
│       ├── AssistentePromptPolicy.cs
│       ├── AssistenteUsageLimiter.cs
│       ├── AssistenteUsageRecorder.cs
│       ├── AssistenteFeatureAccessService.cs
│       └── AssistenteTransparencyService.cs
├── Controllers/Assistente/AssistenteController.cs
└── Infrastructure/
    ├── AI/OpenAiAssistenteIaProvider.cs
    └── Settings/AssistenteIaSettings.cs
```

A validação de saída está em `AssistenteIaApplicationService.ValidateProviderResult`;
não existe classe separada `AssistenteOutputValidator`. Auditoria de uso é feita
por `IAssistenteUsageRecorder`/`AssistenteUsageRecorder`, com persistência no
ledger. A arquitetura alvo acima mantém as responsabilidades conceituais.

## Responsabilidades

### Controller

- exigir autenticação;
- expor capacidades e limites não sensíveis para o frontend;
- aceitar o contrato descrito em [contrato-api.md](contrato-api.md);
- criar a resposta `text/event-stream` e desabilitar buffering quando preciso;
- serializar somente eventos internos conhecidos;
- propagar `HttpContext.RequestAborted`;
- não montar prompts e não acessar SDK do provedor.

### Serviço de aplicação

- validar modo, operação, seleção e limites;
- obter a Peça no escopo da Organization e confirmar o Caso relacionado;
- autorizar cada `contextoCasoId` no mesmo Caso;
- montar a entrada canônica;
- aplicar timeout e orçamento;
- orquestrar stream, validação final e auditoria;
- nunca chamar uma operação de atualização da Peça.

### Construtor de contexto

O builder produz blocos com origem explícita, por exemplo:

```text
[INSTRUCAO_DO_USUARIO]
...

[TEXTO_DA_PECA escopo="Selecao" dado_nao_confiavel="true"]
...

[CONTEXTO_DO_CASO id="..."]
...
```

Conteúdo delimitado não pode modificar prompt de sistema. Os blocos são
ordenados, truncados de forma determinística e contabilizados antes do envio.
Dados não autorizados ou fora do limite causam erro; não devem ser adicionados
silenciosamente.

### Provider

A abstração atual retorna um resultado completo validado, sem expor SDK:

```csharp
public interface IAssistenteIaProvider
{
    Task<AssistenteIaProviderResult> GerarAsync(
        AssistenteIaProviderRequest request,
        CancellationToken cancellationToken);
}
```

O adapter OpenAI consome o stream do provider internamente e acumula a saída
estruturada. O controller publica `conteudo.delta` com o texto completo após a
validação do resultado. Contagem de entrada usa a abstração separada
`IAssistenteTokenCounter`. Timeout, descarte HTTP e cancelamento existem; retry
automático e extração incremental segura ainda estão pendentes.

### Validador de saída

- aceita somente operações previstas no contrato;
- restringe `Perguntar` e `Revisar` a `ResponderNoPainel`, rejeitando operações
  incompatíveis;
- limita título, texto, resumo e quantidade de alertas;
- valida categoria, título e descrição de cada achado do modo `Revisar`;
- rejeita saída estrutural inválida; o texto não passa por remoção de HTML no
  backend, e o frontend o converte em nós de texto TipTap sem interpretar tags;
- garante que exista um evento final completo;
- valida estrutura e presença dos campos de alertas; a identificação semântica
  de referências sem fonte depende do prompt/modelo e da avaliação, sem detector
  determinístico adicional no backend;
- trata saída inválida como `AI_PROVIDER_ERROR`, sem aplicar fallback inseguro.

## Prompt e versionamento

O prompt de sistema é código versionado em:

```text
src/JuridicoAI.Api/src/Application/Services/AssistentePromptPolicy.cs
```

A versão atual é `assistente-juridico-v3`. Ela explicita que o modelo não tem
pesquisa externa, proíbe referência jurisprudencial concreta sem fonte, exige
`REFERENCIA_NAO_VERIFICADA` para legislação não fornecida e impede inferir a
natureza de objetos ou ampliar a ausência de uma prova para outros documentos.
A política completa está no
[Tópico 10](../decisoes/pa-10-legislacao-jurisprudencia-fontes.md).

Cada solicitação registra ao menos `promptVersion`, provider lógico, modelo,
latência, contagem de entrada/saída, estado final e IDs técnicos. Alterar regras
de segurança exige nova versão e casos de avaliação. Prompts nunca ficam no
frontend.

O gate mínimo para mudanças de prompt ou modelo está definido no
[Tópico 9 — Avaliação de qualidade jurídica](../../../alreadyDone/assistente-ia/decisoes/pa-09-avaliacao-qualidade-juridica.md).
A suíte de 30 casos e seu executor já existem em
[`avaliacoes/qualidade-juridica`](../../../../avaliacoes/qualidade-juridica/README.md).
A baseline técnica `1.0.3`, no commit `f6376df`, encerrou o `PA-09` para a
configuração congelada do MVP. Qualquer mudança no modelo, prompt, suíte ou
avaliador cria uma nova configuração candidata e exige nova execução do gate.
A homologação jurídica formal e a autorização comercial permanecem bloqueadas
por `PA-17`.

O formato final deve ser estruturado pelo recurso nativo do provedor quando
disponível. Mesmo assim, o backend revalida tudo; esquema do provedor não é uma
fronteira de confiança.

## Configuração

O alvo aprovado para modelo, tokens, orçamento, concorrência e retry está no
[Tópico 1 — Modelo e operação](../decisoes/pa-01-modelo-e-operacao.md). A configuração
abaixo representa a primeira fatia atualmente implementada e ainda será
migrada para os perfis definidos naquele documento.

Retenção, histórico, exclusão, minimização de dados e transparência seguem o
[Tópico 3 — Retenção, exclusão e LGPD](../decisoes/pa-03-retencao-exclusao-lgpd.md).

Configuração atual, com segredos reais fora do repositório:

```json
{
  "AssistenteIa": {
    "Enabled": false,
    "Provider": "OpenAI",
    "Model": "",
    "ApiKey": "",
    "BaseUrl": "https://api.openai.com/v1/",
    "TimeoutSeconds": 90,
    "MaxInstructionCharacters": 4000,
    "MaxDocumentContextCharacters": 60000,
    "MaxAdditionalContextCharacters": 8000,
    "MaxTotalContextCharacters": 80000,
    "MaxInputTokens": 60000,
    "MaxOutputCharacters": 20000,
    "MaxOutputTokens": 6000,
    "MaxRequestBodyBytes": 524288,
    "RequestsPerUserPerMinute": 10,
    "ConcurrentRequestsPerUser": 2,
    "RequestsPerOrganizationPerMinute": 30,
    "ConcurrentRequestsPerOrganization": 6,
    "InputCostPerMillionTokensUsd": 0,
    "CachedInputCostPerMillionTokensUsd": 0,
    "OutputCostPerMillionTokensUsd": 0,
    "PricingVersion": "unconfigured"
  }
}
```

Os valores são configuráveis. Alterações de limites aplicam-se a novas
partições após reinício; para previsibilidade operacional, alterar esses
valores com restart controlado da aplicação.

As tarifas começam em zero e o custo fica `null`, sem fabricar uma estimativa.
Antes do tenant interno, preencher as três tarifas por milhão de tokens e uma
`PricingVersion` identificável, conforme o preço vigente do modelo contratado.

`ApiKey` deve vir de user-secrets no desenvolvimento e de secret manager ou
variável de ambiente no deploy. Ela nunca é devolvida ao cliente ou registrada.
`Enabled=false` deve responder `AI_UNAVAILABLE` sem tentar acessar o provedor.

Esse `Enabled` é a trava de bootstrap do kill switch global. Quando estiver
`false`, nenhum estado do banco pode habilitar a IA. Com ele em `true`, o estado
operacional `AI_ENABLED` é consultado no banco a cada nova verificação e pode
ser alterado sem reiniciar a API pelo comando `set-ai-enabled`.

O rollout por Organization usa `OrganizationFeature` e falha fechado. As
operações `set-ai-enabled` e `set-ai-feature` exigem
`AI_OPERATION_UPDATED_BY_ID` e `AI_OPERATION_REASON`. Alterações globais e por
Organization são registradas de forma append-only em
`ai_feature_change_audit`, sem conteúdo jurídico. O estado global corrente fica
em `ai_operational_controls`; na ausência inicial desse registro, a trava de
bootstrap prevalece e o estado operacional é considerado habilitado.

Para flags de Organization, o operador precisa ser `Owner` do tenant. Para o
controle global, precisa ser um usuário ativo e também possuir acesso ao
ambiente operacional. Uma futura interface administrativa deverá substituir a
execução manual por um fluxo autenticado próprio.

## Persistência e auditoria

Estado atualmente implementado:

- não exige persistir conversa ou conteúdo integral;
- produz logs estruturados com IDs, estado, duração e uso;
- não registra instrução, contexto, resposta, token de autenticação ou PII;
- permite correlacionar `clientRequestId`, `solicitacaoId`, request trace,
  Organization, usuário e Peça por identificadores.
- persiste `ai_usage_ledger` sem conteúdo jurídico, incluindo tokens de entrada,
  cache e saída, preço versionado e custo estimado.
- persiste o estado corrente do kill switch e um histórico imutável de
  alterações de flags, com operador, data e justificativa técnica.

O alvo de métricas, ledger de custo, eventos de produto e `CorrelationId`
ponta a ponta está no
[Tópico 14 — Observabilidade e métricas](../decisoes/pa-14-observabilidade-metricas.md).
O estado atual ainda não possui dashboards, alertas ou exportador de métricas.

O histórico persistente aprovado no Tópico 3 será implementado em entidades
próprias; mensagens não entram no JSON da Peça. A migration deve definir
retenção e índices por Organization/Caso/Peça/data. Conteúdo persistido deve ter
autorização, exclusão e política de proteção equivalentes às da Peça.

“Aceita” e “rejeitada” não podem ser inferidas apenas porque uma Peça foi salva.
Uma futura coleta dessa decisão deve usar endpoint explícito. Métricas agregadas
não devem permitir reconstruir conteúdo jurídico.

## Rate limit, custos e resiliência

- 10 solicitações/minuto e 2 simultâneas por usuário;
- 30 solicitações/minuto e 6 simultâneas por Organization;
- partições cumulativas por usuário e Organization, além do endpoint de auth;
- limite de caracteres antes do cálculo/estimativa de tokens;
- timeout total e cancelamento propagado;
- circuit breaker somente se compatível com o adapter escolhido;
- sem retry depois do primeiro delta;
- uso e custo lógico registrados para orçamento;
- mensagens amigáveis para 429, timeout e indisponibilidade.

Não fazer fallback silencioso para modelo com política ou capacidade diferente.
O alvo aprovado do MVP não prevê fallback automático de modelo.

## Segurança e LGPD

A política completa e seus critérios de comprovação estão no
[Tópico 11 — Segurança, isolamento e logs](../decisoes/pa-11-seguranca-isolamento-logs.md).

- consultar Peça e Contextos sempre pelo escopo fornecido por `IAuthContext`;
- não aceitar `organizationId`, `casoId` ou `userId` como autoridade no corpo;
- revalidar que todos os contextos pertencem ao Caso da Peça;
- enviar ao provedor somente campos autorizados pela requisição;
- mascarar dados que não forem necessários quando `incluirDadosCaso=true`;
- configurar, quando suportado, não retenção/não treinamento no provedor;
- proteger contra prompt injection delimitando todo conteúdo externo;
- nunca executar ferramenta, URL ou instrução embutida no texto da Peça;
- não permitir que uma resposta do modelo selecione novos dados do banco;
- estabelecer contrato de operador/suboperador antes do uso em produção.

## Testes mínimos

### Unidade

- combinações válidas e inválidas de modo/operação/escopo;
- seleção obrigatória para `Reescrever`;
- limites e truncamento determinístico;
- builder não mistura instrução de sistema com conteúdo do usuário;
- validação de saída e alertas;
- mapeamento de falhas do provider;
- cancelamento interrompe enumeração;
- geração nunca invoca escrita no repositório de Peças.

### Integração

- 401 sem token;
- capacidades refletem feature flag, modos e limites configurados;
- 404 para Peça de outra Organization;
- contexto de outro Caso/Organization é rejeitado;
- sequência SSE válida e evento final único;
- cancelamento encerra chamada simulada do provider;
- rate limit retorna 429;
- corpo excessivo retorna 413;
- erro anterior ao stream usa ProblemDetails;
- erro posterior ao início usa `solicitacao.falhou`.

### Avaliação de comportamento

Manter casos sintéticos versionados, sem dados reais, cobrindo:

- pedido que tenta induzir criação de fato;
- peça contendo instrução maliciosa para o modelo;
- pedido de jurisprudência sem fonte;
- reescrita que precisa preservar sentido;
- análise que deve reconhecer informação ausente;
- texto longo próximo aos limites.

## Sequência recomendada

- [x] Criar Settings, validação e feature flag.
- [x] Criar DTOs/enums alinhados ao contrato.
- [x] Criar provider fake e testes do serviço sem rede.
- [x] Implementar autorização e builder de contexto.
- [x] Implementar policy e validador de saída.
- [x] Ampliar avaliações sintéticas de comportamento e prompt injection.
- [x] Implementar controller SSE.
- [x] Adicionar testes HTTP do controller e protocolo SSE com dependências simuladas.
- [ ] Comprovar integração com bancos e infraestrutura reais no gate de produção.
- [x] Adicionar adapter OpenAI Responses.
- [x] Adicionar rate limit e concorrência por usuário e Organization.
- [x] Adicionar feature flags por Organization e kill switch global.
- [x] Adicionar `CorrelationId`, limite de payload e ledger inicial de custo.
- [ ] Adicionar bloqueio e alertas de orçamento por Organization.
- [x] Implementar contagem exata de entrada e concorrência do
      [Tópico 1](../decisoes/pa-01-modelo-e-operacao.md).
- [ ] Implementar perfis de saída e retry do Tópico 1.
- [x] Implementar o aviso versionado, a confirmação persistida e o bloqueio de
      primeiro uso definidos no [Tópico 3](../decisoes/pa-03-retencao-exclusao-lgpd.md).
- [ ] Implementar histórico, exclusão, retenção e as próximas camadas de
      minimização definidas no Tópico 3.
- [x] Atualizar Swagger, `.env.example`, user-secrets e documentação operacional.
- [x] Reconciliar critérios correspondentes no documento principal com código e
      evidências registradas em 2026-10-18.
