# Especificação — experiência conversacional do assistente

> **Estado:** aprovada para implementação incremental
>
> **Última atualização:** 2026-10-06
>
> **Decisão:** [PA-18 — Experiência conversacional](../decisoes/pa-18-experiencia-conversacional.md)

## 1. Objetivo

Transformar o painel atual em um chat integrado ao editor, semelhante à
experiência de agentes em IDEs. O usuário conversa, anexa contexto e recebe
respostas ou propostas de alteração sem precisar entender os modos e operações
do backend.

O redesenho não reduz as proteções existentes. Gerar, revisar e aplicar
continuam sendo etapas distintas.

## 2. Estrutura visual

```text
┌──────────────────────────────────────────┐
│ Assistente                      [↗] [×]  │
├──────────────────────────────────────────┤
│                                          │
│ Você                                     │
│ Torne este parágrafo mais objetivo.      │
│                                          │
│ Assistente                               │
│ Preparei uma alteração:                  │
│ ┌─ Alteração sugerida ─────────────────┐ │
│ │ trecho proposto                     │ │
│ │ [Ver comparação]                    │ │
│ │ [Aceitar] [Editar] [Rejeitar]       │ │
│ └──────────────────────────────────────┘ │
│                                          │
├──────────────────────────────────────────┤
│ [Seleção · 248 caracteres ×]             │
│ ┌──────────────────────────────────────┐ │
│ │ Pergunte ou peça uma alteração…     │ │
│ └──────────────────────────────────────┘ │
│ [＋ contexto] [Melhorar] [Revisar] [→]  │
└──────────────────────────────────────────┘
```

### Regiões

| Região | Responsabilidade |
|---|---|
| Cabeçalho | título, nova conversa futura, expandir e fechar |
| Conversa | mensagens do usuário, respostas, alertas, achados e propostas |
| Contextos | chips com tudo que será enviado no próximo pedido |
| Composer | caixa única, ações rápidas opcionais, anexar contexto e enviar |

O composer permanece fixo no rodapé. Somente a conversa possui rolagem.

## 3. Contexto automático e explícito

### Com seleção

Ao abrir o assistente com uma seleção não vazia, o frontend cria
automaticamente:

```text
[Seleção · <quantidade> caracteres ×]
```

A seleção é um snapshot. O chip pode ser removido antes do envio. A mensagem
não presume que o usuário quer substituir o trecho; a intenção vem do pedido ou
de uma ação rápida.

### Sem seleção

Nenhum documento completo será anexado silenciosamente. O composer informa que
o pedido será enviado sem conteúdo do editor e oferece:

- seção atual;
- Peça completa;
- dados essenciais do Caso;
- contexto cadastrado no Caso;
- informação somente para o pedido.

### Representação

Cada contexto anexado deve mostrar tipo e dimensão aproximada, permitir remoção
e possuir descrição acessível. Exemplos:

```text
[Seleção · 248 caracteres ×]
[Seção “Dos Fatos” · 1.420 caracteres ×]
[Caso · dados essenciais ×]
[Relato inicial ×]
```

O menu `＋ contexto` substitui o bloco expansível atual. Dados não selecionados
não são enviados.

## 4. Entrada e intenções

O usuário escreve em uma única caixa. Ações rápidas apenas preenchem ou
restringem a intenção:

| Ação visível | Intenção interna inicial | Resultado esperado |
|---|---|---|
| Pergunta livre | `Perguntar` | resposta no chat |
| Melhorar / Reescrever | `Reescrever` | proposta para substituir seleção |
| Criar trecho | `Redigir` | proposta para inserir no destino capturado |
| Revisar | `Revisar` | achados no chat, sem alteração direta |
| Desenvolver achado | `Redigir` | nova mensagem e proposta vinculada ao achado |

Na primeira entrega, ações rápidas podem mapear para os modos existentes. Para
um pedido livre escolher com segurança qualquer operação, o backend deverá
aceitar uma intenção conversacional e calcular as operações permitidas a partir
das flags e do contexto disponível. O modelo poderá recomendar somente uma
operação desse conjunto; o usuário continuará aprovando a aplicação.

Não será implementada classificação por busca ingênua de palavras como
“reescreva” ou “revise” apenas no frontend.

## 5. Mensagens

A conversa trabalha com os seguintes tipos visuais:

