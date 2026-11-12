# Calibração do OCR — stand by

Estado: em espera por orientação do responsável pelo produto em 2026-11-11.
Motivo: concentrar o trabalho na conclusão da implementação atual do assistente
conversacional com documentos nativos. Retomada depende de nova priorização.

O motor reconhece texto, mas os critérios atuais rejeitaram todos os trechos da
amostra inicial, inclusive leituras corretas. O OCR permanece desativado por padrão.
Imagens e regiões digitalizadas devem continuar sinalizadas como não lidas;
texto nativo de um PDF misto pode ser aproveitado com aviso de leitura parcial.

## Trabalho preservado para retomada

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

## Referências

- [Implementação documental](../../features/assistente-conversacional/evidencias/e2-documentos-2026-11-11.md).
- [Medição inicial](../../features/assistente-conversacional/evidencias/documentos/calibracao-ocr-inicial-2026-11-11.json).
- [Script de calibração](../../../tools/documents/calibrate_ocr.py).
- [Prioridades atuais](../../../TODO.md).
