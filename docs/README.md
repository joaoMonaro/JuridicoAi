# Documentação do JuridicoAI

Referência enxuta do comportamento implementado na solução `JuridicoAI.sln`
(.NET 10, PostgreSQL 17, OpenAI Responses API).

## Visão geral e execução

- [Arquitetura e fluxos](arquitetura.md)
- [Configuração, segredos e execução](configuracao-e-execucao.md)
- [Versionamento e fluxo de branches](versionamento.md)

## Backend

- [API HTTP — mapa completo de endpoints](backend/api.md)
- [Autenticação e multitenancy](backend/autenticacao-e-multitenancy.md)
- [Fundação do caso](backend/casos.md)
- [Peças, templates e exportação Word/PDF](backend/pecas.md)
- [Assistente de IA (fluxo conversacional v2 e legado)](backend/assistente-ia.md)
- [Coleção Postman](backend/JuridicoAI.postman_collection.json)

## Operação

- [Configurar a IA — ativação, flags e tarifas](operacao/configuracao-ia.md)
- [Exportação Word e PDF — LibreOffice e fontes](operacao/exportacao-word-pdf.md)
- [Monitoramento do assistente (P04)](operacao/monitoramento-assistente.md)
- [Solução de problemas e limitações](operacao/troubleshooting.md)

## Planejamento, testes e operação auxiliar

- [Escopo e critérios do MVP](../MVP.md)
- [TODO geral do projeto](../TODO.md)
- [Preparação para produção e pendências abertas](../implementacoes/producao-e-pendencias.md)
- [Ensaio local de publicação](../ops/rehearsal/README.md)
- [Stack local de monitoramento](../ops/monitoring/README.md)
- [Avaliações de IA (conversacional, qualidade e segurança)](../avaliacoes/assistente-conversacional/README.md)

> O scraper do TJSP e o fluxo antigo de jurisprudências foram extraídos para
> `../JuridicoAI.Scrapper` e não fazem parte desta API.
