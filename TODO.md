# TODO — Projeto JuridicoAI

Atualizado em **2026-11-14**. Acompanhamento geral das prioridades do projeto.
O detalhamento de cada feature permanece no seu próprio TODO.

## Pendência com alta prioridade

- [ ] implementacao do fluxo total do assistente conversasional (definido na feature assistente-conversacional)

## Pendências

- [x] **PROJ-001 — Testar e corrigir a latência da IA. Prioridade: `medium` (média).**

Na avaliação reduzida, uma consulta simples sobre a minuta aprovada levou
**63,941 segundos**, enquanto as demais interações ficaram entre 5,300 e
22,993 segundos. O tempo medido é o da interação completa; os registros originais
não separaram as etapas da demora.

**Concluído em 2026-11-14:** diagnóstico, correção, instrumentação e repetição
real com GPT-6.1 Terra. A consulta passou de **63,941 s para 22,697 s** nesta
comparação. O cenário completo de três interações passou nos checks técnicos,
com custo calculado de **US$ 0,03260130**. Ver
[evidência real](implementacoes/features/assistente-conversacional/evidencias/m01-latencia-real-2026-11-14.md).

A geração no provedor levou 2,555 s; a espera dominante foi de aproximadamente
18,905 s até a admissão pelo limite local. Essa política remanescente está em
**M03 (medium)** no TODO da feature, separada da correção de reagendamento.

Verificações realizadas:

1. Consulta reproduzida e etapas medidas separadamente.
2. Reagendamento excessivo e consumo de cota em negações corrigidos/testados.
3. Tempos comparados com o modelo real, sem elevar cotas ou reduzir contexto.
4. Diagnóstico/resultados registrados. Não há SLA aprovado; novas rodadas pagas
   continuam exigindo orçamento definido.

Detalhamento: [M01/M03 no TODO da feature](implementacoes/features/assistente-conversacional/TODO.md#melhorias-evidenciadas-na-avaliação-reduzida).
A melhoria foi observada em uma rodada e não garante a latência de todo pedido.

## Acompanhamento por feature

- [Assistente conversacional — entregas, validações e melhorias](implementacoes/features/assistente-conversacional/TODO.md).
