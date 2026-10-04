# Implementações do JuridicoAI

Este diretório é a bancada de trabalho das funcionalidades em evolução. Ele
concentra especificações, decisões, planejamento e evidências enquanto uma
entrega ainda está sendo desenvolvida ou validada.

O diretório [`docs/`](../docs/README.md) continua sendo a referência do
comportamento estável e efetivamente disponível. Quando uma decisão de
implementação se torna comportamento permanente, sua descrição deve ser
promovida para `docs/`; o histórico da decisão pode permanecer aqui.

## Organização

```text
implementacoes/
└── features/
    └── <nome-da-feature>/
        ├── README.md
        ├── especificacoes/
        │   ├── backend.md
        │   ├── frontend.md
        │   └── contrato-api.md
        ├── decisoes/
        │   └── pa-<numero>-<assunto>.md
        ├── planejamento/
        │   └── pontos-em-aberto.md
        └── evidencias/
            └── <evidencia>.md
```

Cada feature deve manter no `README.md` seu estado, escopo e critérios de
aceite. As pastas têm responsabilidades distintas:

- `especificacoes/`: comportamento pretendido e contratos técnicos em evolução;
- `decisoes/`: decisões fechadas, com contexto e consequências;
- `planejamento/`: pendências reais e próximos passos;
- `evidencias/`: checklists, resultados e comprovações de validação.

Documentos históricos que não orientam mais a implementação pertencem a
[`docs/historico/`](../docs/historico/).

## Features atuais

- [Assistente de IA para redação jurídica](features/assistente-ia/README.md)
  - Especificações: [contrato HTTP](features/assistente-ia/especificacoes/contrato-api.md), [backend](features/assistente-ia/especificacoes/backend.md) e [frontend](features/assistente-ia/especificacoes/frontend.md)
  - Planejamento: [pontos em aberto](features/assistente-ia/planejamento/pontos-em-aberto.md)
  - Decisões: [PA-01](features/assistente-ia/decisoes/pa-01-modelo-e-operacao.md), [PA-03](features/assistente-ia/decisoes/pa-03-retencao-exclusao-lgpd.md), [PA-07](features/assistente-ia/decisoes/pa-07-ancora-expirada.md), [PA-08](features/assistente-ia/decisoes/pa-08-autosave-em-erro.md), [PA-09](features/assistente-ia/decisoes/pa-09-avaliacao-qualidade-juridica.md), [PA-10](features/assistente-ia/decisoes/pa-10-legislacao-jurisprudencia-fontes.md), [PA-11](features/assistente-ia/decisoes/pa-11-seguranca-isolamento-logs.md), [PA-12](features/assistente-ia/decisoes/pa-12-testes-acessibilidade.md), [PA-13](features/assistente-ia/decisoes/pa-13-feature-flags-rollout.md) e [PA-14](features/assistente-ia/decisoes/pa-14-observabilidade-metricas.md)
  - Evidências: [segurança operacional](features/assistente-ia/evidencias/seguranca-operacional-producao.md) e [acessibilidade manual](features/assistente-ia/evidencias/checklist-manual-acessibilidade.md)
  - Avaliações: [suíte executável de qualidade jurídica](../avaliacoes/qualidade-juridica/README.md)
