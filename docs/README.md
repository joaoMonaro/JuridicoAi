# Documentação do JuridicoAI

Esta documentação descreve o comportamento implementado atualmente na solução
`JuridicoAI.sln`.

## Visão geral

- [Arquitetura e fluxos](arquitetura.md)
- [Configuração e execução](configuracao-e-execucao.md)

## Backend

- [API HTTP](backend/api.md)
- [Ingestão e reprocessamento de jurisprudências](backend/ingestao-jurisprudencias.md)
- [Normalização e classificação](backend/classificacao.md)
- [Persistência no MongoDB](backend/persistencia.md)
- [Geração de petições com OpenAI](backend/geracao-peticoes.md)

## Scraper

- [Visão geral](scraper/visao-geral.md)
- [Uso pela linha de comando](scraper/cli.md)
- [Integração técnica com o TJSP](scraper/integracao-tjsp.md)

## Operação

- [Solução de problemas e limitações conhecidas](operacao/troubleshooting.md)

## Escopo atual

O projeto contém:

- uma API ASP.NET Core em .NET 10;
- um scraper do portal e-SAJ/TJSP, também em .NET 10;
- persistência em MongoDB;
- geração de texto por meio da Responses API da OpenAI;
- classificação local de jurisprudências por regras.

Não há, no estado atual do repositório, suíte automatizada de testes,
autenticação/autorização, OpenAPI/Swagger, observabilidade distribuída ou
contêiner da aplicação. O `docker-compose.yml` provisiona somente o MongoDB.
