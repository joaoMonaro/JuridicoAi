# Avaliação do assistente conversacional atual

Esta suíte complementa a baseline de respostas isoladas em `../qualidade-juridica`.
Ela exercita a aplicação atual: API, banco, extração nativa, memória, ferramentas,
fila, seções e propostas. Não reutiliza a aprovação da baseline antiga como
aprovação do produto novo.

## Cenários e estado atual

`conversa-v2-1.0.0` possui sete cenários e nove interações, somente fictícios:

1. Minuta inicial → correção de nomes/valores → aprovação no banco descartável →
   pergunta sobre a versão aprovada.
2. Leitura de todos os documentos fornecidos, síntese e minuta por seções.
3. Recuperação de fato antigo em mais de mil mensagens, fora da janela recente.
4. Correção recente prevalecendo sobre fatos antigos da memória.
5. Leitura parcialmente duvidosa, excluída e sinalizada.
6. Instrução maliciosa em documento tratada como dado não confiável.
7. Isolamento de um anexo de outro usuário na mesma Peça.

Para a primeira rodada, o responsável definiu **GPT-6.1 Terra e teto total de
US$ 1,00**, com [seleção reduzida de quatro cenários/seis interações](rodada-1usd.md).
A rodada reduzida foi **executada**: seis interações passaram nos checks técnicos,
com 18 chamadas e custo calculado de **US$ 0,04549805**. Ver
[resultados e limitações](../../implementacoes/features/assistente-conversacional/evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).
A avaliação ampla e a homologação jurídica continuam pendentes. A chave foi usada
apenas temporariamente e o responsável foi avisado de que pode revogá-la. A suíte
completa permanece disponível para uma avaliação posterior com orçamento próprio.
Em **2026-11-14**, somente CONV-001 foi repetido com a instrumentação 1.1.0 e
correções de M01: três interações aprovadas, 11 chamadas, custo de US$ 0,03260130.
A consulta passou de 63,941 s para 22,697 s nessa comparação. Ver
[resultado e divisão dos tempos](../../implementacoes/features/assistente-conversacional/evidencias/m01-latencia-real-2026-11-14.md).
Essa execução não amplia a cobertura aos cenários extensos adiados.
OCR permanece desligado; nenhum caso depende da calibração em espera.

## Validação local, sem API

Na raiz do backend:

```bash
python3 tools/conversation-evals/run.py validate
python3 -m unittest discover -s tools/conversation-evals -p 'test_*.py' -v
python3 tools/conversation-evals/run.py preflight
```

`preflight` retorna código 2 e lista a configuração ausente, sem iniciar banco,
aplicação ou chamadas ao provedor. `validate` não carrega credenciais.

## Preparação da rodada real

- Configurar `AssistenteIa:ApiKey` em User Secrets da API ou na variável
  `AssistenteIa__ApiKey`/`OPENAI_API_KEY`, fora do Git e do chat.
- Informar o modelo explicitamente (`--model` ou configuração local) e o teto
  total em US$. Não substituir silenciosamente um modelo por outro.
- Conferir os preços atuais do modelo na documentação oficial antes de passar os
  valores de entrada, entrada em cache e saída. A URL da fonte é gravada; o script
  não declara os valores corretos apenas por receber uma URL oficial.
- Ter Docker com contexto `default`, imagem PostgreSQL 17 e .NET 10 disponíveis.

A configuração local do avaliador dá precedência às variáveis de ambiente sobre
User Secrets. A aplicação de avaliação recebe uma chave fictícia para o proxy
local; somente o proxy usa a chave real, enviada à API oficial por HTTPS. Não há
leitura ou modificação do banco do usuário.

Exemplo de comando, a preencher **somente depois** de confirmar modelo, orçamento
e preços:

```text
python3 tools/conversation-evals/run.py run --confirm-real-run \
  --model <modelo-escolhido> --max-cost-usd <teto-total> --max-calls 80 \
  --input-usd-per-million <preco> --cached-usd-per-million <preco> \
  --output-usd-per-million <preco> \
  --pricing-source <url-oficial-conferida>
```

`--case CONV-001` seleciona um cenário; pode ser repetido. A flag de execução
representa a autorização já dada pelo responsável, não uma confirmação a cada
mensagem. A seleção e o teto devem ser registrados antes da rodada.

