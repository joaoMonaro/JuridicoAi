# Contrato HTTP do assistente de IA

> **Estado:** primeira fatia do backend implementada
>
> **Última atualização:** 2026-10-01
>
> Capacidades, geração, validação e eventos SSE estão implementados. O delta de
> conteúdo ainda é emitido como um único bloco após a validação da saída
> estruturada; streaming incremental do campo `texto` permanece pendente.

> **Redesign conversacional:** este contrato permanece vigente durante as
> primeiras etapas do PA-18. Pedido livre com intenção automática deverá ser
> especificado como evolução compatível antes da implementação da etapa E4; o
> frontend não inferirá operações por palavras-chave frágeis.

Este documento é o contrato compartilhado entre frontend e backend. Nomes e
exemplos devem ser atualizados antes do código quando houver uma mudança
incompatível.

## Capacidades

```http
GET /assistente/capacidades
Authorization: Bearer <token>
```

Resposta proposta:

```json
{
  "habilitado": true,
  "features": ["AI_ASSISTANT", "AI_EDIT", "AI_REVIEW"],
  "modos": ["Perguntar", "Redigir", "Reescrever", "Revisar"],
  "limites": {
    "instrucaoCaracteres": 4000,
    "contextoDocumentoCaracteres": 60000,
    "contextoAdicionalCaracteres": 8000
  },
  "suportaStreaming": true,
  "suportaHistorico": false
}
```

O frontend usa essa resposta para exibir a entrada da feature, aplicar limites
antes do envio e não prometer histórico indisponível. O endpoint não revela
chave, prompt de sistema, custo, nome interno de deployment ou configuração
sensível. Uma indisponibilidade temporária durante a geração continua sendo
tratada pelo endpoint de solicitação.

`features` contém as flags efetivas da Organization autenticada e `modos` é
filtrado pelas mesmas regras usadas pela rota de geração. Sem `AI_ASSISTANT`,
`habilitado` é `false` e nenhum modo fica disponível. Consulte o
[Tópico 13 — Feature flags e rollout](../decisoes/pa-13-feature-flags-rollout.md).

## Transparência antes do primeiro uso

```http
GET /assistente/transparencia
Authorization: Bearer <token>
```

Resposta quando o usuário ainda não confirmou a versão vigente na Organization
autenticada:

```json
{
  "versao": "ai-transparency-v1",
  "confirmado": false,
  "confirmadoEm": null,
  "titulo": "Uso de inteligência artificial",
  "mensagem": "Algumas informações do caso poderão ser processadas...",
  "avisoDeRevisao": "As respostas geradas por inteligência artificial podem conter erros...",
  "politicaDePrivacidadeUrl": null
}
```

Para registrar que o aviso vigente foi apresentado:

```http
POST /assistente/transparencia/confirmacoes
Authorization: Bearer <token>
Content-Type: application/json

{"versao":"ai-transparency-v1"}
```

O registro é idempotente e usa sempre `UserId` e `OrganizationId` obtidos da
identidade autenticada. Se a versão enviada já não for a atual, a API responde
`409 AI_TRANSPARENCY_NOTICE_OUTDATED`; o frontend deve buscar e apresentar o
novo aviso. Uma confirmação anterior não vale para uma nova versão, outro
usuário ou outra Organization.

Sem confirmação da versão atual, o endpoint de geração responde
`428 AI_TRANSPARENCY_ACK_REQUIRED` antes de contar tokens, adquirir concorrência
ou chamar o provider. Esse registro comprova a apresentação do aviso e não é
tratado como consentimento jurídico geral.

## Endpoint de geração

```http
POST /pecas/{pecaId}/assistente/solicitacoes
Authorization: Bearer <token>
X-Correlation-ID: <uuid>
Content-Type: application/json
Accept: text/event-stream
```

O endpoint valida que a Peça pertence à Organization autenticada e apenas gera
uma resposta. Ele não atualiza a Peça.

Além da autorização e das feature flags, a geração exige a confirmação descrita
na seção anterior.

O corpo não aceita `organizationId` como autoridade. O tenant é obtido da
identidade, sessão e membership validadas pelo backend; `pecaId` e todos os IDs
de contexto são consultados dentro desse tenant. Uma tentativa de usar recurso
de outra Organization retorna 404/403 sem conteúdo e sem chamar o provider. Ver
[Tópico 11 — Segurança, isolamento e logs](../decisoes/pa-11-seguranca-isolamento-logs.md).

