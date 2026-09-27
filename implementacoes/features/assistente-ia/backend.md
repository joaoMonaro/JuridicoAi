# Plano de implementação — backend

> **Estado:** primeira fatia vertical implementada
>
> **Última atualização:** 2026-09-26
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

Em 2026-09-26, estão disponíveis:

- `GET /assistente/capacidades`;
- `POST /pecas/{pecaId}/assistente/solicitacoes` em SSE;
- feature flag e limites na seção `AssistenteIa`;
- validação antecipada de Peça, Caso, seleção, contexto e operação;
- isolamento por Organization e autorização dos contextos do Caso;
- deduplicação de `clientRequestId` enquanto uma geração está em andamento;
- prompt versionado em código e saída validada por JSON Schema;
- adapter OpenAI Responses com `store: false`, timeout e cancelamento;
- rate limit por usuário, particionado dentro da Organization;
- logs técnicos sem prompt, resposta ou PII;
- testes do serviço de aplicação e do contrato HTTP com o provider.

O stream externo já usa os eventos do contrato. Nesta primeira fatia, o
provider acumula e valida o JSON estruturado antes de emitir `conteudo.delta`,
portanto esse evento contém o texto completo em um bloco. Extração incremental
segura do campo `texto`, orçamento por Organization e testes HTTP end-to-end
continuam pendentes.

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

## Estrutura proposta

```text
src/JuridicoAI.Api/src/
├── Application/
│   ├── Abstractions/
│   │   ├── IAssistenteIaProvider.cs
│   │   └── IAssistenteAuditSink.cs
│   ├── DTOs/AssistenteIaDtos.cs
│   └── Services/
│       ├── AssistenteIaApplicationService.cs
│       ├── AssistenteContextBuilder.cs
│       ├── AssistenteOutputValidator.cs
│       └── AssistentePromptPolicy.cs
├── Controllers/Assistente/AssistenteController.cs
└── Infrastructure/
    ├── AI/<Provider>AssistenteIaProvider.cs
    └── Settings/AssistenteIaSettings.cs
```

O nome concreto do provider só será definido depois da decisão registrada no
documento principal.

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

A abstração deve representar a capacidade necessária, e não um SDK específico:

```csharp
public interface IAssistenteIaProvider
{
    IAsyncEnumerable<AssistenteIaChunk> GerarAsync(
        AssistenteIaInput input,
        CancellationToken cancellationToken);
}
```

O adapter concreto traduz mensagens, streaming, uso de tokens, finish reason e
erros. Deve possuir timeout, descarte correto da resposta HTTP e tratamento de
cancelamento. Retry automático só é permitido antes de qualquer delta ser
entregue, para evitar texto duplicado.

### Validador de saída

- aceita somente operações previstas no contrato;
- força `ResponderNoPainel` para `Perguntar` e `Revisar`;
- limita título, texto, resumo e quantidade de alertas;
- valida categoria, título e descrição de cada achado do modo `Revisar`;
- descarta HTML e conteúdo estrutural inválido;
- garante que exista um evento final completo;
- acrescenta alerta quando o resultado contiver referências que não foram
  respaldadas por uma fonte fornecida;
- trata saída inválida como `AI_PROVIDER_ERROR`, sem aplicar fallback inseguro.

## Prompt e versionamento

O prompt de sistema é código versionado. Sugestão de localização:

```text
src/JuridicoAI.Api/src/Application/AI/Prompts/assistente-juridico-v1.md
```

Cada solicitação registra ao menos `promptVersion`, provider lógico, modelo,
latência, contagem de entrada/saída, estado final e IDs técnicos. Alterar regras
de segurança exige nova versão e casos de avaliação. Prompts nunca ficam no
frontend.

O formato final deve ser estruturado pelo recurso nativo do provedor quando
disponível. Mesmo assim, o backend revalida tudo; esquema do provedor não é uma
fronteira de confiança.

## Configuração

Configuração proposta, com valores reais fora do repositório:

```json
{
  "AssistenteIa": {
    "Enabled": false,
    "Provider": "",
    "Model": "",
    "ApiKey": "",
    "BaseUrl": null,
    "TimeoutSeconds": 90,
    "MaxInstructionCharacters": 4000,
    "MaxDocumentContextCharacters": 60000,
    "MaxAdditionalContextCharacters": 8000,
    "MaxOutputCharacters": 20000,
    "RequestsPerUserPerMinute": 6,
    "RequestsPerOrganizationPerMinute": 30
  }
}
```

`ApiKey` deve vir de user-secrets no desenvolvimento e de secret manager ou
variável de ambiente no deploy. Ela nunca é devolvida ao cliente ou registrada.
`Enabled=false` deve responder `AI_UNAVAILABLE` sem tentar acessar o provedor.

## Persistência e auditoria

Primeira fatia:

- não exige persistir conversa ou conteúdo integral;
- produz logs estruturados com IDs, estado, duração, tamanhos e uso;
- não registra instrução, contexto, resposta, token de autenticação ou PII;
- permite correlacionar `clientRequestId`, `solicitacaoId`, request trace,
  Organization, usuário e Peça por identificadores.

Se for necessário persistir histórico, criar entidades próprias; não incorporar
mensagens ao JSON da Peça. A migration deve definir retenção e índices por
Organization/Peça/data. Conteúdo persistido deve ter autorização, exclusão e
política de proteção equivalentes às da Peça.

“Aceita” e “rejeitada” não podem ser inferidas apenas porque uma Peça foi salva.
Uma futura coleta dessa decisão deve usar endpoint explícito. Métricas agregadas
não devem permitir reconstruir conteúdo jurídico.

## Rate limit, custos e resiliência

- partições por usuário e Organization, além do limite do endpoint de auth;
- limite de solicitações simultâneas para impedir esgotamento de conexões;
- limite de caracteres antes do cálculo/estimativa de tokens;
- timeout total e cancelamento propagado;
- circuit breaker somente se compatível com o adapter escolhido;
- sem retry depois do primeiro delta;
- uso e custo lógico registrados para orçamento;
- mensagens amigáveis para 429, timeout e indisponibilidade.

Não fazer fallback silencioso para modelo com política ou capacidade diferente.
Quando existir fallback, ele precisa ser configurado, observável e indicado no
evento final.

## Segurança e LGPD

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
- [ ] Ampliar avaliações sintéticas de comportamento e prompt injection.
- [x] Implementar controller SSE.
- [ ] Adicionar testes HTTP end-to-end do controller e protocolo SSE.
- [x] Adicionar adapter OpenAI Responses.
- [x] Adicionar rate limit por usuário.
- [ ] Adicionar orçamento e limite agregado por Organization.
- [x] Atualizar Swagger, `.env.example`, user-secrets e documentação operacional.
- [ ] Marcar critérios correspondentes no documento principal.
