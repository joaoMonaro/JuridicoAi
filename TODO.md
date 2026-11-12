# TODO — JuridicoAI

Lista de acompanhamento do assistente conversacional. Atualizada em 2026-11-12.
O escopo completo permanece no [plano de implementação](implementacoes/features/assistente-conversacional/especificacoes/05-entrega-e-validacao.md).

## Prioridade atual — Concluir o assistente conversacional sem OCR

Por orientação do responsável pelo produto em 2026-11-11, a calibração do OCR
fica em stand by para dar continuidade à implementação atual. A leitura nativa
de PDF, DOCX e TXT permanece no escopo ativo. Imagens e regiões digitalizadas
continuam sinalizadas como não lidas enquanto o OCR estiver desativado.

Recorte entregue: recuperação documental por ferramentas integrada à conversa,
com cobertura e limites explícitos. Veja as [evidências](implementacoes/features/assistente-conversacional/evidencias/e2-recuperacao-documental-2026-11-11.md).
Entregue também: [memória versionada e recuperação do histórico](implementacoes/features/assistente-conversacional/evidencias/e2-memoria-conversa-2026-11-12.md), com originais preservados e correções no contexto.
Entregue também: [análise documental extensa durável](implementacoes/features/assistente-conversacional/evidencias/e2-analise-documental-duravel-2026-11-12.md), com sínteses por lote, consolidação, andamento e preparação final do mesmo pedido.
Próximo recorte: ampliar a geração de minutas extensas por etapas e a durabilidade dos demais fluxos de geração.

Pendências do escopo ativo:

- [x] Integrar recuperação documental à conversa, com busca/leitura por etapas e indicação de cobertura, dentro dos limites da execução.
- [x] Ampliar leitura integral de conjuntos extensos com resumos e checkpoints, preservando cobertura/proveniência e limites explícitos.
- [x] Implementar resumo versionado e busca no histórico para conversas extensas, com proveniência, invalidação e indicação no frontend.
- [ ] Completar execução durável da geração e minutas por seções; a análise documental e sua preparação final já usam fila/checkpoints/retomada.
- [ ] Implementar área pessoal de histórico após perda de vínculo e exclusão manual autorizada.
- [ ] Avaliar continuidade, fidelidade às fontes e qualidade jurídica com o modelo real antes da liberação.

## Stand by — Calibração do OCR

- [ ] Retomar a calibração de imagens e documentos digitalizados quando essa frente voltar a ser priorizada.

O detalhamento e os critérios de conclusão foram preservados no
[registro de OCR em espera](implementacoes/standBy/calibracao-ocr/README.md).
O pipeline permanece implementado e desativado; esta pausa não declara suporte
entregue a digitalizações nem conclusão da calibração.