O backend valida ou gera `X-Correlation-ID`, devolve-o no header da resposta e
no evento `solicitacao.iniciada`. O ID é somente para rastreio e nunca participa
da autorização. A geração do ID no frontend ainda está pendente. Consulte o
[Tópico 14 — Observabilidade e métricas](../decisoes/pa-14-observabilidade-metricas.md).

### Requisição

```json
{
  "clientRequestId": "f79cd150-fca4-491e-9018-7f3a7f177af4",
  "modo": "Redigir",
  "instrucao": "Crie um tópico sobre tutela de urgência.",
  "versaoPeca": 7,
  "operacaoPreferida": "InserirNoCursor",
  "contexto": {
    "documento": {
      "escopo": "SecaoAtual",
      "texto": "DA TUTELA DE URGÊNCIA\nA conta bancária permanece bloqueada...",
      "tituloSecao": "DA TUTELA DE URGÊNCIA",
      "selecao": null,
      "cursor": {
        "posicao": 312
      },
      "revisaoEditor": 18
    },
    "contextoAdicional": "A autora recebe aposentadoria exclusivamente nessa conta.",
    "incluirDadosCaso": true,
    "contextoCasoIds": [
      "28eb8e82-3eac-4b84-ab75-cd95be8ce106"
    ]
  }
}
```

Para uma seleção:

```json
{
  "documento": {
    "escopo": "Selecao",
    "texto": "Texto selecionado e enviado ao modelo.",
    "tituloSecao": "DOS FATOS",
    "selecao": {
      "from": 120,
      "to": 198,
      "textoOriginal": "Texto selecionado e enviado ao modelo."
    },
    "cursor": null,
    "revisaoEditor": 21
  }
}
```

`from`, `to`, `posicao` e `revisaoEditor` são âncoras opacas devolvidas na
sugestão. O backend não as interpreta para escrever no documento. O frontend
as usa para detectar se a revisão base ainda é válida.

### Campos

| Campo | Regra |
|---|---|
| `clientRequestId` | UUID criado pelo cliente; permite rastrear tentativas sem duplicar uma geração concluída. |
| `modo` | `Perguntar`, `Redigir`, `Reescrever` ou `Revisar`. |
| `instrucao` | Obrigatória, após `trim`; limite inicial proposto de 4.000 caracteres. |
| `versaoPeca` | Versão conhecida pelo frontend no início da solicitação; usada para diagnóstico, não para gravar a Peça. |
| `operacaoPreferida` | Opcional e precisa ser compatível com o modo. |
| `documento.escopo` | `Nenhum`, `Selecao`, `SecaoAtual` ou `DocumentoCompleto`. |
| `documento.texto` | Snapshot em texto do escopo escolhido; limite inicial proposto de 60.000 caracteres. |
| `contextoAdicional` | Informação temporária fornecida pelo usuário; limite inicial proposto de 8.000 caracteres. |
| `incluirDadosCaso` | Autoriza o backend a montar um resumo estruturado do Caso e envolvidos. |
| `contextoCasoIds` | Somente contextos textuais explicitamente escolhidos e pertencentes ao mesmo Caso. |

O backend deve rejeitar:

- `Reescrever` sem seleção não vazia;
- `SubstituirSelecao` fora do modo `Reescrever`;
- IDs de contexto que não pertençam ao Caso da Peça;
- corpo acima dos limites configurados;
- instrução vazia ou operação incompatível.

## Streaming SSE

O cliente usa `fetch`, pois `EventSource` não suporta este `POST` com corpo e
Authorization. A resposta usa UTF-8 e eventos SSE separados por uma linha em
branco.

### `solicitacao.iniciada`

```text
id: 9bdfaf6b-6c57-43ef-86ee-65b614dc9051:1
event: solicitacao.iniciada
data: {"solicitacaoId":"9bdfaf6b-6c57-43ef-86ee-65b614dc9051","clientRequestId":"f79cd150-fca4-491e-9018-7f3a7f177af4","correlationId":"15e86af9-a82f-4890-a1d4-99c7d81e0d71"}
```

### `conteudo.delta`

Pode ocorrer várias vezes. Serve apenas para apresentação progressiva; o texto
canônico é o evento final.

```text
id: 9bdfaf6b-6c57-43ef-86ee-65b614dc9051:2
event: conteudo.delta
data: {"delta":"A tutela de urgência é cabível quando "}
```

