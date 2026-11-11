# Corpus documental sintético

Todos os nomes, valores e fatos são fictícios. Nenhum arquivo veio de um usuário.

- `native.pdf`: texto nativo com nome, valor e data.
- `columns.pdf`: duas colunas paralelas; associação de leitura deve ser reprovada.
- `mixed.pdf`: texto nativo e imagem com fatos/valores; imagem não pode desaparecer
  silenciosamente da cobertura quando OCR estiver desligado.
- `protected.pdf`: cópia protegida por senha, apenas para testar rejeição.
- `scan.png`, `blurred.png`, `rotated.png`: texto impresso limpo, desfocado e a
  180 graus; anotações em `expected.json`.

PDFs gerados com ReportLab 5.1.0/PyPDF 7.0.1 e imagens com Pillow 13.0.0,
usando DejaVu Sans local; o PDF misto foi renderizado e conferido visualmente.
Não representam um corpus suficiente para aprovar OCR externo, manuscritos,
carimbos, tabelas complexas ou documentos jurídicos reais.

Os testes nativos requerem Poppler. Testes de OCR real usam as variáveis
`JURIDICOAI_TEST_TESSERACT` e `JURIDICOAI_TEST_TESSDATA` (modelos por+eng).
A medição independente é reproduzível com `tools/documents/calibrate_ocr.py`.
