# Documentação do JuridicoAI

Esta documentação descreve o comportamento implementado atualmente na solução
`JuridicoAI.sln`.

## Visão geral

- [Arquitetura e fluxos](arquitetura.md)
- [Configuração e execução](configuracao-e-execucao.md)
- [User Secrets no desenvolvimento](user-secrets.md)

## Implementações em andamento

- [Índice de implementações](../implementacoes/README.md)
- [Assistente de IA para redação jurídica](../implementacoes/features/assistente-ia/README.md)
  - [Contrato HTTP](../implementacoes/features/assistente-ia/contrato-api.md)
  - [Plano do backend](../implementacoes/features/assistente-ia/backend.md)
  - [Plano do frontend](../implementacoes/features/assistente-ia/frontend.md)
  - [Pontos em aberto](../implementacoes/features/assistente-ia/pontos-em-aberto.md)
  - [Tópico 1 — Modelo e operação](../implementacoes/features/assistente-ia/topico-01-modelo-e-operacao.md)
  - [Tópico 3 — Retenção, exclusão e LGPD](../implementacoes/features/assistente-ia/topico-03-retencao-exclusao-lgpd.md)
  - [Tópico 7 — Âncora expirada](../implementacoes/features/assistente-ia/topico-07-ancora-expirada.md)
  - [Tópico 8 — Autosave em erro](../implementacoes/features/assistente-ia/topico-08-autosave-em-erro.md)
  - [Tópico 9 — Avaliação de qualidade jurídica](../implementacoes/features/assistente-ia/topico-09-avaliacao-qualidade-juridica.md)
  - [Tópico 11 — Segurança, isolamento e logs](../implementacoes/features/assistente-ia/topico-11-seguranca-isolamento-logs.md)
  - [Tópico 12 — Testes e acessibilidade](../implementacoes/features/assistente-ia/topico-12-testes-acessibilidade.md)
  - [Tópico 13 — Feature flags e rollout](../implementacoes/features/assistente-ia/topico-13-feature-flags-rollout.md)
  - [Tópico 14 — Observabilidade e métricas](../implementacoes/features/assistente-ia/topico-14-observabilidade-metricas.md)

## Backend

- [API HTTP](backend/api.md)
- [Coleção Postman](backend/JuridicoAI.postman_collection.json)
- [Autenticação e multitenancy](backend/autenticacao-e-multitenancy.md)
- [Fundação do caso](backend/casos.md)
- [Peças e templates](backend/pecas.md)
- [Ingestão e reprocessamento de jurisprudências](backend/ingestao-jurisprudencias.md)
- [Normalização e classificação](backend/classificacao.md)
- [Persistência de jurisprudências no MongoDB](backend/persistencia.md)

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
- primeira fatia do assistente de IA para Peças, desabilitada por padrão;
- classificação local de jurisprudências por regras;
- testes unitários da fundação do caso e de peças.

Não há, no estado atual do repositório, cadastro público, OpenAPI/Swagger,
observabilidade distribuída ou contêiner da aplicação.
O `docker-compose.yml` provisiona MongoDB e PostgreSQL.
