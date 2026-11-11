# Histórico completo e contexto conversacional básico

Data: 2026-11-11. Recorte de E1/E3 sobre a revisão persistida da etapa anterior.
Implementação local no backend e em `../JuridicoAi-frontend`, ainda sem commit.
Ativação controlada; não conclui o assistente livre nem a entrega documental.

## Disponível nesta etapa

- Perguntas e respostas são persistidas, inclusive quando não geram proposta.
  O painel recupera o histórico ao reabrir a Peça, com paginação de 50 mensagens.
- Cada envio reserva, em uma transação, a mensagem do usuário e o registro da
  resposta em andamento. A resposta estruturada é salva antes de confirmar a
  conclusão ao navegador.
- A identidade vem do JWT. Conversa, mensagens, respostas e propostas continuam
  individuais por usuário/Peça, com isolamento por organização.
- O próximo pedido recebe o histórico cronológico elegível da própria conversa e
  a versão atual da Peça, carregada no servidor. Isso independe de selecionar
  manualmente o documento inteiro ou de carregar todas as páginas do chat na UI.
- A política de continuidade distingue rascunhos de fatos, prioriza correções
  explícitas posteriores do usuário e não trata rejeição como aprovação.
- Respostas rejeitadas permanecem legíveis, com estado persistido, e não entram
  no contexto automático. Falhas, cancelamentos e interrupções preservam o pedido.
- Requests concluídos podem ser repetidos com o mesmo clientRequestId e payload
  para recuperar o resultado sem gerar novamente. Outro payload retorna 409.
- A resposta de um chat pode originar uma proposta sem duplicar pedido/resposta
  no histórico. O vínculo é conferido no servidor, na conversa do titular.
- A Peça é sincronizada antes do envio. Respostas históricas não reaproveitam
  âncoras para aplicação; propostas registradas são reabertas pela revisão salva.
- Falha ao recuperar o histórico bloqueia novo envio até recuperar a conversa.
  O usuário não continua silenciosamente sem o contexto prometido.