| Tipo | Conteúdo |
|---|---|
| Mensagem do usuário | pedido e resumo dos contextos enviados |
| Estado da execução | preparando, elaborando, cancelado ou erro |
| Resposta | texto explicativo sem ação de escrita |
| Revisão | lista de achados, cada um com `Desenvolver` quando permitido |
| Proposta | texto editável, destino e ações de aplicação |
| Alerta | referências não verificadas, dados ausentes ou risco de aplicação |
| Confirmação | aplicada, rejeitada, expirada ou bloqueada por sincronização |

O conteúdo completo dos contextos não precisa ser repetido na conversa. Um
resumo expansível informa o que foi enviado.

## 6. Cartão de alteração sugerida

Uma proposta aparece dentro da mensagem da IA:

```text
Alteração sugerida · Substituir seleção

<texto proposto editável>

[Ver comparação]
[Aceitar] [Editar] [Rejeitar] [Nova versão]
```

Regras:

- comparação fica recolhida por padrão para preservar espaço;
- `Aceitar` usa a operação tipada retornada pelo backend;
- `Editar` permite ajustar somente a proposta, nunca o documento silenciosamente;
- `Rejeitar` encerra a proposta sem alterar a Peça;
- `Nova versão` cria outra mensagem e preserva a anterior na conversa;
- proposta aplicada não pode ser aplicada novamente;
- âncora expirada mantém o cartão para cópia, mas desabilita aplicação;
- estado diferente de `SALVO` explica o bloqueio junto à ação;
- undo e redo continuam pertencendo ao editor.

## 7. Dimensões e responsividade

- desktop: largura inicial entre 420 e 480 px;
- largura redimensionável com mínimo que preserve o composer;
- ação `Expandir` ocupa uma área maior sem esconder permanentemente o editor;
- telas estreitas: drawer em largura total;
- textos e diffs longos usam quebra, expansão e rolagem interna controlada;
- a largura escolhida pode ser lembrada localmente, sem sincronização no MVP.

Os valores finais devem usar os tokens e componentes já existentes no
frontend, não medidas duplicadas espalhadas pelo código.

## 8. Histórico e ciclo de vida

### Primeira entrega

A conversa vive na memória enquanto a mesma Peça estiver aberta. Fechar e
reabrir o painel não apaga as mensagens. Trocar de Peça separa as conversas da
sessão atual.

### Entrega posterior

Persistência no JuridicoAI seguirá:

```text
Case
 └── AiConversation
      ├── Message
      └── Suggestion
```

Esse passo depende do PA-03 e do PA-06: tenant, retenção, exclusão separada e
ausência de dependência do histórico do provider.

## 9. Acessibilidade

- conversa identificada como região com nome acessível;
- novas mensagens anunciadas uma vez, sem ler cada delta;
- foco permanece no composer após envio, salvo ação explícita sobre proposta;
- ações de proposta operáveis por teclado;
- contexto removível possui nome completo, não apenas ícone;
- comparação não depende somente de vermelho e verde;
- expandir/recolher informa estado por `aria-expanded`;
- painel redimensionável possui alternativa por teclado ou tamanhos predefinidos;
- aviso de transparência continua modal e anterior ao primeiro envio.

## 10. Métricas sem conteúdo

Eventos mínimos para avaliar a nova experiência:

```text
conversation_opened
message_sent
context_added
context_removed
suggestion_generated
suggestion_applied
suggestion_edited
suggestion_rejected
suggestion_regenerated
suggestion_expired
panel_expanded
```

Nenhum evento armazena texto da mensagem, resposta, seleção ou Peça.

## 11. Critérios de aceite

- [ ] O fluxo normal não exige escolher modo ou escopo técnico.
- [ ] Seleção atual aparece automaticamente como chip removível.
- [ ] É possível adicionar e remover contextos antes do envio.
- [ ] Perguntas e respostas aparecem como conversa.
- [ ] Sugestões aparecem dentro da conversa com destino e ações explícitas.
- [ ] Documento não muda antes de `Aceitar`.
- [ ] Comparação pode ser expandida sem retirar a proposta do chat.
- [ ] Fechar e reabrir o painel preserva a conversa da Peça na sessão.
- [ ] Painel é redimensionável ou expansível.
- [ ] Âncora expirada e autosave inseguro continuam bloqueando aplicação.
- [ ] Fluxos existentes de transparência, cancelamento, retry, undo e redo permanecem válidos.
- [ ] Testes de componente, E2E, teclado e axe-core cobrem a nova interface.