### `solicitacao.concluida`

```text
id: 9bdfaf6b-6c57-43ef-86ee-65b614dc9051:3
event: solicitacao.concluida
data: {"sugestao":{"id":"0c31dd95-fcc7-44b4-ab86-28f04fe6dd90","modo":"Redigir","operacaoSugerida":"InserirNoCursor","titulo":"Da tutela de urgência","texto":"Diante da probabilidade do direito...","resumo":"Estrutura os requisitos e relaciona o bloqueio ao perigo de dano.","alertas":[{"codigo":"REFERENCIA_NAO_VERIFICADA","mensagem":"Confirme os dispositivos legais antes do protocolo."}],"ancora":{"from":null,"to":null,"posicao":312,"revisaoEditor":18,"textoOriginal":null}},"uso":{"entradaTokens":1420,"saidaTokens":380},"modelo":"configurado-no-servidor"}
```

`texto` é a fonte canônica inicial. No MVP, parágrafos são separados por uma
linha vazia e convertidos pelo frontend em nós TipTap válidos. HTML retornado
pelo provedor nunca é injetado diretamente no DOM ou no editor.

`REFERENCIA_NAO_VERIFICADA` indica que a referência não estava nas fontes
fornecidas e precisa ser conferida oficialmente. Esse alerta nunca transforma
jurisprudência concreta inventada em conteúdo aceitável. O endpoint não aciona
pesquisa externa ou scraper; consulte o
[Tópico 10](../decisoes/pa-10-legislacao-jurisprudencia-fontes.md).

No modo `Revisar`, a sugestão também contém `achados` estruturados, sem exigir
que o frontend interprete bullets produzidos livremente:

```json
{
  "achados": [
    {
      "id": "a1",
      "categoria": "Lacuna",
      "titulo": "Relacionar o bloqueio ao perigo de dano",
      "descricao": "O texto relata o bloqueio, mas ainda não explica seu impacto imediato sobre a subsistência.",
      "alertas": []
    }
  ]
}
```

Categorias iniciais: `Lacuna`, `Contradicao`, `Clareza`, `Estrutura` e
`Verificacao`. “Desenvolver” cria uma nova solicitação `Redigir`: o frontend
inclui título e descrição do achado no contexto adicional e volta a enviar os
escopos explicitamente escolhidos. Um achado nunca é inserido automaticamente.

### `solicitacao.falhou`

Depois que o stream começou, erros são enviados no próprio stream:

```text
id: 9bdfaf6b-6c57-43ef-86ee-65b614dc9051:2
event: solicitacao.falhou
data: {"code":"AI_PROVIDER_TIMEOUT","message":"A IA demorou mais que o esperado. Tente novamente.","retryable":true,"requestId":"00-a1b2"}
```

O stream termina após `solicitacao.concluida`, `solicitacao.falhou` ou
cancelamento da conexão. O frontend ignora qualquer sugestão sem evento final.

Todos os eventos do produto possuem o campo SSE padrão `id` no formato:

```text
<solicitacaoId>:<sequencia>
```

A sequência começa em `1` no evento `solicitacao.iniciada` e cresce sem lacunas.
Se não houver delta, o terminal recebe a sequência `2`. O frontend:

- ignora retransmissões da mesma sequência e eventos antigos já superados;
- aceita texto idêntico em sequências diferentes, pois pode ser repetição
  legítima do conteúdo;
- rejeita ausência de ID, lacuna de sequência ou mistura de IDs de solicitação
  como `AI_STREAM_INVALID`;
- ignora qualquer evento posterior ao primeiro terminal.

Comparar somente o texto do delta não é uma estratégia válida de deduplicação.
O marcador `[DONE]`, se recebido, é ignorado para compatibilidade e não
substitui `solicitacao.concluida` ou `solicitacao.falhou`. Reconexão automática
e retomada por `Last-Event-ID` permanecem fora do MVP.

## Operações

| Operação | Pré-condição de aplicação no frontend |
|---|---|
| `ResponderNoPainel` | nunca oferece escrita no editor automaticamente |
| `InserirNoCursor` | cursor de destino confirmado; revisão base ainda válida ou novo destino escolhido explicitamente |
| `InserirAposSelecao` | seleção original ainda corresponde ao snapshot |
| `SubstituirSelecao` | `from`, `to`, revisão e texto original ainda correspondem |
| `CriarSecao` | destino da nova seção escolhido e mostrado ao usuário |

