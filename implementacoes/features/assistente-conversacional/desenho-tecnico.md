# Desenho técnico proposto — assistente conversacional

> **Estado:** conversa livre, propostas automáticas, revisão persistida e contexto
> de histórico integrados ao editor atrás de flags. Anexos/leitura nativa e worker
> documental, memória versionada e análise documental durável integrados; calibração de OCR, geração extensa e avaliação
> com modelo real pendentes.
>
> **Atualizado em:** 2026-11-12
>
> **Base:** [escopo e decisões de produto](README.md).

## Detalhamento técnico

As especificações abaixo concretizam esta visão de arquitetura. Quando houver
diferença de detalhe, prevalecem os contratos versionados das especificações:

1. [Dados, índices e autorização](especificacoes/01-dados-e-autorizacao.md).
2. [API, estados, eventos e execução durável](especificacoes/02-api-e-execucao.md).
3. [Editor, ancoragem, comparação e aplicação](especificacoes/03-editor-e-aplicacao.md).
4. [IA, documentos, OCR, memória e limites](especificacoes/04-ia-documentos-e-limites.md).
5. [Etapas e matriz de validação](especificacoes/05-entrega-e-validacao.md).
6. [JSON Schema e exemplos de contrato](contratos/README.md).
7. [Diagramas de componentes, aprovação e dados](diagramas.md).
8. [Conferência deste detalhamento](evidencias/detalhamento-tecnico-2026-11-11.md).

Frontend atual conferido em `../JuridicoAi-frontend`, commit `7700c51`. A
indisponibilidade da cópia antiga não é mais uma pendência. A integração
transacional possui testes PostgreSQL; calibração de OCR e medições de custo
ainda não foram executadas.

Atualização: além da [prova local do editor](evidencias/e0-editor-2026-11-11.md),
há [revisão persistida e aplicação no editor real](evidencias/e1-revisao-persistida-2026-11-11.md).
Também há [histórico persistente usado no contexto](evidencias/e1-e3-historico-2026-11-11.md),
com geração ainda ligada à requisição e limite explícito de janela.
O recorte de [conversa livre](evidencias/e3-conversa-livre-2026-11-11.md) reutiliza
o provedor existente, com intenção e grupos em uma saída estruturada, sem chamada
separada de classificação nem gravação pelo modelo. Ainda não implementa o loop
de ferramentas, worker e arquitetura conversacional completa descritos abaixo.

O [recorte documental](evidencias/e2-documentos-2026-11-11.md) acrescenta extração
nativa com OpenXml/Poppler, fila documental com leases e contexto autorizado.
O pipeline Tesseract está testado, mas sua calibração não aprovou rollout.

## 1. Direção arquitetural

Evoluir a API ASP.NET Core existente como aplicação modular. Não é necessário
separar conversa, propostas e documentos em microserviços nesta entrega.

| Camada | Responsabilidade |
|---|---|
| Frontend | Chat, upload, progresso, comparação no editor e envio da aprovação |
| API autenticada | Autorização, mensagens, consultas, aprovação e exclusão |
| Orquestrador conversacional | Contexto, chamadas ao modelo, consultas autorizadas e geração em etapas |
| Processador documental | Extração, OCR, avaliação de leitura e trechos consultáveis |
| Serviço de propostas | Operações tipadas, comparação, versões e aplicação determinística |
| Worker | Processamento durável de documentos, gerações e exclusões autorizadas |
| PostgreSQL | Histórico, propostas, trechos, trabalhos, eventos e consumo |
| Armazenamento de arquivos | Originais privados, imagens intermediárias e derivados |

O worker pode começar como `BackgroundService` no mesmo processo, consumindo
trabalhos persistidos no PostgreSQL. Seu contrato deve permitir execução em
processo separado posteriormente, sem substituir o modelo de dados.

