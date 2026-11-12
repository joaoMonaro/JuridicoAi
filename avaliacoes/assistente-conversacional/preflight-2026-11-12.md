# Preflight — 2026-11-12

Solicitada avaliação real do assistente conversacional, na baseline de aplicação
`b634f3be78081038bc7b2a899e25e53f538fed57` e frontend
`50aa43937ce92c688cc98c03383a422c7ff968cf`.

## Constatado

- A suíte anterior de 30 casos (`qualidade-juridica` 1.0.3) exercita respostas
  isoladas do provedor; não mede sozinha os novos fluxos persistidos.
- Não foi encontrada chave OpenAI nem modelo configurado nas variáveis próprias
  da aplicação ou nos User Secrets compartilhados da API/avaliador.
- Não há tabela de preços ou teto financeiro para esta rodada configurados.
- Os segredos de outros serviços não foram exibidos, usados ou transmitidos.

## Preparado

Suíte `conversa-v2-1.0.0`: sete cenários, nove interações fictícias. Inclui geração,
correção/aprovação de proposta, consulta posterior, documentos, memória longa,
correção recente, leitura duvidosa, instrução maliciosa e chat de outro titular.

Runner em `tools/conversation-evals/`, com API/PostgreSQL descartáveis e reserva de
custo antes de cada chamada. Preflight não inicia esses serviços sem a configuração
necessária. A validação local verifica a suíte, o orçamento, a barreira do proxy e
a separação entre aprovação automática de checks e revisão jurídica pendente.

**Chamadas reais: 0. Custo de geração incorrido por esta preparação: US$ 0.**
Nenhum resultado de modelo foi produzido ou aprovado, nenhuma configuração de IA
do produto foi ativada e nenhum registro real foi alterado.

## Para executar

Configurar a chave localmente, informar o modelo e o teto total em US$. Depois,
conferir os preços atuais na fonte oficial, executar o preflight e iniciar a rodada
limitada. As perguntas de configuração foram encaminhadas ao responsável; a
solicitação de avaliação não foi tratada como autorização de gasto ilimitado.

OCR segue em stand by. Backup, rollout e homologação jurídica permanecem nos seus
recortes próprios; não foram avaliados por este preflight.
