# Persistência e aplicação da revisão no editor real

Data: 2026-11-11. Implementação local nos repositórios backend e
`../JuridicoAi-frontend`, ainda sem commit desta etapa. Recorte de E1/E4;
não conclui o assistente conversacional.

Este registro descreve a etapa de revisão. A evolução posterior de mensagens e
contexto está no [registro de histórico](e1-e3-historico-2026-11-11.md).

## Comportamento implementado

Com a capacidade `suportaRevisaoPersistida` habilitada, uma sugestão editável do
assistente atual oferece “Revisar no documento”. Essa ação prepara a comparação
e salva a proposta privada, sem alterar a Peça. A aprovação na comparação é a
única autorização de aplicação; não há uma segunda confirmação no chat.

- Conversa única por organização, usuário e Peça, protegida por índice único.
- Propostas recuperadas ao reabrir a Peça, com documento-base, grupos, hashes e
  versões persistidos. Lista retorna as 30 propostas mais recentes.
- Registro idempotente de um par pedido/resposta por proposta; mensagens possuem
  sequência e consulta paginada. Perguntas/respostas sem proposta ainda não são
  persistidas, e essas mensagens ainda não compõem o contexto do modelo.
- Comparação no editor real, navegação, decisões individuais e em lote.
  O adaptador legado produz um grupo único; propostas com vários grupos são
  suportadas pelo backend e pelo plugin, sem inferência de independência semântica.
- Aprovação altera Peça, decisões e recibo na mesma transação PostgreSQL. A
  atualização otimista da Peça impede sobrescrever outra versão.
- Aprovações parciais remapeiam operações remanescentes sem mudar seu preview.
  Rejeição modifica decisões, preservando a Peça.
- Recibo por requestId impede aplicação duplicada e distingue resultado antigo
  da versão atual da Peça. A reconciliação consulta o recibo e, se necessário,
  repete o mesmo comando aprovado.
- Frontend aguarda o servidor antes de alterar o corpo, coordena a versão com o
  autosave e não envia PATCH duplicado. Desfazer continua como edição manual e
  salva sobre a versão confirmada.
- Enquanto o resultado de aplicação é incerto, edição e decisões ficam
  bloqueadas; “Verificar resultado” recupera o commit sem pedir nova aprovação.
- Paginação suspensa durante o preview, com indicação “Paginação de revisão”.
  A abertura enquadra o trecho original, e não apenas o texto proposto.
- FKs restritivas impedem exclusão física em cascata do histórico. A exclusão
  lógica da Peça não remove as mensagens.

Não houve chamada adicional ao modelo nem alteração de provedor/prompt.
O endpoint transitório recebe um rascunho editável do cliente; não o trata como
prova de que aquele texto veio diretamente do provedor. A publicação futura por
worker deverá reutilizar o compilador e o serviço de aplicação.

## Dados e rotas desta etapa

Migration aditiva `20261111132819_AddAssistantReviewWorkspace`:
`ai_conversations`, `ai_messages`, `ai_proposals`, `ai_application_receipts`.
Organização e conversa compõem as FKs dos agregados. Grupos ficam em JSONB dentro
da proposta, atualizados sob lock da conversa; não foi criada uma tabela separada
para cada grupo. O schema guarda documento-base e operações compiladas, sem
migrar ou inserir IDs nos nós dos documentos existentes.

| Rota relativa a `/assistente/v2` | Uso disponível |
|---|---|
| `POST /pecas/{id}/conversa` | Abrir/obter conversa própria |
| `GET /conversas/{id}/mensagens?antes=&limite=` | Mensagens associadas às propostas |
| `GET /conversas/{id}/propostas` | Recuperar propostas |
| `POST /conversas/{id}/propostas` | Registrar rascunho da sugestão legada |
| `GET /propostas/{id}` | Consultar proposta própria |
| `POST /propostas/{id}/aplicacoes` | Aprovar e obter recibo |
| `GET /conversas/{id}/aplicacoes/{requestId}` | Recuperar resultado |
| `POST /propostas/{id}/rejeicoes` | Registrar rejeição |

Registro transitório: `clientRequestId`, `versaoPecaEsperada`, `instrucao`,
`textoResposta`, `grupos[{explicacao,operacoes[{startIndex,deleteCount,novosBlocos}]}]`.
DTOs recusam campos desconhecidos. Rejeição usa revisão, versão das decisões e IDs
dos grupos; repetição com versão antiga retorna conflito, sem idempotência por
requestId nesta etapa. O contrato planejado de rejeição idempotente ainda falta.

`Proposta.documentoBase` foi incorporado ao JSON Schema e aos exemplos para
reabertura da comparação. O compilador verifica filhos/attrs/marks suportados,
links, limites, sobreposições, conteúdo original e ausência de diferenças. Aceita
o parágrafo vazio implícito do TipTap em documentos legados com `content: []`.
Estruturas existentes fora dos blocos substituídos são preservadas; o suporte
integral a documentos antigos com tipos desconhecidos ainda exige validação.

