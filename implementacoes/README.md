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

