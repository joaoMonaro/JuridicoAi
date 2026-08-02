# Revisão da documentação anterior do scraper

Os documentos canônicos e revisados do scraper estão nesta pasta:

- [Visão geral e contratos](visao-geral.md)
- [CLI e códigos de saída](cli.md)
- [Fluxo técnico do TJSP](integracao-tjsp.md)
- [Solução de problemas](../operacao/troubleshooting.md)

Eles substituem, como referência de manutenção, o texto histórico em
`src/JuridicoAI.Scrapper/docs/fluxo-scraper-tjsp.md`.

## Correções incorporadas

A revisão confrontou a documentação anterior com o código e registrou:

- diferença entre o HTML bruto de cada item e a primeira página completa;
- teto efetivo de 200 resultados;
- paginação fixa de 20 itens;
- códigos de saída e separação entre `stdout` e `stderr`;
- valores fixados pela integração da API;
- comportamento tolerante do endpoint de atualização de captcha;
- campos reais do payload, parser e contratos;
- dependências frágeis da estrutura atual do e-SAJ;
- ausência de retentativa, backoff e detecção de bloqueio.

O arquivo `src/JuridicoAI.Scrapper/docs/inicializacao-tjsp.txt` é apenas um
comando histórico de execução e não é a referência operacional atual.
