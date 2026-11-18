# Implementações do JuridicoAI

> **Organização atualizada em:** 2026-11-12

Este diretório separa a implementação ativa, as entregas já registradas e as
pendências colocadas em espera. A referência do comportamento disponível
continua em [`docs/`](../docs/README.md).

A [lista de TODO da feature](features/assistente-conversacional/TODO.md) prioriza
a conclusão do assistente conversacional e registra as melhorias observadas.
A [calibração do OCR](standBy/calibracao-ocr/README.md) está em espera por
orientação do responsável pelo produto; leitura nativa segue no escopo ativo.

## Implementação ativa — assistente conversacional

- [Estado atual, entregas e pontos restantes](features/assistente-conversacional/estado-atual.md): referência de andamento consolidada em 2026-11-12.
- [Escopo e decisões de produto](features/assistente-conversacional/README.md).
- [Plano E0–E5 e matriz de validação reconciliados](features/assistente-conversacional/especificacoes/05-entrega-e-validacao.md).
- [Desenho técnico](features/assistente-conversacional/desenho-tecnico.md).

O fluxo principal está implementado atrás de flags: conversa livre, histórico e
memória, anexos privados/leitura nativa, recuperação documental, análise extensa,
geração durável por seções, revisão com aprovação transacional, histórico pessoal
e exclusão autorizada do ambiente ativo.

A avaliação real reduzida passou nos checks técnicos: quatro cenários/seis
interações, custo calculado de US$ 0,04549805. Ver
[resultados e limites](features/assistente-conversacional/evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).
Restam avaliação ampliada, homologação, controles financeiros de produção,
monitoramento, proteção das
exclusões após restauração, rollout por organização, validação ampliada do editor
e preparação operacional. OCR permanece desativado e em espera. Implementação
com testes técnicos não declara aprovação jurídica ou liberação do piloto.

O [plano anterior de geração de minutas](features/geracao-minutas-ia/README.md)
foi incorporado à ampliação. Sua
[validação manual](features/geracao-minutas-ia/evidencias/validacao-manual-experiencia-2026-10-23.md)
e o [prompt fictício](features/geracao-minutas-ia/evidencias/prompts/prompt-geracao-peca-ficticia.txt)
permanecem como evidências históricas, sem transferir aprovações ao novo fluxo.

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
│   │   ├── TODO.md
│   │   ├── estado-atual.md
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
