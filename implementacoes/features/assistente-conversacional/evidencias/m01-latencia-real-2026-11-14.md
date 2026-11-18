# M01 — Confirmação com GPT-6.1 Terra

Teste autorizado em **2026-11-14**, com chave temporária e teto de **US$ 1,00**.
Executado sobre a baseline `98db3ad` com as correções/instrumentação de
[M01](m01-latencia-local-2026-11-14.md) deste recorte, cujos hashes foram
registrados no manifest da execução.

## Escopo e resultado

Somente **CONV-001**, com as três interações originais: minuta inicial, correção
com aprovação explícita da fixture e consulta sobre a versão aprovada. Os outros
cenários não foram repetidos. Foram preservados modelo, prompts, limites de
contexto/tokens/cotas e geração por três seções; não foi usado provedor simulado.

As três interações passaram nos checks técnicos, sem avisos do verificador.
Nenhuma Peça foi alterada antes da aprovação da fixture no banco fictício. Não
houve proposta de alteração na consulta final. Revisão jurídica formal continua
pendente e os cenários extensos adiados não foram homologados por este teste.

| Interação | Rodada de 2026-11-12 | Rodada de 2026-11-14 |
|---|---:|---:|
| Gerar a minuta | 22,993 s | 24,013 s |
| Corrigir nome/valor e preparar nova proposta | 20,658 s | 19,154 s |
| Consultar nome/valor após aprovação | **63,941 s** | **22,697 s** |

Na consulta, a redução observada foi de **41,244 s, aproximadamente 64,5%**.
É uma comparação entre duas execuções do cenário, não um benchmark estatístico
nem garantia de latência para toda conversa.

Pergunta: “Qual é o nome da contratante e o valor da restituição na minuta
aprovada? Só responda, sem propor alterações.” Resposta: “A contratante é Beatriz
Santos, e o valor da restituição solicitada é de R$ 750,00.”

## O que a instrumentação demonstrou

| Medição da consulta final | Valor |
|---|---:|
| Primeira aquisição do job, desde sua criação | 715,698 ms |
| Última aquisição, quando a capacidade ficou disponível | **18.904,957 ms** |
| Reagendamentos por `AI_USER_RATE_LIMITED` | **9**, cada um com 2.000 ms |
| Contagem de tokens de entrada | 640,041 ms |
| Preparação na tentativa admitida, incluindo contagem | 649,478 ms |
| Cabeçalhos da geração | 686,504 ms |
| Geração no provedor, incluindo cabeçalhos e leitura/validação | **2.555,306 ms** |
| Publicação | 17,704 ms |
| Processamento da tentativa admitida | 3.275,936 ms |

Os tempos aninhados não devem ser somados. Do lado do cliente, enfileiramento
levou 9,721 ms; espera pelo terminal, 22.658,275 ms; recuperação, 29,155 ms.
O avaliador consulta o job em intervalos de 500 ms, também presentes no total.

A espera dominante nesta rodada foi **local**, antes da contagem e da chamada
ao modelo. As duas minutas anteriores consumiram cinco etapas cada; a consulta
aguardou a reposição da cota de dez operações/minuto. A correção evitou agendar
mais uma janela inteira de 60 s: as rechecagens ocorreram em intervalos de cerca
de 2 s até a capacidade voltar. Isso confirma o mecanismo no fluxo real atual.

Ainda existe espera legítima pela política de cotas. O teste não elevou limites
nem mudou a unidade de admissão de etapa interna para mensagem do usuário.
Rever essa política é uma melhoria separada, M03, antes de prometer consultas
rápidas após gerações extensas. Os artefatos antigos não permitem decompor com
precisão os 63,941 s da rodada anterior.

## Consumo e encerramento

- **11 chamadas de geração**, todas com uso conhecido; zero retries pagos.
- Entrada total: **37.311 tokens**, sendo **14.326 em cache**; saída: **1.780**.
- Custo calculado total: **US$ 0,03260130**, conciliado com o ledger do produto.
  Somente a chamada da pergunta custou US$ 0,00187915.
- Preços padrão reconferidos na [documentação oficial](https://developers.openai.com/api/docs/models/gpt-6.1-terra):
  US$ 1/milhão de entrada, US$ 0,05/milhão em cache e US$ 5/milhão de saída.
  Custo calculado por `usage`, sem consulta à fatura da conta.
- API/PostgreSQL e proxy descartáveis, somente dados fictícios, portas livres,
  sem ocupar 5173. Todos encerrados ao final. OCR continuou desativado.
- Chave recebida por entrada oculta e mantida apenas em memória; não persistida
  em configuração, Git ou artefatos. Verificação não encontrou texto no formato
  da chave de projeto nos arquivos gerados. O responsável foi avisado de que já
  podia revogar a chave temporária.

Artefatos locais ignorados pelo Git: execução `20261114-151516-803d4c`, incluindo
`results.json`, `latency.json`, `usage.json`, ledger e manifest com hashes da
suíte/avaliador. [Relatório local](../../../../avaliacoes/assistente-conversacional/execucoes/20261114-151516-803d4c/report.md).
O script foi o avaliador 1.1.0 com `--case CONV-001 --model gpt-6.1-terra
--max-cost-usd 1.00 --max-calls 32`, usando os preços acima.

**M01 concluído no recorte de diagnóstico, correção e comparação real.** P03
continua como próximo desenvolvimento; M03 acompanha a política da espera
remanescente, sem alterar limites automaticamente.
