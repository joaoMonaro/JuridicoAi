# Assistente conversacional com edição da Peça e contexto persistente

> **Estado:** proposta de escopo para discussão; implementação não iniciada.
>
> **Data:** 2026-11-10
>
> **Origem:** conversa com o responsável pelo produto.
>
> **Relação:** amplia a proposta de geração de minutas, incluindo continuidade
> conversacional, documentos e edição com aprovação visual. Não declara entrega
> nem transfere aprovações técnicas das avaliações anteriores.

## Problema e objetivo

O fluxo atual exige modos predefinidos, separa pedidos sem memória conversacional
e torna a aplicação das sugestões inconveniente. O usuário precisa conseguir
relatar o caso, fornecer documentos, gerar a minuta e continuar trabalhando nela
com o assistente, inclusive após fechar e reabrir a aplicação.

O objetivo é um chat livre capaz de preparar e executar alterações na Peça
aberta. Antes de qualquer alteração, o usuário visualiza exatamente a mudança
proposta e a aprova. A aprovação na comparação autoriza a aplicação, sem uma
segunda pergunta de confirmação no chat.

## Comportamentos definidos na conversa

- Pedidos em linguagem natural, sem obrigação de escolher modos técnicos.
- Entrada de fatos, orientações e documentos pelo chat.
- Geração de uma minuta inicial a partir desse material.
- Continuidade para revisão, melhoria e correções durante a elaboração.
- Conversa e contexto salvos para retomar o trabalho posteriormente.
- Interação com a Peça atualmente aberta, respeitando seu conteúdo existente.
- Alterações apresentadas explicitamente e aplicadas somente após aprovação.
- Operação dinâmica, evitando perguntas e confirmações repetitivas.

### Decisões de produto confirmadas

- Uma conversa persistente por usuário em cada Peça. Outro usuário autorizado
  abre sua própria conversa, sem acesso ao histórico do primeiro.
- Acesso automático à Peça aberta, à própria conversa e seus anexos, além dos
  dados e documentos cadastrados no mesmo Caso, respeitando as permissões do
  usuário e permitindo identificar as fontes utilizadas. Conversas e anexos
  privados de outros usuários ou de outras Peças não entram no contexto.
- Documentos enviados pelo chat ficam restritos àquela conversa e não são
  cadastrados automaticamente no Caso.
- Conversas não expiram por inatividade ou prazo automático. O próprio usuário
  pode excluí-las manualmente; excluir exige autorização explícita e remove
  mensagens, anexos privados, propostas e contexto derivado do chat, preservando
  o conteúdo já aprovado na Peça. Exclusão ou arquivamento da Peça não autoriza
  apagar automaticamente sua conversa.
- Leitura de PDF com texto, DOCX, TXT, PDF digitalizado e imagens, incluindo OCR
  na primeira entrega.
- Somente trechos com leitura confiável podem ser usados. Conteúdo ilegível,
  incompleto ou duvidoso é sinalizado e excluído do contexto utilizado. Trechos
  confiáveis de um documento parcialmente legível podem ser aproveitados sem
  preencher lacunas por suposição. Qualidade da leitura não resolve contradições
  factuais entre documentos.
- Revisão no próprio documento, com inclusões e exclusões identificadas, mantendo
  o conteúdo salvo intacto até a aprovação.
- Navegação entre mudanças, aprovação ou rejeição por alteração independente e
  ações para aprovar ou rejeitar todas. Mudanças dependentes ficam agrupadas.
- Aprovar aplica a mudança sem nova confirmação no chat. Propostas pendentes
  podem ser ajustadas pela conversa; mudanças já aprovadas são preservadas.
- Em uma Peça vazia, a minuta inteira aparece como proposta formatada com a ação
  “Aprovar minuta”. O usuário pode sair da revisão e editar manualmente.
- Conversas longas mantêm o histórico integral salvo, usando resumo atualizado
  das decisões e consulta às mensagens anteriores para compor o contexto.
- Arquivos extensos são processados por partes com andamento e disponibilidade
  visíveis, sem apresentar leitura parcial como análise completa.
- Minutas extensas podem ser geradas em etapas internas e reunidas em uma
  proposta completa antes da aprovação, sem exigir pedidos repetidos de
  “continue”. Limites impeditivos são informados e o trabalho é preservado,
  sem apresentar conteúdo truncado como minuta completa.