O MongoDB e o scraper permanecem no fluxo atual de jurisprudências, fora desta
alteração. O assistente usa a integração de IA existente por uma interface
ampliada. O detalhamento mantém o modelo configurável e propõe extração/OCR
locais; versões e qualidade precisam ser validadas na etapa E0.

## 2. Modelo de dados proposto

Nomes indicativos, a ajustar ao padrão de implementação.

| Entidade | Dados essenciais |
|---|---|
| `AssistenteConversa` | OrganizationId, UserId, PecaId, estado, sequência, datas |
| `AssistenteMensagem` | ConversaId, autor, conteúdo, sequência, estado, ClientRequestId |
| `AssistenteAnexo` | ConversaId, mensagem de origem, StorageId, hash, MIME, tamanho, estado de processamento |
| `AssistenteDocumentoProcessado` | Origem privada ou DocumentoCaso, versão/hash da origem, versão do extrator |
| `AssistenteTrechoDocumento` | DocumentoProcessadoId, página/localização, texto, qualidade, elegibilidade, motivo de exclusão |
| `AssistenteMemoria` | ConversaId, resumo, fatos/decisões com referências, sequência coberta e versão |
| `AssistenteProposta` | ConversaId, mensagem/execução, revisão, versão e snapshot-base da Peça, estado |
| `AssistenteAlteracao` | PropostaId, grupo de dependência, alvo, original/hash, substituição tipada, decisão |
| `AssistenteExecucao` | ConversaId, mensagem, estado, orçamento, progresso e erro técnico |
| `AssistenteTrabalho` | Tipo, execução/origem, estado, tentativas, lease e checkpoint |
| `AssistenteEvento` | ConversaId, ExecucaoId, sequência, tipo e dados de produto para reconexão |
| `AssistenteExclusao` | Autorização do titular, estado da remoção e confirmação das etapas |

Uma restrição única garante uma conversa ativa por OrganizationId + UserId +
PecaId. Excluir e começar outra gera uma identidade nova, sem recuperar conteúdo
da anterior. Sequências de mensagens e chaves de idempotência são persistidas.

Relacionamentos de Peça para conversa não usam exclusão em cascata. Excluir ou
arquivar a Peça não apaga o chat. Filtros de autorização continuam obrigatórios
mesmo que exista uma referência válida no banco.

Documentos privados e documentos do Caso mantêm origens distintas. Uma extração
de DocumentoCaso pode ser reaproveitada por versão do arquivo, sempre condicionada
ao acesso ao Caso. Anexos privados e derivados não são compartilhados entre chats,
nem por deduplicação que exponha conteúdo ou existência de arquivos.

## 3. Autorização e fronteiras do contexto

O servidor deriva usuário e organização do token, nunca do corpo. Cada consulta
ao chat, download, busca, evento, proposta e trabalho verifica titularidade.

O contexto permitido contém somente:

1. A conversa corrente e seus anexos privados elegíveis.
2. A versão atual da Peça, caso o usuário mantenha acesso.
3. Dados, contextos e documentos autorizados do mesmo Caso.

Uma conversa privada não dá acesso às outras conversas da mesma Peça. Referências
a fontes também não podem expor anexo privado a outro usuário. Mudanças aprovadas
são compartilhadas como conteúdo da Peça, sem anexar ao documento um link público
para a conversa de origem.

Permissões são verificadas novamente no processamento em segundo plano e antes
de publicar resultados ou aplicar mudanças. Revogação de acesso interrompe novas
operações sobre a Peça, sem apagar automaticamente o histórico retido.

Está aprovada a leitura do histórico privado pelo titular autenticado mesmo após
perda de acesso à Peça. Implementar uma área de histórico independente da abertura
do editor, com autorização por titularidade para consulta e exclusão. Consultar
conteúdo já persistido na conversa não exige acesso atual à Peça; operar sobre o
documento ou atualizar o contexto exige autorização vigente.

