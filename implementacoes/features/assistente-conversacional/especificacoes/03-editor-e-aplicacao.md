# Editor, propostas e aplicação transacional

> Especificação de implementação, 2026-11-11. A prova local do editor está
> implementada; [evidência e limites](../evidencias/e0-editor-2026-11-11.md).
> O restante descreve a integração prevista, ainda não entregue.

## Evidência do frontend atual

| Arquivo em `../JuridicoAi-frontend/src/` | Comportamento conferido |
|---|---|
| `features/pecas/editor/legal-document-editor.tsx` | TipTap 3, StarterKit + TextAlign para heading/paragraph; onUpdate agenda autosave e incrementa revisão local |
| `features/pecas/hooks/use-peca-autosave.ts` | Debounce 3s, PATCH versionado; flush chama save uma vez; prepareExport drena pendências e detecta falhas |
| `features/ai-assistant/editor.ts` | Âncoras por posição/revisão local; inserção de texto convertida somente em parágrafos/hardBreak |
| `features/ai-assistant/components/assistant-panel.tsx` | Turns em useState, modos fixos, AbortController vinculado à requisição e estado de aplicação local |
| `features/pecas/editor/use-document-pagination.ts` | Mede filhos DOM e altera marginTop; previews com widgets podem afetar medidas |
| `features/pecas/api.ts` | PATCH não retorna documento canônico, só versão/data/apresentação opcional |
| `pages/CaseWorkspacePage.tsx` | Peça vem do servidor e é remontada ao trocar seleção; conflito pede recarregar |

## Decisão de ancoragem para v1

Usar âncoras **locais a um snapshot versionado**, resolvidas pelo backend. Não
introduzir IDs persistentes em todos os documentos existentes na primeira entrega.
Isso evita uma migration de ConteudoJson e mantém compatibilidade com clientes
atuais. Não exige extensão paga ou mecanismo de colaboração.

O snapshot associa aliases (`bloco-0`, `bloco-1` etc.) a cada nó top-level e limites
entre eles. O modelo recebe estrutura, texto e aliases; não inventa posições DOM
ou ProseMirror. Uma operação consolidada é `SpliceBlocos`: substituir uma sequência
contígua de blocos por outra, incluindo inserção (deleteCount=0) e remoção.
Minuta inicial é um splice de todo o corpo vazio; trocar corpo existente aparece
como substituição integral explícita e exige AI_DOCUMENT_GENERATION.

Listas/blockquote são uma unidade estrutural top-level. A aplicação preserva os
itens não modificados por cópia do JSON original; a revisão pode mostrar diffs
internos, mas uma alteração dependente nessa lista é aprovada como grupo único.
Um pedido sobre seleção em um parágrafo usa o parágrafo como bloco de aplicação,
preservando o restante. A UI destaca apenas diferenças reais.

Persistir operações com parentPath=`["content"]`, startIndex, deleteCount,
expectedBlocksSha256, novosBlocos, IDs de grupo e anchors-base. Parâmetros de
splice são compilados pelo servidor a partir dos aliases conhecidos. O contrato
recebido do modelo não pode escolher parentPath arbitrário nem escrever header,
footer, styles, template, status ou identidade.

