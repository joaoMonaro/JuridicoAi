# Análise documental extensa com resumos e checkpoints

Data: 2026-11-12. Implementação local em backend e frontend, atrás de flag.
Complementa [recuperação documental](e2-recuperacao-documental-2026-11-11.md) e
[memória da conversa](e2-memoria-conversa-2026-11-12.md). OCR permanece em stand by.
Nenhum deploy, commit, push ou alteração do banco/configuração do usuário.

## Comportamento entregue

Pedidos que exigem o conjunto inteiro de documentos podem acionar `analisar_fontes`
na conversa natural. O backend fixa as fontes/versões autorizadas do pedido e
assume o trabalho em segundo plano. A análise lê lotes, salva sínteses com citações,
consolida os resultados e prepara a resposta/proposta do mesmo pedido. Não cria
uma mensagem de usuário a cada lote nem exige que a pessoa envie “continue”.

Depois de receber `solicitacao.aguardando`, a tela pode ser fechada. O worker
continua e a resposta fica salva para reabertura. O painel recupera andamento por
arquivo, permite cancelar e abre a comparação quando recebe uma proposta ao
acompanhar a análise. Aprovação visual continua sendo a única aplicação na Peça.

Consultas localizadas mantêm a busca/leitura seletiva existente. A intenção natural
continua sendo interpretada pelo modelo, sem classificador regex. A ferramenta de
análise só aceita cobertura Todos; a continuação final mantém esse escopo no servidor,
mesmo se a saída do modelo tentar rebaixá-lo. Esse fluxo continua sujeito à avaliação
semântica do modelo configurado antes de liberação.

## Organização técnica

- `AssistenteDocumentAnalysisService`: autorização, snapshot do conjunto, fila,
  consulta/cancelamento/retomada, orçamento acumulado e reutilização da análise.
- `AssistenteDocumentAnalysisWorker` e `AssistenteDocumentAnalysisProcessor`:
  trabalho durável em PostgreSQL, com claim por `FOR UPDATE SKIP LOCKED`, lease e
  token de exclusividade por tentativa. Nenhuma transação fica aberta durante IA.
- `AssistenteAnaliseDocumental`: identidade do titular/conversa, pedido/resposta
  originais, manifesto/hash, política/modelo, cursores, síntese final, prazo e lease.
- `AssistenteAnaliseLote`: entrada do lote, referências aos originais, resumo,
  reserva/liquidação de tokens e relação entre lotes de consolidação.
- `AssistenteBackgroundIdentity`, `AssistenteExecutionAuthContext` e correlação
  por execução: identidade interna limitada ao escopo do worker. Não fabricam JWT
  nem sessão; o worker revalida Membership e usuário/organização ativos no banco.

Fluxo: NaFila → Lendo → Consolidando → Pronta → PreparandoResposta → Concluida.
Os terminais alternativos são Incompleta, Interrompida, Cancelada e Desatualizada.
A resposta do chat usa AguardandoAnalise durante o processamento. O índice único
passa a incluir Gerando e AguardandoAnalise, impedindo outro pedido ativo naquela
conversa. Outros chats continuam independentes.

## Leitura, síntese e consolidação

Cada lote lê até 12 trechos completos de uma fonte, respeitando o limite de
caracteres. O cursor avança somente com o checkpoint confirmado. Um novo processo
pode continuar pelos lotes já salvos. Trecho que excede o espaço disponível produz
limite explícito; não há corte silencioso nem promoção de leitura duvidosa.