Permanecem abertas as decisões listadas ao final. Os detalhes técnicos e visuais
precisam concretizar os comportamentos acima antes da implementação.

## Jornada proposta para a primeira entrega

1. Dentro de sua conversa individual na Peça, o usuário relata o caso e envia
   documentos, de uma vez ou progressivamente. Mensagens e anexos são persistidos
   com acesso restrito à conversa.
2. O sistema informa o estado de processamento dos anexos e quais materiais
   estão disponíveis para o assistente. Falhas de leitura são visíveis.
3. Ao receber um pedido de minuta, o assistente usa o material disponível e
   apresenta uma proposta completa, com observações separadas do texto jurídico.
4. O usuário confere a proposta e aprova sua inserção na Peça. Se já houver
   conteúdo, a comparação apresenta o destino e qualquer substituição proposta.
5. O usuário continua editando manualmente e conversando. Pode acrescentar fatos,
   corrigir informações, discutir argumentos ou pedir alterações localizadas.
6. Pedidos de edição geram propostas sobre a versão atual da Peça, com comparação
   entre original e resultado. Aprovar executa a aplicação no editor e o
   salvamento existente; rejeitar preserva o documento.
7. Ao retornar depois, o usuário recupera a conversa, os documentos disponíveis,
   a Peça salva e o estado das propostas. Propostas antigas precisam ser
   conferidas contra o documento atual antes de poderem ser aplicadas.

## Conversa e contexto de trabalho

Cada usuário possui uma conversa persistente própria em cada Peça. Ao abrir a
mesma Peça, outro usuário autorizado inicia ou retoma a sua própria conversa;
não herda mensagens, anexos, orientações privadas ou propostas pendentes do
primeiro usuário.

O assistente pode consultar automaticamente a Peça atual, a própria conversa e
seus anexos, além dos dados e documentos cadastrados no mesmo Caso, conforme o
pedido e as permissões do usuário. Disponibilidade não significa enviar todo o
material ao modelo em cada mensagem. O isolamento entre conversas abrange também
texto extraído, OCR, resumos, referências a fontes e mecanismos de busca.

A conversa é individual; a Peça é compartilhada conforme suas permissões.
Alterações aprovadas passam a integrar o documento e podem ser vistas pelos
demais usuários autorizados, sem disponibilizar o chat ou anexo de origem. Ao
retomar a conversa, novas propostas consideram inclusive mudanças feitas por
outros usuários; propostas antigas precisam ser verificadas contra a versão atual.

| Informação | Comportamento esperado |
|---|---|
| Mensagens do usuário e do assistente | Persistir e permitir retomada da conversa |
| Documentos fornecidos no chat | Preservar vínculo privado com a conversa, origem, estado de leitura e conteúdo utilizável |
| Documentos cadastrados no Caso | Consultar automaticamente conforme as permissões, sem incluir anexos privados de outros chats |
| Peça atual | Ser a referência para preparar novas edições, incluindo alterações manuais |
| Propostas | Registrar versão de origem, mudanças e estado: pendente, aplicada, rejeitada ou desatualizada |
| Decisões e correções | Permitir continuidade sem exigir que o usuário repita orientações |
| Fontes utilizadas | Permitir identificar de qual material fornecido veio uma informação relevante |

Uma resposta gerada ou proposta pendente não equivale a conteúdo aprovado.
Uma proposta rejeitada não deve reaparecer como decisão aceita. Correções
explícitas do usuário devem prevalecer sobre informações anteriores corrigidas;
contradições materiais ainda não resolvidas precisam ser apresentadas ao usuário.

Persistir a conversa não implica enviar todo o histórico ao modelo a cada pedido.
A estratégia de seleção, consulta a documentos e resumos será definida no desenho
técnico, mantendo rastreabilidade ao material original e respeitando limites de
entrada. Resumos não podem transformar suposições em fatos confirmados.

## Conversas longas, arquivos extensos e geração em etapas

O histórico integral permanece persistido. Para compor o contexto de cada pedido,
o assistente pode usar um resumo atualizado das decisões e consultar mensagens
anteriores relevantes. Essa organização não exclui nem substitui o histórico
original armazenado e deve respeitar correções, rejeições e mudanças na Peça.

