# Documentação do JuridicoAI

Esta documentação descreve o comportamento implementado atualmente na solução
`JuridicoAI.sln`. Especificações de novas features ficam em `docs/features/`,
com status explícito. Outros planos e decisões em evolução permanecem em
[`implementacoes/`](../implementacoes/README.md).

## Visão geral

- [Arquitetura e fluxos](arquitetura.md)
- [Versionamento — produção, homologação e fluxo de mudanças](versionamento.md)
- [Configuração e execução](configuracao-e-execucao.md)
- [User Secrets no desenvolvimento](user-secrets.md)

## Features

- [Exportação de peças para Word e PDF — escopo e aceite](features/exportacao-word-pdf.md)

## Backend

- [API HTTP](backend/api.md)
- [Assistente de IA](backend/assistente-ia.md)
- [Coleção Postman principal, sem o stream do assistente](backend/JuridicoAI.postman_collection.json)
- [Autenticação e multitenancy](backend/autenticacao-e-multitenancy.md)
- [Fundação do caso](backend/casos.md)
- [Peças e templates](backend/pecas.md)

O scraper do TJSP e o código anterior de jurisprudências foram separados em
`../JuridicoAI.Scrapper`, relativo à raiz deste repositório. Não fazem parte
da API ou dos requisitos do MVP.

## Operação

- [Configurar a IA — ativação e ajustes](operacao/configuracao-ia.md)
- [Solução de problemas e limitações conhecidas](operacao/troubleshooting.md)
- [Configuração da exportação Word e PDF](operacao/exportacao-word-pdf.md)
- [Monitoramento do assistente — P04](operacao/monitoramento-assistente.md)

## Implementações: ativa, entregas e espera

- [Índice de implementações](../implementacoes/README.md)
- [Preparação para produção e testes locais — andamento](../implementacoes/features/preparacao-producao/README.md)
- [Assistente conversacional — estado atual](../implementacoes/features/assistente-conversacional/estado-atual.md)
- [TODO do assistente e melhorias evidenciadas](../implementacoes/features/assistente-conversacional/TODO.md)
- [Geração de minutas — planejamento anterior incorporado](../implementacoes/features/geracao-minutas-ia/README.md)
- [Entregas registradas — alreadyDone](../implementacoes/alreadyDone/README.md)
- [Pendências em espera — standBy](../implementacoes/standBy/README.md)
- [Dossiê anterior do assistente — em espera](../implementacoes/standBy/assistente-ia/README.md)
- [Conferência do estado implementado em 2026-10-18](../implementacoes/alreadyDone/assistente-ia/evidencias/conferencia-documental-2026-10-18.md)
- [Suíte executável de qualidade jurídica](../avaliacoes/qualidade-juridica/README.md)

## Histórico e auditorias

- [Visão inicial do produto](historico/visao-inicial-produto.md)
- [Auditoria documental de 2026-10-03](historico/auditoria-documentacao-2026-10-03.md)

Documentos desta seção preservam contexto e decisões passadas, mas não são
referência normativa para o comportamento atual.

## Escopo atual

O projeto contém:

- uma API ASP.NET Core em .NET 10 com autenticação JWT e Swagger em desenvolvimento;
- cadastro público configurável, provisionamento de organização/usuário por CLI,
  login e renovação de sessão;
- casos, envolvidos, contextos, documentos, peças e templates de peças;
- editor de peças com autosave no frontend;
- exportação de peças para Word editável e PDF, com conversor local configurável;
- assistente de IA com geração em streaming, revisão e aplicação explícita no editor;
- conversa livre com histórico/memória, documentos nativos, análise e geração
  duráveis, revisão persistida, histórico pessoal e exclusão autorizada, atrás de flags;
- controles de tenant, confirmação de transparência, feature flags e kill switch da IA;
- persistência em PostgreSQL;
- testes automatizados de backend, frontend, segurança e qualidade jurídica.

Há monitoramento local configurável por OTLP, com painel e regras de alerta em
`ops/monitoring/`. Não há contêiner da aplicação. O `docker-compose.yml` provisiona
PostgreSQL. Configuração de notificações e monitoramento no deploy, acesso ao piloto,
proteção das exclusões após restauração, configuração das tarifas do piloto e validações jurídicas
continuam acompanhados na documentação de implementação.