O provedor chama uma função estrita `registrar_sintese_documental`, usando o contrato de
[Function calling](https://developers.openai.com/api/docs/guides/function-calling). Uma chamada
produz a síntese; não existe uma chamada adicional só para verbalizar que ela foi
salva. Até 20 registros podem conter Fato, Valor, Data, Contradicao ou Lacuna,
cada um com texto e uma a quatro citações literais de 4 a 300 caracteres. O servidor
valida trechoId, pertencimento ao lote, citação e tamanho; insere documento, página/
localizador e hash da origem. O modelo só indica trechos do lote fornecido; metadados e hashes são preenchidos pelo servidor.

A síntese é genérica sobre as fontes, sem depender da tese do pedido. A política
pede preservação de nomes, valores, datas, ressalvas e conflitos sem resolução.
Essas instruções e referências não comprovam fidelidade semântica: a síntese é
seletiva e ainda precisa de avaliação com modelo real.

Se as sínteses acumuladas não couberem no tamanho final, o worker consolida grupos
de lotes em novas sínteses. Os filhos e suas referências permanecem salvos. Uma
consolidação só pode citar as origens literais dos filhos recebidos; não usa texto
de resumo como se fosse citação do documento. O número de raízes diminui a cada
consolidação, dentro do mesmo orçamento máximo.

Na preparação final, o chat recebe a síntese consolidada e o catálogo congelado.
Pode consultar `buscar_fontes`/`ler_fonte` para detalhes, valores, citações ou
contradições que não constem da síntese. Uma análise concluída pode ser reutilizada
na mesma conversa quando manifesto, política, modelo e permissões continuam válidos.
Não se compartilha resumo privado entre titulares ou conversas. Um novo pedido
explícito também pode aproveitar lotes confirmados de uma análise que parou por
limite/interrupção, desde que o conjunto e suas versões continuem exatamente iguais.
Somente checkpoints confirmados são copiados, com referências ao lote anterior;
a entrada bruta não é duplicada. Essas etapas não geram novas chamadas de síntese
nem são contabilizadas como consumo novo. O novo pedido mantém seu próprio orçamento,
sem renovação automática do orçamento do pedido anterior.

## Cobertura e elegibilidade

`trechosAnalisados` registra os trechos processados nas etapas confirmadas;
`trechosConsultados` continua registrando os originais fornecidos diretamente ao
modelo da resposta atual. A interface distingue essas duas medidas. Nenhuma delas
comprova compreensão ou correção jurídica.

Trechos excluídos, fontes parciais/inutilizáveis e falhas de leitura continuam
impedindo declarar cobertura integral. Processar todos os trechos elegíveis não
elimina regiões ausentes ou duvidosas. A política existente bloqueia proposta
integral aplicável quando o pedido Todos não tem cobertura completa.

Permissões, transparência, manifesto e qualidade são revalidados antes de enviar
conteúdo ao contador/provedor, antes dos checkpoints e na publicação. Remoção ou
reprocessamento de fonte invalida o trabalho dependente. Fonte adicionada depois
não entra naquele conjunto. Alteração da Peça interrompe a preparação final sobre
a versão antiga; nenhuma proposta é aplicada automaticamente sobre uma versão nova.

Síntese, proposta e mensagem final são publicadas com proteções de exclusividade;
a conclusão do trabalho e a resposta/proposta são confirmadas na mesma transação.
Cancelamento ou substituição da tentativa impedem publicação tardia.

## Quedas, retomada e limites

Lote Preparado ainda sem chamada paga pode ser retomado automaticamente. Lote
Chamando com lease expirado é considerado indeterminado: não se repete a chamada
paga automaticamente. O trabalho fica Interrompido e os lotes concluídos continuam
salvos. A retomada explícita usa o mesmo pedido, mantém esses checkpoints e conserva
a reserva pessimista da tentativa sem confirmação. Ela depende de permissões,
fontes, prazo e orçamento ainda válidos.

A preparação da resposta final também é marcada antes da geração. Uma queda nessa
fase não provoca outra geração paga automática. A parte ainda indeterminada fica
reservada conservadoramente. Se não houver orçamento/prazo para retomar, o servidor
informa o impedimento; não aumenta limites automaticamente.

Rate/concurrency limits locais, detectados antes de iniciar uma chamada, reagendam
a etapa sem consumir uma geração. Falhas HTTP do provedor não recebem repetição
paga automática. O ledger é obrigatório neste fluxo: falha de persistência não é
silenciada. Cada chamada física possui seu lançamento; não existe um segundo
lançamento agregado para duplicar consumo. O orçamento inclui as chamadas anteriores
do pedido, lotes, consolidações e preparação final, com reservas antes das chamadas.

Defaults em `AssistenteDocumentos`:

| Configuração | Default |
|---|---|
| AnalysisEnabled | false; exige Enabled e RetrievalEnabled |
| MaxAnalysisCalls | 12 chamadas de geração por pedido, incluindo fases anteriores/final |
| MaxAnalysisTotalTokens | 180.000 de entrada + saída |
| AnalysisBatchCharacters | 24.000 |
| AnalysisSummaryCharacters | 8.000 |
| AnalysisOutputTokens | 2.000 por síntese, limitado também pelo máximo global |
| AnalysisLeaseSeconds | 180; deve exceder TimeoutSeconds global + 30 |
| AnalysisDeadlineSeconds | 600; prazo operacional, sem excluir dados |

Também valem os limites globais de entrada, saída, contexto, timeout, rate e
concorrência. Não foi implementado orçamento financeiro distribuído por organização.
Reservas aqui limitam tokens do pedido; preços ausentes continuam desconhecidos.
O worker inicial executa uma etapa por vez; capacidade e desempenho precisam ser
medidos no piloto, sem promessa de prazo ou leitura ilimitada.

## API, persistência e ativação

- SSE `solicitacao.aguardando`: terminal válido daquela conexão, com andamento;
  não equivale a uma resposta jurídica concluída.
- Mensagens persistidas: `analiseDocumentalId` permite recuperar o trabalho.
- `GET /assistente/v2/conversas/{conversaId}/analises/{id}`: andamento privado.
- `POST .../analises/{id}/cancelamento`: interrompe preservando documentos/checkpoints.
- `POST .../analises/{id}/retomada`: retomada explícita quando ainda permitida.

Migration `20261112130954_AddDurableDocumentAnalysis`: tabelas
`ai_document_analyses`/`ai_document_analysis_batches`, vínculo opcional composto na
mensagem, FKs restritivas, índices e unicidade por resposta. `prompt_version` passa
a 256 caracteres para registrar a composição das políticas sem truncamento.
Downgrade que apagaria dados ou versões de política existentes é bloqueado.
Rollback funcional desativa AnalysisEnabled e preserva o material já salvo.

Ativação exige migrations, frontend com o novo SSE, AnalysisEnabled, documentos/
recuperação, flags de revisão/histórico/conversa livre, assistente global, feature
AI_ASSISTANT e transparência vigente. Minuta/edição continuam exigindo as respectivas
features. Nenhuma flag foi ativada no ambiente do usuário. OCR continua desligado.

## Evidências locais

| Verificação | Resultado |
|---|---|
| Backend completo com PostgreSQL real | 260 passaram, 1 teste local de OCR ignorado por ausência do runtime |
| Vitest | 126 passaram |
| Playwright: conversa livre, histórico e revisão persistida | 23 passaram |
| Build de backend/frontend e lint | Passaram; aviso de bundle >500KB já existente |
| EF has-pending-model-changes | Sem mudanças pendentes |
| API e PostgreSQL reais com provedor simulado | Fluxo de fila, worker, síntese, proposta, replay e aprovação passou |

A integração HTTP usou um TXT de 117.000 bytes, 37 trechos elegíveis, seis lotes de
leitura e uma consolidação. O servidor concluiu o pedido após terminar a conexão
SSE, mantendo somente duas mensagens no chat. Foram nove chamadas físicas: intenção,
seis lotes, consolidação e resposta final. Ledger: nove linhas, 2.700 tokens de
entrada e 720 de saída sintéticos, com versão composta de política de 108 caracteres.
Replay não gerou novas chamadas. A Peça permaneceu na versão original até a
aprovação, que alterou somente o primeiro parágrafo e preservou o segundo.
API/provedor locais utilizaram portas dinâmicas 35585/40339, sem usar 5173.

Os testes também cobrem novas instâncias entre etapas, workers concorrentes,
retomada de chamada incerta, reaproveitamento após pedido limitado, cancelamento durante chamada, publicação tardia,
invalidação de fonte/Peça, perda de Membership, citações inválidas, limite, prazo,
reagendamento antes de chamar IA, ledger obrigatório e execução sem HttpContext.
O fluxo real revelou e corrigiu uma dependência de correlação HTTP no worker.
No frontend, ações 202 sem corpo usam apiFetch; isso também corrige reprocessamento
documental que tentava interpretar uma resposta vazia como JSON.

Não houve chamada à OpenAI nem calibração de OCR. A fidelidade das sínteses,
interpretação de pedidos, conflitos e qualidade jurídica ainda exigem avaliação
com o modelo real. Síntese seletiva não assegura retenção de todo detalhe factual.

## Escopo que permanece aberto

Este recorte torna duráveis a análise documental e a preparação final do pedido
que entrou nesse fluxo. A geração comum antes do agendamento, ou sem análise
extensa, continua ligada à requisição. Geração de minutas extensas por seções,
checkpoints gerais da geração, área pessoal após perda de vínculo, exclusão
manual autorizada e liberação após avaliação real permanecem separadas.