Documentos extensos podem ser processados por partes, com progresso, falhas e
conteúdo disponível identificáveis. O assistente deve distinguir o material já
processado do restante e não apresentar conclusões parciais como análise do
documento completo. Somente trechos com leitura confiável podem ser utilizados.

Quando uma minuta exigir múltiplas gerações, o sistema coordena as etapas e
reúne o resultado em uma proposta completa e coerente antes da aprovação. O
usuário não precisa solicitar “continue” repetidamente. Nenhuma etapa interna
autoriza aplicação antecipada na Peça.

Se um limite impedir a conclusão, o sistema informa o impedimento e o que falta,
preserva o trabalho produzido e distingue rascunho incompleto de proposta pronta
para aprovação. Essa preservação não altera a Peça nem elimina a exigência de
revisão visual da versão final.

Limites numéricos de arquivos, páginas, entrada, saída, etapas e consumo serão
definidos no desenho técnico, considerando capacidade e custo. A aprovação deste
comportamento não significa processamento ou geração ilimitados.

## Liberdade de interação

- Perguntas e discussões recebem respostas sem exigir aplicação no editor.
- Pedidos claros de edição produzem mudanças concretas para revisão visual.
- A Peça aberta compõe o contexto de trabalho; seleção manual é um recurso para
  delimitar o pedido, não uma exigência para toda edição.
- O assistente pergunta quando uma ambiguidade ou falta de informação impede um
  resultado útil. Lacunas que permitam uma minuta preliminar podem virar campos
  pendentes, apresentados separadamente.
- O usuário pode pedir ajustes em uma proposta antes de aprová-la, sem alterar
  antecipadamente a Peça.
- A IA continua limitada às permissões verificadas pela aplicação. Interpretar
  intenção não concede acesso ou autorização para modificar documentos.

## Retenção e exclusão da conversa

O histórico permanece salvo sem expiração automática por prazo ou inatividade.
O usuário pode excluir manualmente sua própria conversa e começar outra. A
interface deve explicitar o alcance dessa ação e obter autorização explícita.

A exclusão abrange mensagens, anexos privados, propostas e conteúdo derivado
usado como memória ou fonte, incluindo extrações, OCR, resumos e índices de
consulta. Esses dados não podem continuar disponíveis ao assistente em uma nova
conversa. Texto já aprovado na Peça e documentos cadastrados separadamente no
Caso permanecem preservados.

A nova conversa retoma somente o contexto automático autorizado: Peça atual e
material do Caso. Não recupera o histórico privado excluído.

Excluir ou arquivar a Peça, ou retirar o acesso do usuário, não equivale a
autorizar exclusão do chat. Essas operações não devem acionar exclusão em cascata
da conversa ou dos seus anexos. A retenção não concede acesso a dados da Peça
após revogação das permissões; os detalhes de consulta e exclusão do histórico
nessa situação precisam ser especificados no desenho técnico.

O desenho técnico também deve definir a conclusão da exclusão de derivados e
cópias de armazenamento, sem apresentar remoção parcial como exclusão concluída.

## Comparação, aprovação e aplicação

A interface deve mostrar o local afetado e distinguir inclusões, exclusões e
substituições, preservando contexto suficiente do texto original. Mostrar apenas
o novo texto não atende ao objetivo da comparação.

As mudanças de um pedido são agrupadas em uma proposta apresentada no próprio
documento. Trechos removidos aparecem riscados; inclusões recebem destaque e
identificação textual, sem depender apenas de cores. Trechos intactos permanecem
visíveis para leitura em contexto. Uma barra informa o número de alterações
pendentes, permite navegar entre elas e aprovar ou rejeitar todas. Cada alteração
independente também pode ser aprovada ou rejeitada individualmente. Mudanças
dependentes devem formar um grupo coerente, impedindo aplicação parcial
inconsistente.

Essa visualização não modifica o conteúdo salvo antes da aprovação. Na Peça
vazia, a minuta completa é exibida como proposta formatada, com a ação “Aprovar
minuta”. Aprovada a criação, revisões seguintes destacam somente as mudanças.
O chat apresenta explicações breves e permite pedir ajustes em propostas
pendentes. Aprovar um trecho aplica a mudança, remove seu destaque de pendência
e aciona o salvamento normal; rejeitar mantém o original.

