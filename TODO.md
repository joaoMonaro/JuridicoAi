# TODO — JuridicoAI

Lista de acompanhamento do assistente conversacional. Atualizada em 2026-11-11.
O escopo completo permanece no [plano de implementação](implementacoes/features/assistente-conversacional/especificacoes/05-entrega-e-validacao.md).

## Prioridade 1 — Calibrar o OCR

- [ ] Calibrar e validar o OCR antes de liberar leitura de imagens e documentos digitalizados no produto.

O motor reconhece texto, mas os critérios atuais rejeitaram todos os trechos da
amostra inicial, inclusive leituras corretas. O OCR permanece desativado por padrão.

Trabalho necessário:

- [ ] Validar a medição e ampliar o corpus representativo, com transcrição correta e marcação de nomes, valores, datas e identificadores.
- [ ] Separar amostras de calibração das usadas na validação final.
- [ ] Comparar preparação das imagens e configurações de reconhecimento.
- [ ] Medir erros de leitura, erros aceitos como confiáveis e conteúdo correto descartado, por categoria de documento.
- [ ] Ajustar os critérios com base nas medições e verificar a preservação do sentido nas regiões parcialmente legíveis.
- [ ] Registrar a política calibrada, versões, hashes dos modelos, resultados e limitações.

Critério de conclusão: demonstrar cobertura útil e qualidade em amostras não
usadas no ajuste, conferir campos materiais dos trechos aceitos e manter regiões
duvidosas excluídas e sinalizadas. Reduzir limiares ou passar testes de execução,
isoladamente, não conclui a calibração.

Referências: [implementação documental](implementacoes/features/assistente-conversacional/evidencias/e2-documentos-2026-11-11.md),
[medição inicial](implementacoes/features/assistente-conversacional/evidencias/documentos/calibracao-ocr-inicial-2026-11-11.json)
e [script de calibração](tools/documents/calibrate_ocr.py).

## Outras pendências já definidas

Sem prioridade relativa definida nesta lista:

- [ ] Integrar recuperação documental extensa à conversa, com indicação de cobertura.
- [ ] Implementar resumo versionado e busca no histórico para conversas extensas.
- [ ] Implementar execução durável da geração, checkpoints e retomada.
- [ ] Implementar área pessoal de histórico após perda de vínculo e exclusão manual autorizada.
- [ ] Avaliar continuidade, fidelidade às fontes e qualidade jurídica com o modelo real antes da liberação.