O provedor, modelo configurado, saída estruturada, contagem de tokens e ledger
existentes foram reaproveitados. Não se usa `previous_response_id` nem um chat
compartilhado no provedor: a aplicação controla a memória no PostgreSQL e inclui
os dados elegíveis no input. A OpenAI descreve a manutenção manual de contexto
em [Conversation state](https://developers.openai.com/api/docs/guides/conversation-state).
`store=false` e os controles de geração existentes permanecem ativos. A política
adicional `politica-conversa-v1` é identificada nos logs de metadados e no ledger.

## Persistência e contratos efetivos

Migration `20261111142012_PersistAssistantConversationMessages`, posterior a
`AddAssistantReviewWorkspace`. A tabela `ai_messages` recebe campos de request,
resposta a uma mensagem, resultado estruturado, estado, código de falha e prazo
operacional. `proposta_id` passa a ser opcional. Mensagens antigas continuam
legíveis e recebem estado inicial `Concluida`.

FK composta da resposta garante que o pedido pertence à mesma conversa e
organização. Índices únicos protegem clientRequestId, resposta por mensagem e
uma única resposta `Gerando` por conversa. O lock da conversa serializa reservas,
conclusões e decisões. Nenhum desses prazos exclui mensagens.

| Endpoint | Contrato implementado |
|---|---|
| `GET /assistente/v2/conversas/{id}/mensagens?antes=&limite=50` | Array em ordem crescente, cursor exclusivo, máximo 100; pode carregar páginas anteriores |
| `POST /assistente/v2/conversas/{id}/mensagens` | Request transitório com clientRequestId, modo, instrucao, versaoPeca, operacaoPreferida e contexto; resposta SSE 200 com o protocolo existente |
| `POST /assistente/v2/conversas/{id}/mensagens/{mensagemUsuarioId}/rejeicao` | 204, idempotente; rejeita resposta sem proposta registrada, preservando o conteúdo |
| `POST /assistente/v2/conversas/{id}/propostas` | Acrescenta mensagemUsuarioId opcional para vincular o pedido existente |

A mensagem de leitura inclui `clientRequestId`, `respostaAId`, `estado`,
`codigoErro`, `solicitacao` e `resultado`, além dos campos já disponíveis. O
resultado da geração inclui `sugestao.mensagemUsuarioId` para o vínculo posterior.
Não se confia em uma autoria enviada pelo cliente; o par e o texto da instrução
são conferidos na conversa autenticada.

Estados da resposta: `Gerando`, `Concluida`, `Falhou`, `Cancelada`, `Interrompida`
e `Rejeitada`. Um pedido terminal que não concluiu não é executado outra vez com
o mesmo ID; reenviar explicitamente cria outro pedido e preserva o anterior.

O POST continua sendo um adaptador do gerador atual, com modos e SSE ligados à
requisição. Não é ainda o contrato planejado `EnviarMensagem` sem modo/HTTP 202,
nem worker durável, SSE de eventos reproduzíveis ou execução desacoplada do browser.
Fechar/cancelar o stream cancela a geração atual. Uma queda do processo deixa o
registro legível como interrompido após o prazo (timeout configurado + 30s),
libera a conversa no próximo envio e impede publicação tardia daquele resultado.
Não há retomada automática nem nova chamada paga após reiniciar.

## Contexto e limites explícitos

O backend reúne mensagens por sequência, informações adicionais fornecidas pelo
próprio usuário e o documento atual do servidor. O histórico não altera instruções
de sistema e não autoriza escrita na Peça. A geração continua sem aplicar nada.

Resultados antigos baseados em outra versão da Peça, em contextos compartilhados
explícitos do Caso ou sem metadados de origem não são reutilizados como fonte
atual. Permanecem no histórico legível; o input contém indicação da omissão.
Mensagens fornecidas pelo usuário continuam com essa origem. Decisões de propostas
são identificadas por grupo/estado, sem reenviar conteúdo derivado de fonte inelegível.
Essa é uma filtragem conservadora; revalidação detalhada de fontes e recuperação
sob referência explícita continuam para a próxima integração de contexto.

Não há truncamento silencioso. O limite atual é `MaxTotalContextCharacters`
(80.000 por padrão), além da contagem de tokens existente, e a montagem desta
etapa admite no máximo 1.000 mensagens. Atingir o limite mantém o histórico e
sinaliza `AI_HISTORY_CONTEXT_TOO_LARGE`, sem enviar contexto incompleto ao modelo.
Quando o excesso só é identificado na preparação final, o pedido e o estado da
falha também ficam preservados. Resumos versionados e consulta histórica ainda
são necessários para continuidade de conversas maiores; esse suporte não está
sendo declarado pronto.

## Ativação e retenção

As duas flags ficam desligadas no `appsettings.json`. Em teste, depois das duas
migrations, habilitar:

```text
AssistenteIa__ReviewV2Enabled=true
AssistenteIa__ConversationV2Enabled=true
```

`ConversationV2Enabled` exige `ReviewV2Enabled`; a combinação inválida é recusada
na inicialização. Manter configuração válida do provedor, controle operacional e
`AI_ASSISTANT`; edição requer também `AI_EDIT`. `suportaHistorico` passa a ser
verdadeiro somente com a conversa habilitada e acesso de IA disponível. A UI usa
essa capacidade e mantém o fluxo anterior quando ela está desligada.

O aviso efetivo passa a `{TransparencyNoticeVersion}-conversation-v2`, explicando
armazenamento de mensagens e uso do histórico em pedidos posteriores. Aceitar o
aviso limitado a propostas não vale para essa nova finalidade.

Desligar a flag preserva os registros. Leitura autenticada própria continua pela
API organizacional. A área pessoal para acesso após perda de Membership, exclusão
manual autorizada e seus derivados continuam pendentes. O downgrade da migration
é bloqueado quando houver registros conversacionais que perderiam informação;
rollback funcional deve usar a flag, mantendo o banco.

A migration foi aplicada somente em bancos descartáveis. O banco do ambiente do
usuário, permissões e configurações de IA do projeto não foram alterados.
Não houve deploy, commit ou push desta etapa.

## Verificação executada

| Verificação | Resultado |
|---|---|
| `dotnet test tests/JuridicoAI.Api.Tests` com PostgreSQL descartável | 180 passaram, sem skips |
| Testes da conversa com PostgreSQL real | Reabertura, contexto cronológico, isolamento, idempotência, concorrência, cancelamento, interrupção, rejeição, limite e vínculo com proposta |
| Testes do gerador | Histórico chega ao request do provedor, política versionada no ledger e excesso de contexto não chama o provedor |
| Teste de transparência | Novo aceite exigido para persistência e uso do histórico |
| `npm run test:run` | 114 passaram em 14 arquivos |
| Playwright: conversa + revisão persistida + revisão local | 14 passaram |
| Build e lint do frontend | Passaram; aviso existente de bundle maior que 500 kB permanece |
| EF `has-pending-model-changes` | Modelo coincide com a migration |
| HTTP com API e PostgreSQL reais, provedor local simulado | Aviso, persistência, replay sem nova geração, conflito de payload, histórico/correção no input, rejeição e Peça intacta |
| Navegador + frontend/API/PostgreSQL reais, provedor local simulado | Relato, correção, sugestão, proposta vinculada sem duplicação, aprovação em Peça vazia, versão e reabertura |

O teste integrado encontrou divergência da etapa anterior na Peça vazia. O editor
agora normaliza `content: []` para um parágrafo em memória, também na base do
preview, alinhando o compilador do servidor. Abrir não dispara PATCH. A primeira
aprovação foi conferida ponta a ponta, e uma regressão da extensão verifica
conferência do recibo e undo desse caso.

Captura inspecionada em
`../JuridicoAi-frontend/test-results/assistant-chat-api-integration.png`, ignorada
pelo Git. Somente dados fictícios, contas sintéticas e provedor simulado foram
usados; nenhuma chamada à OpenAI nem avaliação semântica com o modelo real.
As falhas legadas de Playwright descritas na E0 continuam fora deste aceite;
a suíte legada completa não é declarada aprovada.

## Próximos recortes

- Intenção automática e chat sem obrigar modos técnicos; revisão de proposta por referência.
- Resumo versionado e busca histórica para conversas que ultrapassem a janela atual.
- Worker, eventos duráveis, checkpoints e geração extensa recuperável.
- Documentos privados, leitura/OCR, fontes do Caso e contexto autorizado com proveniência.
- Área pessoal de histórico e exclusão manual autorizada.
- Avaliação com modelo real, qualidade jurídica e calibração operacional antes de liberar o produto completo.
