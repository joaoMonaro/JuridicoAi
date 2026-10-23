# Plano de implementação — redesign conversacional

> **Estado:** em espera; primeira interface parcialmente implementada
>
> **Última atualização de organização:** 2026-10-23
>
> **Especificação:** [experiência conversacional](../especificacoes/experiencia-conversacional.md)

## Estratégia

O plano completo foi colocado em `standBy` em 2026-10-23. A
[geração de minutas por pedido livre](../../../features/geracao-minutas-ia/README.md)
assume somente a parte do E4 necessária para reconhecer e apresentar uma
proposta de minuta. As outras intenções, a persistência e a conclusão geral do
redesign permanecem em espera.

O redesenho será entregue em fatias verificáveis. A API atual, o parser SSE, as
âncoras, o autosave e as validações de segurança serão preservados. O painel
antigo só será removido depois que os fluxos equivalentes estiverem cobertos.

Chat, chips e propostas já substituem o formulário principal no código. A
conferência estática de 2026-10-18 atualiza os estados sem declarar a regressão
completa ou a validação visual concluídas. As execuções de 2026-10-06 abaixo
permanecem evidências históricas, conforme a
[conferência documental](../../../alreadyDone/assistente-ia/evidencias/conferencia-documental-2026-10-18.md).

## E0 — documentação e contrato de experiência

- [x] registrar a validação manual da interface atual;
- [x] decidir chat como interface principal;
- [x] definir mensagens, contextos, propostas e limites de autonomia;
- [x] definir critérios de aceite e sequência de entrega;
- [ ] produzir wireframe navegável ou protótipo de baixa fidelidade;
- [ ] validar o protótipo com o responsável pelo produto antes da troca visual.

## E1 — estrutura de chat no frontend

- [ ] separar o painel em `ConversationHeader`, `MessageList` e `Composer`;
- [x] modelar mensagens locais com identificador, papel, estado e vínculo com sugestão;
- [x] manter conversa ao fechar/reabrir o painel com o mesmo editor montado;
- [ ] preservar conversa por `pecaId` ao trocar de Peça e retornar na sessão;
- [x] converter execução, erro e cancelamento em mensagens da conversa;
- [x] fixar composer no rodapé e rolagem somente na lista;
- [x] adicionar expansão e alternância entre larguras predefinidas do painel;
- [x] preservar aviso de transparência e carregamento de capacidades.

Nesta etapa, os modos existentes podem permanecer internos. A mudança deve ser
compatível com o endpoint atual.

## E2 — contexto automático

- [x] capturar seleção ao abrir ou focar o assistente;
- [x] exibir seleção como chip removível com dimensão;
- [x] implementar menu `Adicionar contexto`;
- [x] suportar seção atual, documento completo, dados do Caso, contexto
      cadastrado e informação para o pedido;
- [x] exibir resumo expansível dos contextos em cada mensagem enviada;
- [x] garantir que nenhum contexto não selecionado entre no payload;
- [ ] manter limites de tamanho e feedback antes do envio.

## E3 — respostas e sugestões no chat

- [x] renderizar resposta simples como mensagem do assistente;
- [x] renderizar achados de revisão dentro da mensagem;
- [x] criar cartão de proposta com texto editável e comparação recolhível;
- [x] mover aceitar, rejeitar, copiar e nova versão para o cartão;
- [x] preservar propostas anteriores ao regenerar;
- [x] representar aplicada, rejeitada, expirada e bloqueada como estados do cartão;
- [x] impedir aplicação duplicada;
- [x] manter undo, redo e autosave do editor.

## E4 — pedido livre e intenção conversacional

- [ ] definir evolução compatível do contrato para intenção automática;
- [ ] derivar operações permitidas de feature flags, seleção e destino disponíveis;
- [ ] permitir ao provider recomendar somente operações autorizadas;
- [ ] validar a recomendação no backend antes de transmitir a sugestão;
- [ ] manter ações rápidas como restrições explícitas e previsíveis;
- [ ] atualizar contrato, prompt, suíte jurídica e suíte de segurança;
- [ ] não usar classificação por palavras-chave como fonte de autoridade.

Até E4, pedidos enviados sem ação rápida podem ser tratados como pergunta. A
interface deve comunicar esse comportamento sem reintroduzir o seletor de modo.

## E5 — qualidade, migração e remoção do painel antigo

- [ ] concluir revisão da cobertura e executar a regressão completa do redesign;
- [x] adaptar testes de componente para chat, chips, propostas, bloqueios,
  preservação ao fechar/reabrir e expansão;
