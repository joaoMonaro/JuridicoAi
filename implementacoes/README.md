# Implementações do JuridicoAI

Este diretório concentra a documentação viva das funcionalidades em construção.
Ele fica separado de `docs/`, que descreve prioritariamente o comportamento
estável e já disponível do produto.

## Organização

```text
implementacoes/
└── features/
    └── <nome-da-feature>/
        ├── README.md
        ├── backend.md
        ├── frontend.md
        ├── contrato-api.md
        └── pontos-em-aberto.md
```

Cada feature deve possuir um documento principal com estado, escopo, critérios
de aceite e links para os planos técnicos. Decisões ainda não tomadas ficam em
`pontos-em-aberto.md`; depois de resolvidas, são registradas no documento
principal e removidas da lista de pendências.

## Features atuais

- [Assistente de IA para redação jurídica](features/assistente-ia/README.md)
  - [Contrato HTTP](features/assistente-ia/contrato-api.md)
  - [Backend](features/assistente-ia/backend.md)
  - [Frontend](features/assistente-ia/frontend.md)
  - [Pontos em aberto](features/assistente-ia/pontos-em-aberto.md)
  - [Tópico 1 — Modelo e operação](features/assistente-ia/topico-01-modelo-e-operacao.md)
  - [Tópico 3 — Retenção, exclusão e LGPD](features/assistente-ia/topico-03-retencao-exclusao-lgpd.md)
  - [Tópico 7 — Âncora expirada](features/assistente-ia/topico-07-ancora-expirada.md)
  - [Tópico 8 — Autosave em erro](features/assistente-ia/topico-08-autosave-em-erro.md)
  - [Tópico 9 — Avaliação de qualidade jurídica](features/assistente-ia/topico-09-avaliacao-qualidade-juridica.md)
  - [Tópico 11 — Segurança, isolamento e logs](features/assistente-ia/topico-11-seguranca-isolamento-logs.md)
  - [Tópico 12 — Testes e acessibilidade](features/assistente-ia/topico-12-testes-acessibilidade.md)
  - [Tópico 13 — Feature flags e rollout](features/assistente-ia/topico-13-feature-flags-rollout.md)
  - [Tópico 14 — Observabilidade e métricas](features/assistente-ia/topico-14-observabilidade-metricas.md)
