# Implementações e validações em espera

> **Estado:** standBy por orientação do proprietário do projeto
>
> **Revisão documental:** 2026-11-11

## Prioridade atual e OCR

A implementação ativa é o [assistente conversacional](../features/assistente-conversacional/README.md).
A [calibração do OCR](calibracao-ocr/README.md) foi colocada em espera em
2026-11-11 para concluir o fluxo atual com documentos nativos. As prioridades
vigentes estão no [TODO](../../TODO.md).

## Dossiê anterior

O dossiê anterior do [assistente de IA](assistente-ia/README.md) combina partes
implementadas e trabalho restante. Foi colocado em espera, preservando decisões,
especificações, checklists e evidências. Os registros concluídos estão em
[`alreadyDone`](../alreadyDone/README.md).

A [geração de minutas por pedido livre](../features/geracao-minutas-ia/README.md)
foi incorporada ao assistente conversacional. As pendências abaixo registram o
dossiê anterior; o estado atual de cada recorte deve ser conferido na feature
ativa e em suas evidências.

## Pendências identificadas

| Referência | Trabalho restante em espera | Documento |
|---|---|---|
| PA-01/02 | perfis das demais operações, retry, orçamento mensal, alertas de 70%/90%, bloqueio financeiro e medição de custo | [Modelo e operação](assistente-ia/decisoes/pa-01-modelo-e-operacao.md) |
| PA-03/04/06 | histórico persistente, exclusão independente, exclusão recuperável do Caso, restauração, purga, retenção de logs/backups e pseudonimização adicional | [Retenção e exclusão](assistente-ia/decisoes/pa-03-retencao-exclusao-lgpd.md) |
| PA-05 | revisão jurídica do aviso e URL definitiva da política de privacidade | [Transparência](assistente-ia/decisoes/pa-03-retencao-exclusao-lgpd.md#7-transparência-antes-do-primeiro-uso) |
| PA-07 | recuperação guiada da seleção e regeneração com nova âncora | [Âncora expirada](assistente-ia/decisoes/pa-07-ancora-expirada.md) |
| PA-08 | cenário dedicado de undo após falha de autosave posterior à aplicação | [Autosave](assistente-ia/decisoes/pa-08-autosave-em-erro.md) |
| PA-09 — evolução | ampliação das avaliações, execução em CI sob demanda, comparação de candidatos e medições de revisão; baseline atual continua concluída | [Evolução posterior](../alreadyDone/assistente-ia/decisoes/pa-09-avaliacao-qualidade-juridica.md#8-evolução-posterior) |
| PA-10 | aviso permanente na interface sobre ausência de pesquisa externa e homologação jurídica das referências | [Fontes](assistente-ia/decisoes/pa-10-legislacao-jurisprudencia-fontes.md) |
| PA-11 | coordenação distribuída, revisão de produção e inspeção de logs de proxy/APM reais; cobertura dos futuros recursos persistentes | [Segurança](assistente-ia/decisoes/pa-11-seguranca-isolamento-logs.md) |
| PA-12 | auditoria manual de teclado, foco, contraste, leitor de tela e comprovação em plataformas reais | [Testes e acessibilidade](assistente-ia/decisoes/pa-12-testes-acessibilidade.md) |
| PA-13 | interface administrativa autenticada e rollout nominal com evidências de tenant interno e piloto | [Flags e rollout](assistente-ia/decisoes/pa-13-feature-flags-rollout.md) |
| PA-14 | correlação ponta a ponta, coleta de métricas, eventos explícitos e idempotentes de produto, dashboards, alertas, responsáveis e runbooks | [Observabilidade](assistente-ia/decisoes/pa-14-observabilidade-metricas.md) |
| PA-15 | medir e reduzir bundle, avaliando lazy loading | [Bundle](assistente-ia/planejamento/pontos-em-aberto.md#pa-15--tamanho-do-bundle-do-frontend) |
| PA-17 | revisão de LGPD, bases legais, fornecedores, transferência internacional, contratos e homologação jurídica formal | [Validação jurídica](assistente-ia/planejamento/pontos-em-aberto.md#pa-17--validação-jurídica-e-contratual-para-comercialização) |
| PA-18 — E0/E1/E2 | protótipo e validação visual, separação do painel, conversa preservada entre Peças e feedback completo dos limites de contexto | [Redesign](assistente-ia/planejamento/redesign-conversacional.md) |
| PA-18 — E4 | intenção automática geral para pergunta, revisão e reescrita; a geração de minuta fica na feature ativa | [Intenção conversacional](assistente-ia/planejamento/redesign-conversacional.md#e4--pedido-livre-e-intenção-conversacional) |
| PA-18 — E5/E6 | regressão completa do chat, E2E/axe-core, viewport estreita, validação manual, remoção segura de componentes antigos e histórico persistente | [Plano incremental](assistente-ia/planejamento/redesign-conversacional.md) |
| Backend — streaming | exibição incremental do texto durante a geração; atualmente a API entrega um único delta após validação | [Especificação backend](assistente-ia/especificacoes/backend.md) |
| Operação integrada | comprovar integração com bancos e infraestrutura reais, retenção e configurações do provider | [Checklist operacional](assistente-ia/evidencias/seguranca-operacional-producao.md) |

Os perfis e limites específicos da nova geração de minutas fazem parte da
feature ativa; o orçamento global e os perfis das outras operações continuam
neste backlog. Histórico e revisão por seção são evoluções posteriores, sem
inclusão implícita na primeira entrega de geração.

## Como retomar

Escolher um escopo delimitado, levá-lo para `features/` com critérios de aceite
e registrar evidências próprias. A organização em espera não remove os bloqueios
de produção e comercialização já registrados nos documentos de origem.