Por que não IDs estáveis agora: são uma opção oficial do
[TipTap UniqueID](https://tiptap.dev/docs/editor/extensions/functionality/uniqueid),
mas rebase automático não faz parte da primeira entrega. Se vier a ser necessário,
introduzir IDs estáveis será uma evolução versionada, com teste de paste e undo.

## Documento e preservação de formatação

`editor-body-v1` admite os nós atuais suportados pelo exportador: doc, paragraph,
heading (1–6), text, hardBreak, bulletList, orderedList, listItem, blockquote,
codeBlock e horizontalRule. Marcas: bold, italic, underline, strike, code, link.
textAlign: left/center/right/justify somente paragraph e heading. Links apenas
http/https/mailto; proibir javascript/data e atributos HTML arbitrários.

Para geração nova: parágrafos, headings e listas; não gerar imagens, tabelas ou
nós desconhecidos. Documento preexistente com nó desconhecido não é normalizado
silenciosamente: conservar blocos intactos como opacos e bloquear mudança nesses
blocos com AI_SCHEMA_UNSUPPORTED. Não quebrar autosave de clientes legados por
aplicar esse allowlist globalmente; ele protege as novas propostas.

Blocos sem mudança são copiados byte/semanticamente do snapshot, sem pedir ao
modelo que os reescreva. Dentro de parágrafos, o compilador faz diff por tokens
Unicode e reutiliza slices do conteúdo original nos segmentos iguais, incluindo
marks e hardBreak. Não normalizar pontuação, espaços ou acentos para aplicar.

Inserções herdam marks comuns às duas bordas; em borda sem consenso, usam marks
explicitamente propostos e exibidos na revisão, ou texto sem marks. Alterações
de formatação também são diferenças visíveis, não “iguais” só por terem mesmo texto.
Não é permitido remover bold/link de um segmento inalterado sem mostrar alteração.
O resultado final validado vira JSON imutável antes da aprovação.

Hash canônico `json-c14n-v1`: ordenar chaves ordinalmente, preservar ordem de
arrays, omitir propriedades opcionais ausentes (não converter null em ausente),
serializar UTF-8 sem escapes opcionais; números finitos em representação definida
pelo contrato. Tipos de node possuem attrs normalizados pelo mesmo contrato.
Definir fixtures .NET/TypeScript incluindo acentos, emoji e espaços; o frontend
usa hashes do servidor para aprovar, não inventa um algoritmo alternativo.

Grupos com splices sobrepostos, ancorados no mesmo intervalo de inserção ou com
dependência declarada são fundidos antes da publicação. O modelo não decide sozinho
que mudanças dependentes são independentes. Detectar dependência semântica não é
garantido por índice; quando incerta, apresentar um grupo único e explicar.

## Camada de revisão no editor

Criar `AssistantReviewExtension` com estado de plugin e decorations do ProseMirror:

- Decorações inline/node para texto original removido e formatação modificada.
- Widgets com conteúdo proposto renderizado pelo schema validado, contenteditable=false,
  inseridos junto do local real. Nada de innerHTML de resposta do modelo.
- Botões de grupo e navegação em componentes React externos ao conteúdo editável;
  foco por id e scrollIntoView, labels textuais e atalhos acessíveis.
- Documento original permanece em editor.state.doc; previews não alteram getJSON,
  seleção, clipboard de texto original nem exportação. Cópia da proposta é ação
  distinta e explícita, sem duplicar texto original + widget.

Widgets são uma capacidade documentada no código oficial do
[ProseMirror View](https://github.com/ProseMirror/prosemirror-view/blob/master/src/decoration.ts).
O algoritmo de diff e o renderer são componentes próprios; este desenho não assume
contratação de Tracked Changes ou Compare do TipTap.

Estado de UI: Inativa / Carregando / Revisando / PreparandoAplicacao /
Aplicando / ResultadoIncerto / Desatualizada / Falha. A toolbar mostra pendências,
anterior/próxima, aprovar/rejeitar grupo/todas e sair da revisão.

Revisão é somente leitura no corpo enquanto ativa. “Continuar editando” remove
previews e libera edição manual sem rejeitar grupos. Mudança local invalida a
revisão visual até conferir a nova base. O chat segue funcionando. Essa escolha
evita cursor entrando em texto que ainda não faz parte do documento.

Durante preview, `useDocumentPagination` entra em modo revisão: não aplicar novos
marginTop baseados na altura dos widgets, não tratar preview como paginação final.
Manter largura/margens e estrutura do documento; mostrar “Paginação de revisão”.
Ao sair ou aplicar, recalcular páginas reais. Exportação nunca inclui widgets.

## Barreira de salvamento e commit externo

Generalizar prepareExport para `ensurePersisted(): Promise<{version, localRevision}>`.
Ele aguarda inFlight, drena alterações posteriores, rejeita conflito/erro e
confere mounted. `flush` simples não serve como garantia. Exportação reutiliza
a mesma rotina, preservando o comportamento atual.

Acrescentar coordenação no autosave:

1. `beginExternalCommit`: bloquear mutações de corpo e metadados, troca de modelo,
   undo/redo e exportação; preservar seleção. Aguardar IME/composição terminar.
2. `ensurePersisted`: salvar todo estado pendente; verificar que versão corresponde
   à applicablePecaVersion da proposta. Qualquer alteração manual desde a base
   exige revisão/regeneração, não aprovação sobre versão diferente.
3. Enviar aprovação com IDs e hashes exibidos. Sem aplicação local otimista.
4. Receber recibo. Aplicar exatamente os splices canônicos numa única transação
   TipTap, conferir o hash final e adotar versão/data do servidor.
5. `adoptExternalCommit`: atualizar version/persistedRevision, pending vazio e
   onChanged. Ignorar onUpdate como novo autosave apenas para esta transação
   marcada `assistantCommitId`; contar estatísticas/revisão normalmente.
6. Liberar edição depois da confirmação. Não emitir PATCH duplicado do resultado.

OnUpdate de undo/redo posterior volta ao autosave normal. Se o hash da aplicação
local não coincidir, manter editor bloqueado para reconciliação, sem salvar esse
JSON. Não usar setContent para toda aprovação, pois isso compromete histórico e
seleção. Aplicar splices de trás para frente nas posições ProseMirror resolvidas
a partir do snapshot local, cujo hash foi conferido antes.

`closeHistory` antes e depois separa a aplicação das digitações anteriores e
posteriores, para que um desfazer reverta um lote aprovado. API de referência:
[ProseMirror History](https://github.com/ProseMirror/prosemirror-history/blob/master/src/history.ts).
No remount/reload, pilha de undo antiga não é prometida; não reexecutar splices
se o documento já contém o resultado confirmado.

## Transação de aplicação no backend

Ordem concreta:

1. Lock conversa; validar titular/acesso/flags/deletionEpoch.
2. Procurar recibo por requestId e conferir request hash antes de validar versão
   corrente: um retry legítimo precisa recuperar um commit já confirmado.
3. Lock proposta; exigir revisão, decisionVersion, hashes dos grupos pendentes
   e completude da geração. Não chamar IA nesta operação.
4. Ler Peça; exigir Versao==versaoPecaEsperada==applicablePecaVersion e hash do
   snapshot corrente. Revalidar todos os alvos selecionados.
5. Compilar resultado determinístico, validar schema/complexidade e executar
   UPDATE condicionado a id+organization+versão+ausência de exclusão.
6. Se UPDATE afetar 0, rollback. Caso contrário inserir recibo e registrar grupos
   Aplicados, decisionVersion+1, evento e snapshot remanescente; commit.

`IPecaRepository.AtualizarPecaAsync` atual chama ExecuteUpdateAsync e deve participar
do mesmo DbContext/transação do serviço novo `AssistenteProposalApplicationService`.
Extrair validação compartilhada com PecaApplicationService; não duplicar duas
rotas que permitam divergir texto aplicado e decisão registrada.

O recibo retorna documento e hashes canônicos do commit, base original, versão
resultante e grupos aplicados. Se retry ocorrer depois de nova edição da Peça,
retornar o mesmo recibo com `versaoPecaAtual` e `resultadoAindaAtual=false`;
o cliente não substitui a versão recente pelo documento antigo do recibo.

## Aprovação parcial e propostas ajustadas

Após um lote, só reancorar grupos restantes através do mapa de deslocamento dos
splices que a própria aplicação acabou de confirmar. Copiar conteúdo, hashes de
preview e group IDs sem modificar o que o usuário viu. Atualizar snapshot corrente,
applicablePecaVersion e decisionVersion. Cliente adota a proposta retornada.

Se um grupo remanescente não permanecer equivalente, marcar Desatualizado e
exigir nova revisão; não alterar seu texto silenciosamente. Alteração externa
ou manual na Peça invalida a proposta na v1, mesmo que pareça distante do alvo.
É conservador no conflito, sem exigir uma confirmação adicional no fluxo normal.

Pedido “encurte esta sugestão” inclui proposalId/revisão e grupo opcional. O
orquestrador usa a proposta referenciada, não apenas a última mensagem. Nova
revisão substitui somente pendências; material já aplicado vem da Peça atual.

## Falha de rede e concorrência

Resposta perdida: estado ResultadoIncerto bloqueia outra aplicação; consultar
recibo por requestId. Se ausente, repetir mesmo comando/ID; unicidade e locks
resolvem a corrida. Não gerar novo requestId enquanto o resultado for desconhecido.
Não manter página travada indefinidamente: permitir sair com aviso e reconciliar
ao voltar. Falha de transporte nunca significa “rejeitado” ou “não aplicado”.

Duas abas aprovando: uma vence; a segunda obtém recibo idempotente ou 409 de
decisão/versão. Outra pessoa editando: UPDATE versionado impede sobrescrita.
Falha entre UPDATE e recibo: rollback dos dois. Desfazer após aplicação: novo
PATCH versionado, mantendo o fato histórico de que o grupo foi aplicado antes.

## Arquivos previstos

Reaproveitar o painel atual, mas extrair `useAssistantConversation`,
`useAssistantEvents`, `AssistantAttachmentList`, `AssistantReviewToolbar`,
`AssistantReviewExtension`, `proposal-contract` e `proposal-reducer`.
Atualizar usePecaAutosave, LegalDocumentEditor e paginação. Criar página pessoal
de histórico sem editor. Frontend e backend compartilham fixtures de contrato,
não uma dependência de runtime entre TypeScript e .NET.