Após revogação, a área é somente de consulta, além da exclusão manual autorizada:
não permite enviar novos pedidos sobre a Peça, executar ferramentas, aplicar
propostas ou carregar novamente dados/fontes compartilhadas do Caso. Referências
históricas não podem funcionar como links que contornem a revogação. As rotas
de consulta devem separar essas permissões das rotas de geração e aplicação.

## 4. Mensagens, execução e eventos

1. O frontend sincroniza o editor pelo autosave antes de iniciar um pedido de
   edição. Se houver erro, o chat continua disponível para conversa, mas não
   apresenta proposta como baseada numa versão sincronizada.
2. A API autentica, verifica permissões e grava mensagem + execução + trabalho
   na mesma transação. Um ClientRequestId repetido recupera a mesma execução.
3. O worker adquire o trabalho com lease no banco. Uma execução por conversa é
   processada de cada vez, mantendo ordem das mensagens. Outros chats continuam
   independentes, sujeitos aos limites por usuário e organização.
4. O orquestrador lê um snapshot consistente do contexto e executa as etapas.
5. Resposta, referências, proposta e eventos finais são persistidos. O cliente
   recebe progresso por SSE e consegue retomar eventos pela última sequência.
6. Fechar o painel ou perder a conexão não perde o trabalho aceito. Cancelar é
   uma ação explícita, com interrupção da execução, preservando histórico e
   rascunhos incompletos devidamente identificados.

Estados de execução: `NaFila`, `PreparandoContexto`, `AguardandoDocumentos`,
`Gerando`, `Validando`, `Concluida`, `Incompleta`, `Falhou`, `Cancelada`.

Eventos propostos: `mensagem.aceita`, `documento.progresso`, `execucao.progresso`,
`resposta.delta`, `proposta.disponivel`, `execucao.concluida`, `execucao.falhou`.
Texto parcial do chat não é uma proposta aplicável. Só a proposta validada e
persistida habilita aprovação. Caso o provider não permita exposição incremental
adequada, o progresso permanece disponível sem simular streaming de texto.

Trabalhos são retomáveis por checkpoint. Chamadas externas podem ser repetidas
após falha e precisam ter orçamento de tentativas; não prometer execução única
no provedor. Publicação de mensagens e aplicação de mudanças devem ser idempotentes.

## 5. Montagem de contexto e memória

Substituir o montador de pedido isolado por um `AssistenteContextAssembler`:

- Instruções de sistema e permissões de ferramentas.
- Pedido corrente e últimas mensagens relevantes.
- Memória versionada, com referência às mensagens que sustentam fatos e decisões.
- Proposta corrente quando o pedido se refere a uma mudança pendente.
- Snapshot da Peça atual com estrutura e versão identificadas.
- Dados do Caso e trechos documentais confiáveis pertinentes ao pedido.

Para a primeira entrega, a busca pode usar texto e metadados em PostgreSQL,
com trechos por página/seção. Busca semântica é uma evolução a avaliar com casos
reais, não uma dependência presumida nem motivo para introduzir outro banco.

Consulta autorizada vem antes da recuperação do conteúdo. Os trechos recuperados
devem manter origem e localização. Resumo ou índice nunca torna elegível um trecho
reprovado na leitura. Mensagens e documentos são dados não confiáveis perante as
instruções do sistema; conteúdos não podem conceder ferramentas ou permissões.

O resumo preserva histórico integral, registra até qual sequência foi elaborado
e é atualizado após correções. Informações conflitantes permanecem identificadas;
texto atual da Peça, mensagens históricas e propostas rejeitadas não são fundidos
como se representassem a mesma versão aprovada.

O backend monta um manifesto das fontes e versões utilizadas por execução,
permitindo explicar o que embasou a resposta sem registrar conteúdo em logs.

## 6. Processamento de arquivos e OCR

Pipeline proposto:

1. Receber arquivo em armazenamento privado e registrar sua origem e estado.
2. Validar tamanho, tipo efetivo, formato e limites de processamento.
3. Extrair texto e estrutura diretamente quando disponíveis.
4. Executar OCR nas páginas/imagens que necessitem de reconhecimento.
5. Avaliar qualidade por trecho e registrar cobertura por página.
6. Disponibilizar somente trechos aprovados à busca/contexto.

Estados: `Recebido`, `NaFila`, `Extraindo`, `ExecutandoOcr`, `Avaliando`,
`Disponivel`, `Parcial`, `SemConteudoConfiavel`, `Falhou`.

A avaliação combina sinais do extrator, estrutura do texto, cobertura, caracteres
inválidos e verificações de campos materiais como datas, nomes e valores.
Confiança numérica de OCR isolada, ou avaliação do próprio modelo sobre sua
resposta, não basta para declarar leitura confiável.

Os limiares devem ser calibrados com documentos de teste; não há como garantir
ausência total de erros apenas com uma pontuação. Trechos duvidosos ficam fora do
contexto, com localização e motivo visíveis ao usuário. Fontes não devem ser
silenciosamente reconstituídas a partir de respostas anteriores.

Para pedidos que dependem de anexos ainda em processamento, a execução aguarda
as fontes necessárias ou comunica a falta. Não gera uma minuta alegando considerar
documentos ainda não lidos. Uso de partes disponíveis deve explicitar a cobertura.

OCR fica atrás de `IDocumentOcrProvider`, e extração atrás de
`IDocumentTextExtractor`. Biblioteca/provedor e infraestrutura serão escolhidos
após avaliação de qualidade, custo e condições de tratamento dos documentos.

## 7. Liberdade do chat e ferramentas internas

O usuário envia mensagens, não `AssistenteModo`. O orquestrador permite ao modelo
responder ou solicitar operações internas tipadas, por exemplo:

- Consultar a Peça atual.
- Consultar dados e fontes autorizadas do Caso e do chat.
- Preparar minuta.
- Propor inserção, substituição ou remoção de conteúdo.
- Ajustar uma proposta pendente.

O backend resolve IDs, valida parâmetros e executa consultas. Não são ferramentas
de código arbitrário ou acesso livre à rede. Nenhuma ferramenta disponível ao
modelo aprova ou grava a Peça. A mutação é reservada ao serviço de aplicação
acionado pela aprovação autenticada do usuário.

A interface `IAssistenteIaProvider` deve evoluir para suportar mensagens,
resultados/ferramentas tipados e eventos, preservando adaptador do provedor e
contagem de tokens. O contrato atual de uma única sugestão isolada não deve ser
esticado artificialmente para simular conversas. O prompt e avaliações são
versionados novamente; baseline anterior não aprova a nova configuração.

## 8. Propostas e comparação no editor

A proposta é separada da resposta conversacional. Guarda revisão, snapshot-base,
versão da Peça e operações tipadas. Cada operação registra:

- Tipo: inserir, substituir, remover ou criar minuta.
- Alvo estrutural e conteúdo original esperado.
- Hash do original e versão-base.
- Conteúdo proposto em subconjunto validado do schema do editor.
- Grupo de dependência, explicação e estado de decisão.

Não confiar em posições absolutas de caracteres geradas pelo modelo. O backend
fornece identificadores de blocos do snapshot e resolve os alvos. Na v1 esses
aliases são locais à versão do snapshot, sem adicionar IDs persistentes ao
ConteudoJson existente. O contrato concreto usa SpliceBlocos e versionamento,
conforme a especificação do editor. IDs estáveis entre versões ficam para uma
eventual evolução de rebase, sem serem dependência desta entrega.
Modificações dentro de blocos precisam de correspondência exata com o original,
preservando marcas e nós fora do escopo da operação.

O diff visual é calculado pela aplicação a partir do original e da proposta,
não escrito livremente pelo modelo. O frontend usa uma camada de revisão que
não altera `ConteudoJson` enquanto a proposta está pendente. Não salvar textos
riscados ou marcações de revisão como conteúdo real da Peça.

