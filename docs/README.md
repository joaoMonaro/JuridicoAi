# Documentação do JuridicoAI

Esta documentação descreve o comportamento implementado atualmente na solução
`JuridicoAI.sln`. Especificações de novas features ficam em `docs/features/`,
com status explícito. Outros planos e decisões em evolução permanecem em
[`implementacoes/`](../implementacoes/README.md).

## Visão geral

- [Arquitetura e fluxos](arquitetura.md)
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
- [Ingestão e reprocessamento de jurisprudências](backend/ingestao-jurisprudencias.md)
- [Normalização e classificação](backend/classificacao.md)
- [Persistência de jurisprudências no MongoDB](backend/persistencia.md)

## Scraper

- [Visão geral](scraper/visao-geral.md)
- [Uso pela linha de comando](scraper/cli.md)
- [Integração técnica com o TJSP](scraper/integracao-tjsp.md)

O scraper permanece disponível como componente técnico, mas está fora do fluxo
e da apresentação do MVP do assistente de IA.

## Operação

- [Solução de problemas e limitações conhecidas](operacao/troubleshooting.md)
- [Configuração da exportação Word e PDF](operacao/exportacao-word-pdf.md)

## Implementações: ativa, entregas e espera

- [Índice de implementações](../implementacoes/README.md)
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
- [Revisão anterior da documentação do scraper](historico/revisao-documentacao-scraper.md)
- [Auditoria documental de 2026-10-03](historico/auditoria-documentacao-2026-10-03.md)

Documentos desta seção preservam contexto e decisões passadas, mas não são
referência normativa para o comportamento atual.

## Escopo atual

O projeto contém:

- uma API ASP.NET Core em .NET 10 com autenticação JWT e Swagger em desenvolvimento;
- cadastro inicial de organização e usuário, login e renovação de sessão;
- casos, envolvidos, contextos, documentos, peças e templates de peças;
- editor de peças com autosave no frontend;
- exportação de peças para Word editável e PDF, com conversor local configurável;
- assistente de IA com geração em streaming, revisão e aplicação explícita no editor;
- conversa livre com histórico/memória, documentos nativos, análise e geração
  duráveis, revisão persistida, histórico pessoal e exclusão autorizada, atrás de flags;
- controles de tenant, confirmação de transparência, feature flags e kill switch da IA;
- persistência em PostgreSQL e MongoDB;
- scraper do portal e-SAJ/TJSP, pausado no fluxo do MVP;
- classificação local de jurisprudências por regras;
- testes automatizados de backend, frontend, segurança e qualidade jurídica.

Ainda não há observabilidade distribuída nem contêiner da aplicação. O
`docker-compose.yml` provisiona MongoDB e PostgreSQL. Rollout comercial,
proteção das exclusões após restauração, orçamento operacional completo e validações jurídicas
continuam acompanhados na documentação de implementação.