Além da pré-condição específica da operação, toda escrita no editor exige
autosave em `SALVO`. `SALVANDO`, `NAO_SALVO`, `ERRO` e `CONFLITO` bloqueiam a
aplicação da sugestão, mas não a leitura, cópia ou geração da resposta. Consulte
[Tópico 8 — Aplicação com autosave em erro](../decisoes/pa-08-autosave-em-erro.md).

Quando a âncora expirar, o frontend deve bloquear a aplicação da sugestão e
oferecer: selecionar um novo trecho, gerar novamente ou descartar. A resposta
antiga nunca deve ser aplicada à nova seleção. “Inserir no cursor atual” não é
um fallback do MVP; se essa opção for criada futuramente, deverá iniciar uma
nova operação de inserção explícita, sem reaproveitar a alteração original.

Consulte [Tópico 7 — Âncora expirada e destino alternativo](../decisoes/pa-07-ancora-expirada.md).

## Erros HTTP antes do stream

Erros anteriores ao primeiro byte seguem `application/problem+json` e o padrão
atual do middleware da API.

| HTTP | `code` | Situação |
|---|---|---|
| 400 | `AI_INVALID_REQUEST` | modo, contexto ou operação inválidos |
| 401 | padrão de autenticação | sessão ausente ou inválida |
| 403 | `AI_FEATURE_NOT_ENABLED` | feature indisponível para a Organization |
| 404 | `PECA_NOT_FOUND` | Peça inexistente para a Organization |
| 409 | `AI_REQUEST_ALREADY_RUNNING` | mesmo `clientRequestId` ainda em andamento |
| 413 | `AI_CONTEXT_TOO_LARGE` | um campo de contexto ultrapassa seu limite |
| 413 | `AI_INPUT_TOO_LARGE` | o payload completo ultrapassa 60.000 tokens |
| 413 | `AI_REQUEST_TOO_LARGE` | corpo HTTP ultrapassa o limite configurado |
| 422 | `AI_CONTEXT_INCOMPATIBLE` | seleção/modo não formam uma solicitação válida |
| 429 | `AI_USER_RATE_LIMITED` | taxa por usuário excedida |
| 429 | `AI_USER_CONCURRENCY_LIMITED` | concorrência por usuário excedida |
| 429 | `AI_ORGANIZATION_RATE_LIMITED` | taxa da Organization excedida |
| 429 | `AI_ORGANIZATION_CONCURRENCY_LIMITED` | concorrência da Organization excedida |
| 429 | `AI_RATE_LIMITED` | limite do provedor |
| 502 | `AI_PROVIDER_ERROR` | resposta inválida ou falha não transitória do provedor |
| 503 | `AI_UNAVAILABLE` | feature desabilitada ou provedor indisponível |
| 504 | `AI_PROVIDER_TIMEOUT` | tempo máximo excedido |

Respostas visíveis ao usuário não expõem corpo do provedor, prompt de sistema,
stack trace, chave, modelo interno sensível ou detalhes de outra Organization.

## Cancelamento e idempotência

- Fechar o painel, trocar de Peça ou clicar em cancelar aborta o `fetch`.
- O `CancellationToken` da requisição é propagado até o provedor.
- Uma repetição com `clientRequestId` concluído retorna a sugestão já concluída
  se houver persistência habilitada; sem persistência, retorna conflito e o
  cliente cria um novo identificador para “Tentar novamente”.
- “Tentar novamente” representa uma nova geração e recebe outro
  `clientRequestId`.

## Futuras rotas, não pertencentes à primeira fatia

Histórico persistente, decisão de aceitar/rejeitar e conversas podem adicionar:

```text
GET  /pecas/{pecaId}/assistente/conversas
GET  /pecas/{pecaId}/assistente/conversas/{conversaId}
POST /pecas/{pecaId}/assistente/sugestoes/{sugestaoId}/decisao
POST /pecas/{pecaId}/assistente/eventos
```

Essas rotas não devem bloquear o endpoint vertical de geração. Quando forem
adotadas, seus contratos passam a integrar este documento. A rota de eventos é
autenticada, deriva a Organization do backend, aceita somente enum e IDs
técnicos, deduplica por `eventId` e nunca recebe conteúdo da Peça ou sugestão.