## Ativação e rollback

`AssistenteIa:ReviewV2Enabled` é `false` por padrão. Em ambiente de teste, após
aplicar a migration, definir `AssistenteIa__ReviewV2Enabled=true`, mantendo a
configuração de IA existente válida, o controle operacional global ligado e
`AI_ASSISTANT`/`AI_EDIT` autorizados para a organização pelos controles existentes.
O frontend lê essa capacidade na API; não há uma flag local adicional.

A ativação muda a versão efetiva do aviso para
`{TransparencyNoticeVersion}-review-v2`, informando a persistência privada dos
pedidos/propostas. O aceite antigo não libera escrita persistida. É um aviso
versionado de transparência, separado da aprovação de cada mudança.

Desligar a flag bloqueia novos registros/aplicações, mantém as tabelas e permite
consultar histórico próprio com autenticação organizacional válida. A interface
retorna ao fluxo legado, sem apresentar esse rollback como exclusão dos chats.
O gate v2 separado por organização, a área pessoal de histórico e a leitura após
perder Membership continuam pendentes. `suportaHistorico` permanece `false`.

A migration foi aplicada somente em bancos descartáveis de validação. Não foi
aplicada no banco do ambiente de trabalho do usuário, nem houve deploy/ativação.
O serviço existente executa migrations ao iniciar quando `Postgres:RunMigrations`
está habilitado; considerar isso ao iniciar a API sobre um banco persistente.

## Verificações executadas

| Verificação | Resultado |
|---|---|
| `dotnet test tests/JuridicoAI.Api.Tests` com `JURIDICOAI_REVIEW_TEST_POSTGRES` | 170 passaram, sem skips |
| PostgreSQL real, oito cenários | Reabertura, privacidade, documento vazio, aplicação parcial, idempotência, concorrência, rollback, versão e retenção |
| HTTP contra API real em banco descartável | JWT, capacidades, novo aviso/428, campos inválidos/400, registro, aprovação, recibo, idempotência/409, isolamento entre organizações e entre titulares da mesma organização |
| `npm run test:run` no frontend | 109 passaram em 13 arquivos |
| Playwright: `assistant-review` + `assistant-persistent-review` | 8 passaram; reabertura, perda de resposta, rejeição, desfazer persistido e controles de revisão |
| `npm run build` e `npm run lint` no frontend | Passaram; permanece aviso de bundle maior que 500 kB |
| JSON Schema e nove exemplos | Validados com Draft202012Validator e formatos |
| `dotnet-ef migrations has-pending-model-changes` | Modelo coincide com a migration; ferramenta 10.0.3 em runtime EF 10.0.4 |

Os oito testes PostgreSQL criam e apagam um banco por cenário. Incluem falha
sintética na gravação do recibo depois do UPDATE da Peça: a transação reverte
ambos. Duas chamadas simultâneas com o mesmo requestId geram um único commit.
O hash canônico é conferido contra bytes UTF-8 com acento e emoji, além do recibo
fictício de referência.

Playwright usa API fake para testar o contrato do editor, sem simular que isso
prova atomicidade de banco. Essa propriedade é exercitada pelos testes PostgreSQL.
A checagem HTTP separada usou a API real, usuários, memberships, Caso e Peça
fictícios, sem chamar o provedor de IA. Nenhum dado jurídico real foi usado.

O teste legado `assistant-main-flow` continua falhando no seletor que espera texto
original visível dentro de um `<details>` fechado. Esse mesmo ID já falhava no
HEAD anterior, conforme [comparação de baseline da E0](e0-editor-2026-11-11.md).
A suíte legada completa não está aprovada. A captura da integração está em
`../JuridicoAi-frontend/test-results/assistant-persistent-review.png` (ignorada
pelo Git), inspecionada visualmente.

## Trabalho que continua pendente

- Chat livre sem modos e persistência/recuperação de todas as mensagens na UI.
- Uso efetivo do histórico, correções e propostas anteriores no contexto do modelo.
- Worker durável, checkpoints, eventos, geração extensa e ferramentas de consulta.
- Anexos privados, extração, OCR, qualidade de leitura e contexto automático do Caso.
- Autenticação pessoal de histórico, acesso após revogação e exclusão autorizada.
- Revisão de propostas pelo chat, proveniência da geração e gate v2 por organização.
- Validação integral de exportação, schemas antigos, limites, consumo e jornada final.

O requestId de uma aplicação incerta fica na memória do editor montado. Ao
recarregar, a Peça e a proposta são lidas novamente do servidor; não há ainda um
intent journal no navegador que retome aquele botão específico após reload.

Se a capacidade for desligada com uma proposta aberta, o preview é removido e
o editor volta a permitir edição, preservando a proposta salva. Uma aplicação
incerta mantém os controles de recuperação disponíveis até reconciliar.