Cada aprovação se refere ao conteúdo efetivamente exibido. Alterar a proposta
exige uma nova revisão visual. Depois de aprovada, a aplicação deve ocorrer uma
única vez, preservar formatação e conteúdo fora do escopo, respeitar conflitos
de versão e permitir desfazer.

Se o usuário modificar o documento enquanto a proposta estiver pendente, o sistema
não deve substituir silenciosamente a edição manual. Deve verificar compatibilidade
ou preparar uma nova proposta para revisão. Falhas de salvamento precisam ser
visíveis; aprovação não pode ser apresentada como persistência bem-sucedida antes
da confirmação do salvamento.

## Documentos fornecidos pelo chat

Anexar um arquivo precisa torná-lo utilizável na conversa, dentro dos formatos
explicitamente suportados. Somente armazená-lo não atende ao escopo.

A primeira entrega deve contemplar envio, processamento, identificação de origem
e uso do conteúdo nas respostas e minutas. Arquivos não suportados, ilegíveis ou
parcialmente processados devem ter indicação clara; o assistente não pode alegar
ter lido conteúdo indisponível.

Estão incluídos PDF com texto, DOCX, TXT, PDF digitalizado e imagens, com OCR na
primeira entrega. Documentos já cadastrados no mesmo Caso ficam automaticamente
disponíveis para consulta conforme as permissões do usuário.

Anexos enviados pelo chat pertencem exclusivamente àquela conversa. Não são
promovidos automaticamente a documentos do Caso nem disponibilizados a outros
chats. O mesmo isolamento vale para o conteúdo extraído e seus derivados. O
cadastro de documentos do Caso permanece um fluxo separado; uma ação de promoção
do anexo privado ao Caso não está incluída implicitamente nesta entrega.

Somente trechos com leitura confiável entram no contexto utilizado. Trechos
ilegíveis, incompletos ou duvidosos ficam excluídos e são sinalizados por arquivo
e página, quando aplicável, permitindo reenviar uma versão melhor. Partes
confiáveis podem ser aproveitadas sem tratar o documento inteiro como lido.
O filtro avalia a qualidade da extração; contradições entre conteúdos legíveis
continuam precisando ser identificadas.

Critérios de qualidade, formatos específicos de imagem e limites de tamanho
serão detalhados no desenho técnico. O suporte atual de armazenamento não
comprova suporte de leitura pela IA.

## Reaproveitamento e limites

Reaproveitar autenticação, isolamento por organização, permissões de IA, aviso
de transparência, controles operacionais, registro de consumo, editor e salvamento
com versão. Adaptar contratos, contexto, propostas e avaliações ao novo fluxo.

A persistência do conteúdo da conversa será uma funcionalidade de produto,
distinta dos logs técnicos, que devem continuar sem conteúdo jurídico.

Não fazem parte implicitamente desta proposta: pesquisa jurídica externa,
integração do scraper ao chat, publicação ou protocolo de peças, ações em outros
Casos ou Peças, coedição simultânea e múltiplas conversas ramificadas para o mesmo
usuário na mesma Peça.
Esses itens exigem escopo próprio se forem desejados.

## Critérios de aceite propostos

