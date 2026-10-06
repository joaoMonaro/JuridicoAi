# PA-18 — Experiência conversacional do assistente

> **Estado da decisão:** aceita
>
> **Estado da implementação:** pendente
>
> **Data da decisão:** 2026-10-06
>
> **Evidência:** [validação manual da experiência atual](../evidencias/validacao-manual-experiencia-2026-10-06.md)

## Problema

A primeira fatia vertical comprovou geração, aplicação segura e proteção contra
falhas, mas expôs na interface conceitos internos demais:

- modo de operação;
- escopo do documento;
- contexto adicional;
- dados do Caso;
- ações rápidas e ações finais concorrendo pela atenção;
- resultados longos em um painel estreito.

O usuário precisa compreender o contrato técnico antes de conseguir conversar
com a IA. A inconsistência entre `Revisar` e `Sugerir melhorias` reforça essa
dificuldade. O painel funciona, porém se comporta como formulário de
configuração e torna a resposta difícil de visualizar.

## Decisão

O assistente adotará o modelo de interação de um agente integrado ao editor:

```text
contexto capturado do editor
        ↓
mensagem em uma conversa
        ↓
resposta ou alteração sugerida no chat
        ↓
usuário aceita, edita, rejeita ou pede outra versão
        ↓
alteração explícita e reversível no editor
```

O chat passa a ser a interface principal. Modos e operações continuam
existindo no contrato interno enquanto forem necessários, mas deixam de ser
configuração obrigatória exposta ao usuário.

## Regras da experiência

1. Uma única caixa recebe perguntas e pedidos de alteração.
2. A seleção atual é detectada automaticamente e exibida como contexto
   removível.
3. Contextos adicionais aparecem como chips, não como um formulário aberto.
4. Ações rápidas são atalhos opcionais e configuram a intenção internamente.
5. Respostas e propostas aparecem como mensagens na mesma conversa.
6. Alterações usam cartões com comparação e ações explícitas.
7. O painel pode ser redimensionado ou expandido para leitura de conteúdo
   jurídico extenso.
8. Opções avançadas ficam atrás de uma ação de adicionar contexto.
9. O vocabulário visível é orientado à tarefa jurídica, não aos enums da API.
10. A conversa preserva o estado ao menos enquanto a Peça permanecer aberta.

## Limite de autonomia

“Agente” define o modelo de interação, não autorização autônoma para escrever.
O assistente pode analisar contexto e propor uma operação, mas não pode:

- alterar a Peça sem ação do usuário;
- reaplicar uma sugestão em âncora expirada;
- aplicar enquanto o autosave não estiver em `SALVO`;
- pesquisar externamente sem uma ferramenta futura explícita;
- executar uma sequência de mudanças silenciosas;
- contornar feature flags, limites ou confirmação de transparência.

As garantias dos PA-07, PA-08, PA-10, PA-11 e PA-13 permanecem invariantes.

## Consequências técnicas

- o frontend será reorganizado em conversa, mensagens, composer, chips de
  contexto e cartões de sugestão;
- o contrato atual poderá continuar atendendo ações rápidas durante a primeira
  entrega;
- pedido livre com escolha automática entre responder, inserir, substituir e
  revisar exigirá evolução tipada do contrato, sem inferência frágil apenas por
  palavras-chave no navegador;
- o histórico local da sessão será implementado antes do histórico persistente;
- persistência por Caso continuará vinculada ao PA-06 e às regras do PA-03;
- telemetria registrará eventos e estados, nunca o conteúdo das mensagens.

## Fora desta decisão

- agente autônomo com loop de ferramentas;
- múltiplas alterações aplicadas em lote;
- geração integral da Peça, tratada no PA-16;
- pesquisa de jurisprudência ou legislação;
- mudança de modelo ou fallback;
- tela administrativa de feature flags.

## Critério de fechamento

O PA-18 pode ser encerrado quando:

- o chat for a interface principal do assistente;
- modos e escopos técnicos não forem exigidos no fluxo normal;
- seleção e demais contextos estiverem visíveis como chips removíveis;
- resposta extensa puder ser lida em painel redimensionável ou expandido;
- sugestões forem revisadas e aplicadas dentro da conversa;
- pergunta, alteração, revisão, contexto adicional e âncora expirada forem
  comprovados por testes e validação manual;
- a experiência receber aprovação do responsável pelo produto.