## Funcionamento e custo

O runner inicia uma API e PostgreSQL novos, cria contas/Casos/Peças fictícios e
preserva os limites de produção declarados: 12 chamadas/180 mil tokens por pedido,
60 mil tokens de entrada e 6 mil de saída por chamada, 80 mil caracteres de
contexto. Habilita os fluxos novos somente no processo descartável. Não altera
flags, segredos, modelos ou registros do ambiente de trabalho do usuário.

O proxy só admite `Responses` e contagem de tokens, com modelo fixo e ferramentas
fechadas do produto. Exige contagem da entrada exata antes de cada geração,
`store=false` e truncamento desativado. Reserva entrada integral sem desconto de
cache e saída máxima, usando preços explicitamente fornecidos. Bloqueia uma
chamada que excederia o teto estimado ou a quantidade máxima de chamadas.

Consumo desconhecido mantém a reserva e interrompe a rodada; não há repetição
paga automática. Divergência de uso/preço/contagem exige auditoria, não ajuste
silencioso do orçamento. O teto refere-se a esta execução pelos preços conferidos;
não é uma alteração do limite financeiro da conta OpenAI nem uma garantia sobre
outras aplicações que usem essa conta.

A aprovação de uma proposta em `CONV-001` é uma ação explícita da fixture em um
banco descartável, para testar o commit. O modelo não aprova e nenhuma Peça real
é alterada. Os demais cenários verificam ausência de mudança antes de aprovação.

## Artefatos e avaliação

`execucoes/<data-id>/` é ignorado pelo Git e contém:

- `manifest.json`: modelo, commit, estado do working tree, hashes do avaliador e
  da suíte, seleção, limites e fonte de preços.
- `suite.json`, `results.json`, `report.md`: entradas/saídas fictícias, critérios,
  falhas objetivas e espaço para revisão.
- `usage.json`: reservas, uso conhecido, incertezas e custo estimado por chamada.
- `physical-calls.json`: nomes de ferramentas e hashes, sem argumentos livres ou
  raciocínio interno do provedor.
- `product-ledger.json`: conciliação com o ledger técnico da aplicação.
- `latency.json` (avaliador 1.1.0): eventos técnicos permitidos de etapas,
  aquisição e espera local, com IDs de correlação; sem prompts ou respostas.
- `api-operational.log`: log protegido da API descartável, sem logging sensível.

O avaliador 1.1.0 acrescenta `clientTimingMs` aos resultados (enfileiramento,
espera pelo terminal e recuperação) e `duration_ms`/data/correlação ao ledger.
Habilita console JSON e Information somente nas categorias de diagnóstico da API
isolada. Os tempos de etapas aninhadas se sobrepõem: não somá-los. Log antigo ou
categoria desabilitada produz medição indisponível, sem inventar tempo zero.

Prova HTTP da instrumentação **sem chave ou chamada externa**:

```bash
python3 tools/smoke-tests/assistant-latency-http.py
```

Ela inicia API/PostgreSQL descartáveis e usa transporte de IA em memória com
atrasos conhecidos; não mede a velocidade do GPT-6.1 Terra. Ver
[diagnóstico local M01](../../implementacoes/features/assistente-conversacional/evidencias/m01-latencia-local-2026-11-14.md).

Não são gravados chaves, cookies, JWTs, sessões hospedadas ou raciocínio interno.
Uma saída gerada pode conter referência inventada: revisar os artefatos antes de
adicionar explicitamente qualquer baseline ao Git. Falha de execução ou orçamento
impede declarar cobertura integral da suíte.

As regras verificam o corpo efetivamente proposto, não os blocos originais ou
alertas que repetem um fato ausente. Correspondência textual não comprova sentido,
completude ou correção jurídica. Preencher a [rubrica](rubric.md) e distinguir
falhas de geração, limitações de formato do verificador e avaliação semântica.
Nenhum resultado automático aprova comercialização ou homologação jurídica.

Metodologia: objetivos, casos representativos/adversariais, avaliação de etapas e
combinação de verificações com julgamento humano, conforme as
[orientações oficiais de avaliação da OpenAI](https://developers.openai.com/api/docs/guides/evaluation-best-practices).
Esta implementação usa a API de geração do produto, sem depender da plataforma
hospedada de Evals.