- [ ] Relatar fatos e fornecer documentos no chat permite gerar uma minuta sem selecionar modo técnico.
- [ ] A interface informa quais anexos foram lidos, falharam ou não são suportados.
- [ ] Trechos de leitura incompleta ou duvidosa são sinalizados e excluídos do contexto utilizado.
- [ ] Dados e documentos autorizados do mesmo Caso podem ser consultados sem seleção manual repetitiva.
- [ ] A minuta usa o material disponível sem inventar fatos ou fontes para preencher lacunas.
- [ ] Perguntas informativas não alteram o documento nem exigem aprovação de edição.
- [ ] Uma alteração exibe original, resultado e destino antes da aprovação.
- [ ] A revisão ocorre no próprio documento, com navegação e aprovação ou rejeição individual e em conjunto.
- [ ] Aprovar na comparação aplica a mudança sem nova confirmação no chat.
- [ ] Rejeitar, cancelar ou ajustar uma proposta pendente preserva a Peça.
- [ ] Pedidos como “encurte essa parte” podem referir-se à proposta anterior na conversa.
- [ ] Novas edições consideram a Peça atual, incluindo mudanças manuais.
- [ ] Uma correção factual no chat é considerada nos pedidos posteriores.
- [ ] Reabrir a página recupera mensagens, anexos disponíveis e estados das propostas.
- [ ] Conversas não são excluídas automaticamente por prazo, inatividade ou exclusão da Peça, sem autorização explícita.
- [ ] O usuário pode excluir sua conversa; mensagens, anexos privados e derivados deixam de integrar o contexto, preservando o texto aprovado na Peça.
- [ ] Uma nova conversa não recupera conteúdo privado excluído e utiliza somente o contexto automático autorizado.
- [ ] Outro usuário autorizado da mesma Peça recebe seu próprio chat, sem acesso a mensagens, propostas ou anexos privados do primeiro.
- [ ] Anexos privados, OCR, extrações e resumos não vazam por buscas, referências ou contexto automático de outros chats.
- [ ] Alterações aprovadas ficam visíveis na Peça conforme suas permissões, preservando a privacidade da conversa de origem.
- [ ] Propostas não são aplicadas duas vezes nem sobre conteúdo incompatível sem nova revisão.
- [ ] Aplicação, salvamento, desfazer e persistência após recarregar funcionam em conjunto.
- [ ] Alertas e explicações não são inseridos acidentalmente como texto jurídico.
- [ ] O contexto e os arquivos de outra organização não ficam acessíveis ao assistente.
- [ ] Geração incompleta, contexto excessivo e falhas de leitura ou salvamento têm tratamento visível.
- [ ] Resumir o contexto de conversas longas preserva o histórico integral e permite consultar mensagens anteriores.
- [ ] Arquivos extensos apresentam progresso e distinguem leitura parcial de processamento concluído.
- [ ] Minutas geradas em etapas formam uma proposta completa antes da aprovação, sem pedidos repetidos de “continue”.
- [ ] Limites impeditivos preservam o trabalho e não expõem texto truncado como minuta pronta para aplicação.
- [ ] Testes pertinentes, avaliações atualizadas e validação manual cobrem a jornada completa.

## Decisões restantes antes da implementação

1. Definir limites numéricos, orçamento de execução e mecanismos técnicos para
   conversas longas, arquivos extensos e geração em etapas, conforme o
   comportamento aprovado.
2. Detalhar critérios de qualidade da extração, formatos de imagem e limites de
   arquivos, concretizando a exclusão de trechos de leitura duvidosa.
3. Detalhar acesso e exclusão do histórico retido após exclusão da Peça ou perda
   de permissão, sem eliminar o chat automaticamente nem conceder acesso indevido.
4. Localizar o frontend atual: a cópia local do repositório irmão não contém o
   chat recente descrito na documentação.

Essas definições não exigem retomar modos técnicos na experiência do usuário.

## Sequência sugerida de implementação

1. Fechar decisões de produto e desenhar conversa, propostas e comparação.
2. Definir persistência e contratos, incluindo mensagens, anexos e versões.
3. Integrar processamento documental e montagem de contexto conversacional.
4. Implementar geração inicial e ciclo de propostas, aprovação e aplicação.
5. Validar continuidade, preservação do documento, isolamento e qualidade.

As etapas são partes da mesma entrega; persistir mensagens sem usá-las no
acompanhamento, ou permitir upload sem leitura, não conclui o escopo. Não há
estimativa de prazo aprovada para esta ampliação.

## Referências

- [Desenho técnico proposto](desenho-tecnico.md).
- [Comportamento atual do assistente](../../../docs/backend/assistente-ia.md).
- [Proposta anterior de geração de minutas](../geracao-minutas-ia/README.md).
- [DTOs atuais](../../../src/JuridicoAI.Api/src/Application/DTOs/AssistenteIaDtos.cs).
- [Orquestração atual](../../../src/JuridicoAI.Api/src/Application/Services/AssistenteIaApplicationService.cs).
- [Montagem atual de contexto](../../../src/JuridicoAI.Api/src/Application/Services/AssistenteContextBuilder.cs).
