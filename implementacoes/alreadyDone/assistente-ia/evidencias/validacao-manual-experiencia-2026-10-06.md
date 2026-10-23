# Evidência — validação manual da experiência do assistente

> **Data:** 2026-10-06
>
> **Ambiente:** desenvolvimento local
>
> **Resultado funcional:** aprovado com ressalva de experiência

## Objetivo

Retomar a visão do produto e percorrer manualmente os fluxos centrais já
implementados antes de iniciar novas entregas.

Foram usados Caso, Peça e dados fictícios. A IA foi liberada somente para a
Organization utilizada na validação por meio de `AI_ASSISTANT`, `AI_EDIT` e
`AI_REVIEW`. `AI_DOCUMENT_GENERATION` permaneceu desabilitada.

## Cenários executados

| Cenário | Resultado |
|---|---|
| Aviso de transparência antes do primeiro uso | aprovado |
| Perguntar sobre documento completo sem alterar a Peça | aprovado |
| Reescrever seleção e comparar original/proposta | aprovado |
| Aplicar explicitamente e aguardar autosave | aprovado |
| Desfazer e refazer alteração aplicada | aprovado |
| Revisar documento e identificar lacunas | aprovado |
| Desenvolver conteúdo com contexto adicional | aprovado |
| Bloquear aplicação após mudança do documento | aprovado |
| Persistir conteúdo da Peça após recarregar | aprovado |
| Não repetir aviso já confirmado | aprovado |

## Achados

### Funcionais

- o núcleo do assistente é utilizável;
- nenhuma resposta alterou a Peça antes de autorização;
- fatos materiais permaneceram preservados nos cenários observados;
- aplicação, autosave, undo, redo e âncora expirada funcionaram;
- contexto adicional permitiu desenvolver informação além de uma seleção.

### Experiência

- quantidade de controles torna o painel complexo;
- modos, escopos e contexto expõem detalhes técnicos ao usuário;
- ações rápidas, formulário e resultado competem pelo mesmo espaço;
- painel estreito dificulta leitura de respostas jurídicas;
- diferença entre `Revisar` e `Sugerir melhorias` causou confusão;
- ausência de conversa visível faz cada solicitação parecer isolada;
- proposta e decisão de aplicação deveriam fazer parte do corpo do chat.

## Conclusão

A fatia vertical atende às garantias funcionais, mas a experiência atual não é
adequada como interface definitiva do MVP. A próxima entrega de produto deve
priorizar o redesign conversacional definido no
[PA-18](../../../standBy/assistente-ia/decisoes/pa-18-experiencia-conversacional.md), antes de ampliar a
quantidade de ferramentas expostas ao usuário.

O objetivo não é tornar a IA autônoma. O novo chat deve reduzir carga cognitiva
e melhorar leitura, preservando revisão e aplicação explícitas.