- [ ] atualizar E2E de pergunta, reescrita, redação, revisão e âncora expirada;
- [ ] cobrir painel redimensionado, expandido e viewport estreita;
- [ ] executar axe-core em conversa vazia, execução, resposta, proposta, erro e bloqueio;
- [ ] executar auditoria manual de teclado e leitor de tela;
- [ ] comparar bundle antes/depois e tratar regressão relevante com lazy loading;
- [ ] validar manualmente os mesmos cenários da evidência de 2026-10-06;
- [ ] remover componentes e estilos antigos somente após equivalência comprovada.

## E6 — histórico persistente

- [ ] detalhar entidades `AiConversation`, `AiMessage` e `AiSuggestion`;
- [ ] definir criação, continuação, listagem e exclusão do histórico;
- [ ] aplicar isolamento por Organization e Caso;
- [ ] implementar `Limpar histórico da IA` separado de excluir Caso;
- [ ] aplicar retenção e hard delete definidos no PA-03;
- [ ] cobrir acesso cruzado e ausência de conteúdo em logs;
- [ ] atualizar capacidades para anunciar histórico somente quando disponível.

E6 não bloqueia a primeira entrega visual, mas é necessária para uma experiência
conversacional persistente entre sessões.

## Riscos e contenções

| Risco | Contenção |
|---|---|
| redesign quebrar aplicação segura | reutilizar funções de âncora e aplicação; testes de regressão |
| intenção automática sugerir destino incorreto | conjunto permitido calculado no backend e confirmação humana |
| chat crescer e degradar desempenho | virtualização somente quando medida; mensagens por Peça |
| resposta continuar difícil de ler | largura maior, expansão e comparação recolhível |
| estado duplicar documento TipTap | conversa guarda mensagens e snapshots, nunca outro documento canônico |
| histórico ampliar risco de LGPD | primeira etapa local; persistência somente com PA-03/PA-06 |
| bundle crescer | divisão do assistente e medição associada ao PA-15 |

## Definição de pronto da primeira entrega

A primeira entrega termina em E3 e está pronta quando:

- a experiência principal ocorre em um chat;
- seleção e contexto são visíveis sem formulário permanente;
- pergunta, revisão e propostas usam mensagens e cartões;
- aplicação continua explícita e protegida;
- conversa local sobrevive ao fechamento do painel;
- painel ampliado permite ler respostas jurídicas extensas;
- testes automatizados passam;
- a validação manual confirma melhora de compreensão e legibilidade.

Intenção automática completa (E4) e histórico persistente (E6) podem seguir em
entregas posteriores sem bloquear a substituição visual inicial.

## Registro de implementação

### 2026-10-06 — primeira fatia conversacional

Implementado no frontend:

- turnos locais separados por `pecaId`;
- pedido do usuário e resposta da IA exibidos como conversa;
- estados de preparação, streaming, erro e cancelamento na linha do tempo;
- composer fixo e lista de mensagens rolável;
- menu progressivo para os contextos já suportados pelo contrato atual;
- resposta de leitura e cartão editável para propostas;
- preservação da proposta anterior ao solicitar nova versão;
- estados visuais de proposta aplicada e rejeitada;
- largura confortável, largura ampliada e modo expandido;
- foco devolvido ao composer após o término da solicitação.

Validação automatizada desta fatia:

```text
Vitest: 65 testes aprovados
Painel do assistente: 23 testes aprovados
ESLint: sem erros
Build de produção: aprovado
```

O endpoint, o parser SSE, as âncoras, a aplicação explícita e as regras de
autosave não foram alterados. A segunda fatia adicionou o snapshot automático
da seleção, chip com dimensão e remoção efetiva do payload. Pedidos sem seleção
agora começam sem conteúdo da Peça por padrão, evitando contexto implícito.
Continuam pendentes a separação estrutural em componentes menores e a validação
visual/manual da nova experiência.

### 2026-10-18 — conferência do estado implementado

O código mantém turnos indexados por `pecaId` dentro do painel, mas o workspace
remonta `LegalDocumentEditor` com `key={peca.id}` a cada troca. Por isso, a
preservação comprovada pelo teste de componente limita-se a fechar/reabrir o
painel sem desmontar o editor. Navegar entre Peças ou recarregar perde o estado.

O composer e as funções de renderização ainda ficam no mesmo arquivo
`assistant-panel.tsx`. Há testes adaptados para a interface de chat, sem
evidência de execução completa de E5 após todas as mudanças. Permanecem abertos
feedback completo de limites, recuperação guiada de âncora, validação
visual/manual, intenção automática (E4) e histórico persistente (E6).
