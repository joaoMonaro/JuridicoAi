# Contrato HTTP do assistente de IA

> **Estado:** primeira fatia do backend implementada
>
> **Última atualização:** 2026-09-26
>
> Capacidades, geração, validação e eventos SSE estão implementados. O delta de
> conteúdo ainda é emitido como um único bloco após a validação da saída
> estruturada; streaming incremental do campo `texto` permanece pendente.

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

## Endpoint de geração

```http
POST /pecas/{pecaId}/assistente/solicitacoes
Authorization: Bearer <token>
Content-Type: application/json
Accept: text/event-stream
```

O endpoint valida que a Peça pertence à Organization autenticada e apenas gera
uma resposta. Ele não atualiza a Peça.

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
event: solicitacao.iniciada
data: {"solicitacaoId":"9bdfaf6b-6c57-43ef-86ee-65b614dc9051","clientRequestId":"f79cd150-fca4-491e-9018-7f3a7f177af4"}
```

### `conteudo.delta`

Pode ocorrer várias vezes. Serve apenas para apresentação progressiva; o texto
canônico é o evento final.

```text
event: conteudo.delta
data: {"delta":"A tutela de urgência é cabível quando "}
```

### `solicitacao.concluida`

```text
event: solicitacao.concluida
data: {"sugestao":{"id":"0c31dd95-fcc7-44b4-ab86-28f04fe6dd90","modo":"Redigir","operacaoSugerida":"InserirNoCursor","titulo":"Da tutela de urgência","texto":"Diante da probabilidade do direito...","resumo":"Estrutura os requisitos e relaciona o bloqueio ao perigo de dano.","alertas":[{"codigo":"REFERENCIA_NAO_VERIFICADA","mensagem":"Confirme os dispositivos legais antes do protocolo."}],"ancora":{"from":null,"to":null,"posicao":312,"revisaoEditor":18,"textoOriginal":null}},"uso":{"entradaTokens":1420,"saidaTokens":380},"modelo":"configurado-no-servidor"}
```

`texto` é a fonte canônica inicial. No MVP, parágrafos são separados por uma
linha vazia e convertidos pelo frontend em nós TipTap válidos. HTML retornado
pelo provedor nunca é injetado diretamente no DOM ou no editor.

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
event: solicitacao.falhou
data: {"code":"AI_PROVIDER_TIMEOUT","message":"A IA demorou mais que o esperado. Tente novamente.","retryable":true,"requestId":"00-a1b2"}
```

O stream termina após `solicitacao.concluida`, `solicitacao.falhou` ou
cancelamento da conexão. O frontend ignora qualquer sugestão sem evento final.

## Operações

| Operação | Pré-condição de aplicação no frontend |
|---|---|
| `ResponderNoPainel` | nunca oferece escrita no editor automaticamente |
| `InserirNoCursor` | cursor de destino confirmado; revisão base ainda válida ou novo destino escolhido explicitamente |
| `InserirAposSelecao` | seleção original ainda corresponde ao snapshot |
| `SubstituirSelecao` | `from`, `to`, revisão e texto original ainda correspondem |
| `CriarSecao` | destino da nova seção escolhido e mostrado ao usuário |

O frontend pode oferecer “Inserir no cursor atual” quando a âncora expirou,
desde que isso seja uma nova escolha explícita. Ele nunca deve reutilizar uma
posição antiga silenciosamente.

## Erros HTTP antes do stream

Erros anteriores ao primeiro byte seguem `application/problem+json` e o padrão
atual do middleware da API.

| HTTP | `code` | Situação |
|---|---|---|
| 400 | `AI_INVALID_REQUEST` | modo, contexto ou operação inválidos |
| 401 | padrão de autenticação | sessão ausente ou inválida |
| 404 | `PECA_NOT_FOUND` | Peça inexistente para a Organization |
| 409 | `AI_REQUEST_ALREADY_RUNNING` | mesmo `clientRequestId` ainda em andamento |
| 413 | `AI_CONTEXT_TOO_LARGE` | contexto ultrapassa limite configurado |
| 422 | `AI_CONTEXT_INCOMPATIBLE` | seleção/modo não formam uma solicitação válida |
| 429 | `AI_RATE_LIMITED` | limite local ou do provedor |
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
```

Essas rotas não devem bloquear o endpoint vertical de geração. Quando forem
adotadas, seus contratos passam a integrar este documento.
