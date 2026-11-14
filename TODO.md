# TODO — Projeto JuridicoAI

Atualizado em **2026-11-14**. Acompanhamento geral das prioridades do projeto.
O detalhamento de cada feature permanece no seu próprio TODO.

## Pendências

- [ ] **PROJ-001 — Testar e corrigir a latência da IA. Prioridade: `medium` (média).**

Na avaliação reduzida, uma consulta simples sobre a minuta aprovada levou
**63,941 segundos**, enquanto as demais interações ficaram entre 5,300 e
22,993 segundos. O tempo medido é o da interação completa; a causa da demora
ainda não foi identificada.

Próximas ações:

1. Reproduzir a consulta e medir separadamente fila, preparação do contexto,
   contagem de tokens, chamada ao provedor e publicação da resposta.
2. Identificar o gargalo e corrigir esperas desnecessárias comprovadas.
3. Comparar os tempos antes/depois e verificar que contexto, aprovação e
   execução durável continuam corretos, começando por testes locais.
4. Registrar diagnóstico, alterações e resultados. Novas chamadas pagas exigem
   orçamento definido; não há SLA de latência aprovado.

Detalhamento e critérios de conclusão: **M01** no
[TODO do assistente conversacional](implementacoes/features/assistente-conversacional/TODO.md#melhorias-evidenciadas-na-avaliação-reduzida).
Origem: [avaliação real reduzida](implementacoes/features/assistente-conversacional/evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).

## Acompanhamento por feature

- [Assistente conversacional — entregas, validações e melhorias](implementacoes/features/assistente-conversacional/TODO.md).
