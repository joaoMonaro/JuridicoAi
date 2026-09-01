# Documentação do JuridicoAI

Esta documentação descreve o comportamento implementado atualmente na solução
`JuridicoAI.sln`.

## Visão geral

- [Arquitetura e fluxos](arquitetura.md)
- [Configuração e execução](configuracao-e-execucao.md)
- [User Secrets no desenvolvimento](user-secrets.md)

## Backend

- [API HTTP](backend/api.md)
- [Autenticação e multitenancy](backend/autenticacao-e-multitenancy.md)
- [Fundação do caso](backend/casos.md)
- [Peças e templates](backend/pecas.md)
- [Ingestão e reprocessamento de jurisprudências](backend/ingestao-jurisprudencias.md)
- [Normalização e classificação](backend/classificacao.md)
- [Persistência de jurisprudências no MongoDB](backend/persistencia.md)
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
- persistência em MongoDB e PostgreSQL;
- casos, envolvidos, contextos, documentos, peças e templates de peças;
- geração de texto por meio da Responses API da OpenAI;
- classificação local de jurisprudências por regras;
- testes unitários da fundação do caso e de peças.

Não há, no estado atual do repositório, cadastro público, OpenAPI/Swagger,
observabilidade distribuída ou contêiner da aplicação.
O `docker-compose.yml` provisiona MongoDB e PostgreSQL.
