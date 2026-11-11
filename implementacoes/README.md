# Implementações do JuridicoAI

> **Organização atualizada em:** 2026-11-11

Este diretório separa a implementação ativa, as entregas já registradas e as
pendências colocadas em espera. A referência do comportamento disponível
continua em [`docs/`](../docs/README.md).

## Implementação ativa — em andamento

- [Assistente conversacional com edição da Peça e contexto persistente](features/assistente-conversacional/README.md): conversa livre, propostas automáticas, revisão persistida e histórico usado como contexto atrás de flags; documentos e memória extensa pendentes.
- [Plano anterior de geração de minutas](features/geracao-minutas-ia/README.md), incorporado ao escopo ampliado.
- [Validação manual que originou a proposta](features/geracao-minutas-ia/evidencias/validacao-manual-experiencia-2026-10-23.md).
- [Prompt fictício para reprodução](features/geracao-minutas-ia/evidencias/prompts/prompt-geracao-peca-ficticia.txt).

A feature ativa é o assistente conversacional. A revisão possui persistência
e aprovação transacional. Mensagens e respostas são recuperadas e usadas como
contexto, dentro da janela atual. Veja [a etapa de histórico](features/assistente-conversacional/evidencias/e1-e3-historico-2026-11-11.md).
A [conversa livre](features/assistente-conversacional/evidencias/e3-conversa-livre-2026-11-11.md)
prepara propostas diretamente e permite ajustar pendências pelo chat. Memória para
conversas extensas, documentos e avaliação com modelo real continuam pendentes.

## Ampliação de escopo e detalhamento técnico

- [Assistente conversacional com edição da Peça e contexto persistente](features/assistente-conversacional/README.md).

A proposta amplia a geração de minutas para uma jornada com documentos,
continuidade da conversa e edição mediante aprovação visual. O comportamento de
produto foi definido e possui especificações técnicas, contratos e plano de
validação. A implementação avançou da E0 local do editor para a revisão
persistida, com ativação controlada. As limitações
e estimativas do plano anterior não representam esta ampliação.

## Entregas registradas e pendências em espera

- [`alreadyDone/`](alreadyDone/README.md): resumo do que já existe, decisões
  tecnicamente concluídas e evidências históricas.
- [`standBy/`](standBy/README.md): planos e validações que ainda têm trabalho
  pendente, preservados para retomada posterior.

Os nomes das pastas são relativos a `implementacoes/`. Mover documentos não
aprova testes, não conclui homologações e não libera produção ou comercialização.

## Organização

```text
implementacoes/
├── README.md
├── features/
│   ├── assistente-conversacional/
│   │   ├── README.md
│   │   ├── desenho-tecnico.md
│   │   ├── especificacoes/
│   │   └── contratos/
│   └── geracao-minutas-ia/
│       ├── README.md
│       └── evidencias/
├── alreadyDone/
│   ├── README.md
│   └── assistente-ia/
└── standBy/
    ├── README.md
    └── assistente-ia/
        ├── README.md
        ├── decisoes/
        ├── especificacoes/
        ├── planejamento/
        └── evidencias/
```

## Regra de manutenção

Cada feature ativa deve indicar problema, escopo, estado e critérios de aceite.
Uma entrega só passa para `alreadyDone` com evidência do escopo concluído. Se um
documento mistura código existente e entregas futuras, permanece em `standBy`
com as pendências explícitas. Evidências históricas preservam a data, a versão
e os limites da validação original.

Nesta reorganização, os 23 documentos do dossiê anterior foram preservados:
3 registros concluídos/históricos em `alreadyDone`, 19 documentos com planos ou
validações pendentes em `standBy` e a evidência desta sessão na feature ativa.
