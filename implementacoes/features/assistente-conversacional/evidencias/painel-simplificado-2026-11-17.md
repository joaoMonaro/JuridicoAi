# Painel simplificado e contexto por conversa

Decisões aplicadas em **2026-11-17**, após validação da jornada pelo responsável
pelo produto. Este registro não substitui a avaliação jurídica nem declara deploy.

## Comportamento

- Uma caixa de mensagem, anexos e controles de execução/revisão. Retirados os
  atalhos que preenchem prompts, modos, escopos, menu de contexto, dados do Caso,
  contextos cadastrados, informação paralela por pedido e botão Desenvolver.
- A interface confirma o salvamento antes de enviar. A referência da geração é
  a versão salva da Peça, a própria conversa e os anexos vinculados a ela.
- Seleção e cursor estão pausados em novos envios e reenvios, inclusive nos
  pedidos históricos. Leitura de históricos e propostas antigas preservada.
  Pendência **PROJ-006** no [TODO geral](../../../../TODO.md) e no
  [TODO da feature](../TODO.md); também registrada na raiz do frontend.
- O painel requer conversa livre habilitada para novos envios. Sem a capacidade,
  informa indisponibilidade e mantém o histórico consultável, sem voltar aos
  controles antigos.
- Dados e documentos antigos do Caso não entram no catálogo/contexto inicial.
  Sua consulta ocorre pela ferramenta `consultar_caso`, mediante pedido expresso
  na mensagem atual, com citação literal do pedido e acesso vigente ao mesmo Caso.
- A ferramenta preserva proveniência e validação de fontes, incluindo contexto
  tipado/hash dos dados do Caso e snapshots documentais. O pedido original e seu
  hash de idempotência não são reescritos. Fontes alteradas ou revogadas invalidam
  respostas derivadas e propostas dependentes; histórico visível é preservado.
- Documentos do Caso ainda em processamento recebem resposta informativa, sem
  proposta. A fila é mantida e a consulta pode ser reenviada após a leitura.
- A política livre foi versionada para orientar perguntas diante de ambiguidade
  material e avanço útil com lacunas explícitas, sem inventar fatos. Aprovação
  visual continua necessária para modificar a Peça.

## Verificações concluídas

- Backend: build sem avisos/erros e suíte completa com **298 aprovados, zero
  falhas e 1 ignorado**, total 299. O teste ignorado exige runtime OCR, fora do
  escopo ativo. PostgreSQL 17 descartável, sem usar bancos ou volumes existentes.
- Regressões de contexto: fontes automáticas da própria conversa, consulta
  explícita de dados mesmo sem leitura documental habilitada, proveniência,
  histórico antigo/novo, revogação, idempotência e retomada em outro worker.
- Frontend: **134 testes unitários/componentes**, lint e build aprovados;
  permanece o aviso anterior de bundle acima de 500 kB.
- Playwright: **56 cenários do assistente aprovados** na rodada combinada final,
  incluindo aprovação, reload, teclado, transparência, permissões, retomada,
  cancelamento e bloqueios de salvamento/conflito.
- Uma falha intermitente no cenário de retomada ocorreu na primeira rodada
  combinada; não se repetiu nas três execuções isoladas de diagnóstico nem na
  rodada combinada final. Não houve alteração de produto para ocultá-la.
- O setup do teste de exclusão pessoal passou a preparar explicitamente uma
  fonte compartilhada antiga; suas verificações de preservação foram mantidas.
- Links locais dos documentos alterados e `git diff --check` verificados nos
  dois repositórios. O painel também foi inspecionado visualmente.

## Limites da validação

Os testes usam dados fictícios, PostgreSQL descartável e respostas de provedor
simuladas. Não houve chamada paga à IA nem validação semântica com modelo real.
A aderência do modelo à nova política continua sujeita à avaliação real/revisão
humana do MVP. Não foram alteradas flags de produção ou credenciais locais.

## Remoção da flag de conversa livre

A pedido do produto, `AutomaticConversationEnabled` foi removida das opções,
configuração versionada e validações de runtime. Conversa natural é o padrão do
fluxo persistido; chaves antigas, inclusive com valor `false`, são ignoradas.
Habilitação geral (`AssistenteIa:Enabled` e controle operacional), permissões da
organização e infraestrutura de revisão/histórico permanecem necessárias. A
capacidade HTTP `suportaConversaLivre` continua para informar disponibilidade e
compatibilidade da API; não corresponde a uma configuração de modo de chat.

Verificação após a retirada: build sem avisos/erros, **301 testes de backend
aprovados e 1 de OCR ignorado**, 134 unitários de frontend e seis E2Es focados
de disponibilidade/permissões aprovados, com lint/build do frontend aprovados.
Um teste de regressão usa configuração antiga com a chave em `false` e confirma
capacidade e geração conversacional normais. Startup real contra banco
descartável também passou com essa chave antiga em `false` e com documentos,
memória e geração durável habilitados, sem chamada ao provedor. Scripts de
avaliação/smoke e documentação atual foram ajustados para não exigir a chave.

## Limpeza do transporte do frontend

O envio usa um contrato próprio `AssistantInteractionRequest` e conversa
identificada obrigatória. `streamAssistantInteraction` envia diretamente para
`/interacoes`; os dois caminhos alternativos do frontend e a conversão de
formato antigo foram retirados. O painel monta diretamente mensagem, versão,
anexos e referência de proposta. Campos antigos não são enviados nem em retries.

`AssistantRequest` continua disponível para leitura de pedidos persistidos;
endpoints legados no backend e estruturas de propostas não foram retirados nesta
limpeza. Streaming, sinal de cancelamento e proteção de salvamento continuam no
mesmo fluxo. `captureEditorContext` permanece preservado conforme PROJ-006.

Verificação deste recorte: 136 unitários de frontend, lint/build e 56 cenários
combinados de Playwright aprovados. O teste de transporte verifica conversa
obrigatória, formato atual, streaming e encaminhamento do sinal de cancelamento;
testes do painel/histórico conferem que seleção e contexto antigos não voltam em
reenvios. Uma falha inicial de reabertura mostrou o painel fechado durante
consultas de disponibilidade canceladas; o editor agora ignora resultados de
consultas canceladas/substituídas. O cenário passou em três execuções isoladas
e na rodada combinada final. Nenhum endpoint ou dado do backend foi removido.
