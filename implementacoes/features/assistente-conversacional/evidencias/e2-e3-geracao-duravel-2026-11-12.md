# Geração durável e minutas por seções — 2026-11-12

Etapa única implementada no backend e em `../JuridicoAi-frontend`, atrás de
`AssistenteIa:DurableGenerationEnabled`, desativada por padrão. A implantação e a
avaliação de qualidade com o modelo real não fazem parte desta evidência.
OCR permanece desativado e em stand by.

## Comportamento entregue

O chat v2 salva o pedido, a resposta reservada e a geração na mesma transação,
antes de preparar contexto ou chamar o provedor. Encerra o SSE com
`solicitacao.enfileirada`; fechar a tela ou a conexão não cancela o processamento.
O histórico devolve `geracaoId`, e o frontend consulta progresso, plano,
seções concluídas, análise documental associada, cancelamento e retomada.
Uma proposta concluída enquanto o usuário acompanha o pedido abre a revisão.

Perguntas e alterações pontuais passam pelo provedor existente. Para uma minuta
integral, a ferramenta `planejar_minuta` define as seções; o worker solicita e
valida cada seção em `registrar_secao`, salvando os blocos TipTap completos e um
sumário seletivo. Não há botão “continuar” nem aprovação intermediária. O pedido
original, os dados selecionados do Caso, a Peça e a proveniência do histórico são
preservados entre execuções. Consultas auxiliares ficam como dados não confiáveis;
raciocínio interno e identificadores de conversas hospedadas não são persistidos.

A revisão recebe lotes de seções completas, o plano, o pedido e sumários das outras
partes. `revisar_minuta` precisa devolver os IDs e o hash exatos do lote. Todos os
lotes precisam passar; uma rodada de correção pode reescrever as partes apontadas,
recebendo as pendências e o rascunho anterior. Essa verificação é de completude e
coerência, não uma comprovação de mérito jurídico ou de fidelidade a fontes que
não estejam no contexto da revisão.

Somente depois disso o servidor monta **uma proposta integral**, com um grupo de
substituição do documento. As seções internas não se tornam propostas parciais.
A comparação e a aprovação visual existentes continuam sendo a única forma de
alterar a Peça. Falha, limite, cancelamento ou contexto desatualizado preservam o
pedido e os checkpoints, sem aplicar partes incompletas.

## Persistência, retomada e integração

- Migration aditiva `20261112150839_AddDurableAssistantGeneration`, tabelas
  `ai_generations`/`ai_generation_steps` e referência da mensagem. FKs restritivas
  e rollback bloqueado quando há gerações/checkpoints preservados.
- Um pedido ativo por conversa, idempotência pelo identificador e hash da entrada,
  claim com `SKIP LOCKED`, lease e validação do fence ao salvar/publicar.
- Cada chamada física é reservada antes do envio e registrada no ledger. Uma
  chamada sem checkpoint semântico confirmado interrompe a execução após queda;
  não existe repetição paga automática. A retomada explícita preserva consumo e
  partes confirmadas, inclusive quando a resposta do provedor foi recebida mas o
  checkpoint não chegou a ser confirmado.
- Resultado normal confirmado pode ser republicado após falha transacional sem
  outra chamada ao modelo. Publicação da mensagem/proposta e conclusão da geração
  são atômicas. Cancelamento invalida o fence e impede publicação tardia.
- Usuário, Membership, organização, recursos habilitados, modelo/política, versão
  da Peça, dados selecionados do Caso, proposta referenciada e fontes são
  revalidados. O conjunto documental fica fixado no aceite, inclusive durante a
  extração; arquivos anexados depois não entram silenciosamente no pedido.
- `analisar_fontes` delega à análise documental durável já existente. O mesmo pedido
  espera os lotes e a síntese; a análise devolve o controle ao worker de geração,
  que retoma o plano/rascunho. Cancelamento e retomada da análise delegada passam
  pela geração principal. Seu consumo integra o orçamento do pedido, incluindo
  reservas sem ledger confirmado; cobertura `Todos` não pode ser rebaixada.
- Memória versionada é reutilizada entre as fases. O pedido atual fica separado
  das mensagens anteriores na proveniência, e uma compactação já confirmada não
  é repetida a cada seção.

## Contratos e limites

Endpoints autenticados por titular e organização:

- `GET /assistente/v2/conversas/{conversaId}/geracoes/{id}`.
- `POST .../geracoes/{id}/cancelamento`: 204.
- `POST .../geracoes/{id}/retomada`: 202, somente execução interrompida e ainda
  elegível dentro dos limites originais.

O DTO informa `estado`, `secoesConcluidas`, `secoesTotal`, `secoes`, `codigoErro`
e `analiseId`. A mensagem usa `AguardandoGeracao` antes de iniciar, preservando os
estados de conclusão e falha anteriores. O evento terminal do transporte é
`solicitacao.enfileirada`, não uma confirmação de conclusão da geração.

Padrões: até 8 seções, 12 chamadas físicas e 180 mil tokens por pedido, 30 minutos
de prazo, lease de 180 segundos, 20 mil caracteres JSON por seção e 120 mil no
corpo final. Mantêm-se os limites menores de contexto, entrada, saída por chamada,
extração e análise documental. Uma minuta ou revisão que não caiba nesses limites
é encerrada explicitamente; o servidor não trunca texto nem eleva o orçamento.
Os 120 mil caracteres são um teto de montagem, não garantia de que toda minuta
nesse tamanho caiba no contexto de uma chamada posterior.

A flag exige conversa automática, histórico v2 e revisão v2. Implantar a migration
antes de habilitá-la. Com a flag desligada o fluxo anterior continua disponível;
o endpoint v1 independente permanece ligado à requisição. Não houve ativação em
ambiente do usuário, nova dependência externa ou chamada à OpenAI real.

## Validação executada

- Backend: 273 testes aprovados e 1 teste de OCR ignorado por runtime local
  indisponível. Inclui 13 novos testes PostgreSQL de geração; reexecução focada
  após a revisão final dos bloqueios de contexto.
- Frontend: 127 testes unitários, build TypeScript/Vite e lint aprovados.
- Playwright: 25 testes focados de conversa livre, histórico e revisão persistida
  aprovados. Nova prova cobre reload enquanto a mensagem está `Gerando`, revisão
  final, retomada/cancelamento e aprovação única. Não representa execução integral
  de toda a suíte E2E do produto.
- API HTTP real + PostgreSQL real + provedor HTTP simulado: extração TXT, aceite
  persistido, análise delegada, plano, três seções, revisão e proposta de 25.903
  caracteres. Sete chamadas físicas registradas no ledger (2.100 tokens de entrada,
  560 de saída). Conexão SSE encerrada antes do processamento, histórico com duas
  mensagens, replay sem nova geração e Peça alterada somente após aprovação.
- Testes usam portas livres atribuídas dinamicamente, sem ocupar 5173. Serviços
  descartáveis são encerrados após a validação.

Ainda pendentes: avaliação jurídica/factual com modelo real, orçamento financeiro
operacional, área pessoal de histórico após perda de vínculo e exclusão manual
com autorização. Essas pendências não foram declaradas concluídas por esta etapa.