Grupos dependentes são aprovados/rejeitados juntos. Ao ajustar uma proposta,
criar nova revisão e invalidar aprovações ainda não aplicadas da revisão antiga.
Mudanças já aplicadas não são reaplicadas na revisão seguinte.

## 9. Aprovação e persistência atômica

Recomenda-se um endpoint específico de aplicação. Não usar dois comandos
independentes, um para salvar a Peça e outro para marcar a proposta como aplicada:
uma falha intermediária deixaria o estado inconsistente.

Fluxo proposto:

1. O usuário aprova os grupos que acabou de visualizar. O frontend conclui o
   autosave e envia IDs dos grupos, revisão da proposta, versão atual da Peça
   e chave de idempotência. Não manda texto arbitrário como conteúdo aprovado.
2. O servidor verifica titularidade, acesso de edição, estado da proposta,
   dependências, revisão e correspondência dos alvos.
3. O serviço determinístico aplica somente os grupos aprovados ao documento.
   A aprovação não chama o modelo nem reinterpreta o pedido.
4. Na mesma transação PostgreSQL, atualiza a Peça com versão esperada e registra
   decisões e recibo de aplicação. Se a versão mudou, retorna conflito sem
   gravar parcialmente. Uma chave repetida devolve o mesmo recibo.
5. O frontend recebe documento canônico e nova versão, aplica a transação
   correspondente no editor e confirma o estado salvo.

Durante esse curto commit, o editor bloqueia novas mutações ou as enfileira com
reconciliação explícita; nunca sobrescreve digitação ocorrida durante a chamada.
Na perda de conexão, consulta o recibo antes de tentar aplicar novamente.

Reaproveitar a regra de atualização com versão do `PecaRepository` e extrair uma
operação transacional compartilhada com o salvamento existente. O novo caminho
acrescenta atomicidade da decisão da proposta; o autosave manual continua usando
o fluxo normal. O recibo não expõe mensagens/anexos privados a outros usuários.

Undo/redo no editor deve incluir a aplicação aprovada como uma transação coerente.
Desfazer grava uma nova versão pelo salvamento normal, sem tornar a proposta
automaticamente pendente de novo. O registro histórico pode dizer que houve
aplicação mesmo que o conteúdo seja posteriormente editado ou desfeito. Persistir
a pilha de undo entre recarregamentos não está implícito nesta entrega.

Após aplicação parcial, os grupos restantes só continuam aprováveis se o servidor
comprovar correspondência dos seus alvos com a nova base. A atualização deve
preservar exatamente o conteúdo mostrado ou exigir nova revisão. Na primeira
entrega, alterações externas concorrentes podem invalidar a proposta inteira
para regeneração; não é necessário implementar rebase automático.

## 10. Contrato HTTP indicativo

As rotas novas usam `/assistente/v2` e coexistem com o contrato atual. A relação
completa, autenticação pessoal e erros estão na
[especificação HTTP](especificacoes/02-api-e-execucao.md); os payloads críticos
estão em [contratos](contratos/README.md). Não utilizar os nomes indicativos da
primeira versão deste desenho como contrato de implementação.

O envio de mensagens pode retornar `202` com IDs persistidos. A aprovação retorna
o recibo e a versão salva; conflito não vira aplicação automática em outro alvo.
A existência de um ID nunca dispensa autorização em cada endpoint.

## 11. Geração extensa, falhas e orçamento

Geração longa passa por plano interno de seções, geração e validação de cada
parte, montagem e checagem de coerência. Checkpoints permitem retomar sem pedir
“continue”. Rascunhos parciais são persistidos como incompletos e não habilitam
aprovação de minuta integral.

Definir limites configuráveis para upload, páginas, OCR, contexto, saída, número
de ferramentas/etapas, tentativas, duração e consumo por execução/organização.
Agregação de consumo deve somar todas as chamadas, incluindo resumo e geração
em etapas. Interrupção por orçamento informa o trabalho restante.

