# Assistente de IA — base implementada

> **Organizado em:** 2026-10-23
>
> **Escopo:** registro das partes já existentes; o conjunto da feature não está
> declarado concluído

## Entregas existentes

| Frente | Escopo identificado | Referência |
|---|---|---|
| API e provider | autenticação, isolamento, contexto explícito, saída estruturada, timeout, cancelamento e transporte SSE | [Backend atual](../../../docs/backend/assistente-ia.md) |
| Edição segura | proposta separada, aplicação explícita, âncora, autosave, undo e redo | [Conferência do código](evidencias/conferencia-documental-2026-10-18.md) |
| Interface | chat, chips, propostas editáveis, comparação, larguras predefinidas e expansão | [Conferência do código](evidencias/conferencia-documental-2026-10-18.md) |
| Transparência | aviso versionado e confirmação persistida antes do uso | [Backend atual](../../../docs/backend/assistente-ia.md) |
| Controles técnicos | flags, kill switch auditado, limitadores locais, contagem exata de entrada e ledger de consumo | [Conferência do código](evidencias/conferencia-documental-2026-10-18.md) |
| Avaliação jurídica técnica | suíte fictícia, executor e baseline aceita do PA-09 | [Decisão concluída](decisoes/pa-09-avaliacao-qualidade-juridica.md) |

## Limites do registro

As evidências mantêm as datas e versões originais. Esta reorganização não
executou testes da aplicação. A interface conversacional ainda tem regressão
e validação manual pendentes; o transporte SSE atual envia o texto completo
após validação; os limitadores são locais; histórico persistente, orçamento
com bloqueio e homologação operacional/jurídica não estão concluídos.

O [dossiê anterior e suas pendências](../../standBy/assistente-ia/README.md)
foram preservados em espera. A [nova geração de minutas](../../features/geracao-minutas-ia/README.md)
é tratada separadamente como ainda não implementada.