Coordenação de execução e idempotência devem sair do registro apenas em memória
para o PostgreSQL. Limites de uso entre instâncias também precisam de coordenação
antes de operar múltiplas réplicas. Um lease de trabalho pode expirar para permitir
retomada; isso não significa expirar ou excluir o histórico da conversa.

## 12. Exclusão autorizada e retenção

Após autorização do titular, marcar a conversa como em exclusão e bloquear novas
operações. Cancelar trabalhos e invalidar resultados tardios por versão/estado da
conversa. Nenhum worker pode recriar derivados de uma conversa em exclusão.

Um trabalho durável remove originais privados, extrações, OCR, memória, propostas,
eventos com conteúdo, índices e caches associados. Falhas são retomadas e exibidas
como exclusão pendente, não concluída. Metadados técnicos sem conteúdo podem
registrar a execução da solicitação conforme política definida.

Documentos do Caso e texto aprovado na Peça não são removidos. A política de
backups precisa impedir que restauração reative conteúdo cuja exclusão foi
autorizada, com registro mínimo das exclusões e reaplicação antes da disponibilização.
Prazos de remoção de cópias de backup ainda precisam ser definidos, sem alegar
eliminação física imediata de todas as cópias.

Não há job de expiração de chats por inatividade nem exclusão em cascata por
arquivamento/exclusão da Peça.

## 13. Migração e validação

1. Adicionar tabelas, armazenamento privado e contratos novos sem remover os atuais.
2. Implementar conversa persistente e execução durável, com isolamento por titular.
3. Integrar extração/OCR, elegibilidade e recuperação das fontes.
4. Implementar orquestrador e contratos novos do provider.
5. Implementar proposta, diff e aplicação transacional no editor correto.
6. Validar a jornada completa com dados fictícios e ativar por organização.
7. Descontinuar o contrato antigo somente após migrar os clientes e validar regressões.

Não existe histórico persistido atual a migrar no backend. Mensagens que existem
somente em memória de uma interface antiga não podem ser consideradas recuperáveis.

Testes prioritários: isolamento entre chats na mesma Peça; documentos privados
ausentes da busca alheia; qualidade documental; retomada de execução; correção de
memória; conflito de versão; aprovação parcial/dependências; falha entre commit e
resposta; undo e autosave; exclusão durante geração; reconexão; respostas incompletas.

Avaliações da IA precisam cobrir continuidade, fidelidade aos documentos,
preservação do texto existente, ferramentas permitidas e mudanças sem aprovação.
Testes determinísticos não substituem avaliação semântica nem validação manual
da comparação no editor.

## 14. Validações pendentes de implementação

- Calibrar os critérios propostos com corpus fictício e fixar versões de OCR/extratores.
- Provar diff, preservação de formatação e undo com os splices no TipTap atual.
- Executar os contratos de ferramentas e avaliações com o modelo configurado.
- Medir limites, completar preços e configurar orçamento financeiro do piloto.
- Validar busca e cobertura antes de adicionar busca semântica.
- Provisionar armazenamento persistente e validar rotina de backup/exclusão.

As decisões de engenharia e contratos estão nas especificações. Essas validações
não são perguntas de produto pendentes nem funcionalidades já entregues.

Este documento propõe a arquitetura; não instala bibliotecas, escolhe serviços
externos, cria migrations ou inicia a implementação.

Atualização: [memória versionada e recuperação do histórico](evidencias/e2-memoria-conversa-2026-11-12.md)
implementadas atrás de flag, preservando originais e revalidando proveniência.

A [análise documental extensa durável](evidencias/e2-analise-documental-duravel-2026-11-12.md)
preserva lotes/sínteses, consolida e prepara o resultado do pedido em segundo plano.
A durabilidade geral da geração e minutas por seções continuam abertas.
