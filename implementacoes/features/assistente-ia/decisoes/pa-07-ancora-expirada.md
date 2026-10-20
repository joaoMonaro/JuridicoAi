# Tópico 7 — Âncora expirada e destino alternativo

> **Estado da decisão:** aprovado
>
> **Estado da implementação:** bloqueio seguro implementado e coberto por
> testes; recuperação guiada parcial
>
> **Data da decisão:** 2026-09-27
>
> **Última conferência documental:** 2026-10-18
>
> **Ponto encerrado:** `PA-07`

Este documento define o comportamento quando o usuário altera a Peça depois de
enviar uma solicitação cuja resposta depende de uma seleção ou posição do
editor. A prioridade é impedir que uma sugestão seja aplicada em conteúdo
diferente daquele que originou a geração.

## 1. Decisão aprovada

O fluxo obrigatório é:

```text
Sugestão criada
      |
      v
Âncora ainda válida?
   /             \
 sim             não
  |               |
  v               v
aplicar         bloquear
```

Não haverá remapeamento silencioso de posições, busca aproximada pelo texto ou
aplicação automática em outro destino.

Quando a âncora estiver expirada, o usuário poderá:

1. selecionar um novo trecho;
2. gerar novamente, criando nova solicitação e nova âncora;
3. descartar a sugestão anterior.

A sugestão vencida pode continuar visível e copiável, mas nunca reaplicável no
novo trecho. Selecionar outro texto não transforma a resposta antiga em uma
resposta válida para essa seleção.

## 2. O que torna uma âncora válida

Para substituição ou inserção relacionada a uma seleção, todos os critérios
precisam continuar verdadeiros:

1. a Peça aberta é a mesma da solicitação;
2. a revisão local do editor é a revisão capturada no envio;
3. `from` e `to` continuam dentro dos limites do documento;
4. o texto atual entre `from` e `to` é exatamente `originalText`;
5. não existe conflito de versão que bloqueie a edição.

Para inserção no cursor ou criação de seção, a Peça, a revisão local e a posição
original também precisam coincidir. Uma alteração em qualquer ponto do
documento invalida a aplicação, mesmo que o texto selecionado ainda pareça
igual. Essa regra conservadora evita depender de mapeamento de transações no
MVP.

Respostas do tipo `ResponderNoPainel` não escrevem na Peça e continuam legíveis;
elas não precisam oferecer ação de aplicação.

## 3. Experiência quando a âncora expirar

O cartão deve informar:

> O documento mudou desde o envio. Para evitar alterar o trecho errado, esta
> sugestão não pode mais ser aplicada. Selecione o trecho desejado e gere uma
> nova sugestão.

Estados e ações:

```text
Sugestão válida
  -> Aplicar | Copiar | Gerar novamente | Descartar

Sugestão expirada
  -> Selecionar novo trecho
  -> Gerar novamente (após seleção válida)
  -> Copiar
  -> Descartar
```

`Selecionar novo trecho` devolve foco ao editor e coloca o painel em estado de
aguardo. `Gerar novamente` captura a seleção atual, cria novo
`clientRequestId`, novo snapshot e nova revisão base. A instrução e o contexto
adicional permanecem editáveis; a resposta antiga não é reaproveitada como se
tivesse sido produzida para o novo trecho.

`Descartar` remove a sugestão da sessão visual sem editar a Peça e, quando o
histórico do Tópico 3 estiver implementado, registra apenas a decisão explícita
de descarte vinculada à sugestão.

## 4. Inserir no cursor atual

`Inserir no cursor atual` não será oferecido como recuperação padrão no MVP.
Uma resposta criada para substituir um parágrafo pode não ser adequada a outro
ponto do documento.

Se essa capacidade for adicionada futuramente, deverá:

- aparecer como ação explícita e secundária;
- explicar que o texto será inserido, não reaplicado como alteração;
- criar uma nova operação `InserirNoCursor` com destino capturado no momento da
  confirmação;
- manter preview e confirmação antes de escrever;
- registrar a mudança de semântica para auditoria;
- aplicar em uma única transação TipTap, preservando undo e autosave.

Ela nunca poderá ser um fallback automático de `SubstituirSelecao`.

## 5. Relação com a implementação atual

Já está implementado:

- contador de revisão local incrementado em alterações do TipTap;
- snapshot com posição, texto original e revisão;
- comparação de revisão, limites e texto original;
- bloqueio da ação quando a âncora não coincide;
- mensagem orientando nova geração;
- ações de copiar, tentar novamente e rejeitar;
- bloqueio adicional em conflito de autosave.

Ainda precisa ser refinado:

- ação explícita `Selecionar novo trecho` com retorno de foco;
- estado visual aguardando nova seleção;
- habilitação de `Gerar novamente` somente após seleção válida nos modos que a
  exigem;
- nomenclatura `Descartar` no lugar de `Rejeitar` para a âncora expirada;
- cobertura completa da recuperação guiada; os testes existentes já cobrem
  bloqueio e ausência de transação em âncora expirada.

Na interface conversacional atual, `Tentar novamente` cria outro identificador,
mas reutiliza o `selectionSnapshot` do turno original. Essa ação não captura uma
nova seleção. O usuário precisa selecionar o trecho no editor e enviar um novo
pedido pelo composer para usar outro snapshot. Selecionar texto não torna a
proposta antiga aplicável. A [conferência de código](../evidencias/conferencia-documental-2026-10-18.md)
preserva os itens de recuperação como pendentes.

## 6. Plano técnico

### Frontend

- manter o snapshot original imutável junto da sugestão;
- representar o estado como `ready`, `stale`, `applied` ou `discarded`;
- invalidar `ready` para `stale` em qualquer transação que altere o documento;
- nunca retornar de `stale` para `ready`, mesmo se o usuário desfizer a edição;
- ao selecionar novo trecho, capturar outro snapshot apenas na nova solicitação;
- cancelar stream anterior antes da nova geração;
- preservar instrução, modo e contexto livre entre as tentativas;
- impedir qualquer chamada de `applySuggestion` para sugestão `stale`.

### Backend e contrato

O backend continua sem autoridade para aplicar alterações. Cada nova geração
recebe novo `clientRequestId` e nova âncora no corpo. Não será criado endpoint
para reaplicar ou mover sugestão antiga.

Quando o histórico estiver disponível, o estado `stale` pode ser persistido
como metadado da sugestão, sem gravar conteúdo no log técnico.

### Testes

- alterar texto dentro da seleção invalida a aplicação;
- alterar texto fora da seleção também invalida pela revisão local;
- desfazer a edição não revalida a sugestão antiga;
- trocar de Peça impede aplicação;
- posição fora dos limites impede aplicação;
- texto diferente com a mesma posição impede aplicação;
- selecionar novo trecho não habilita aplicação da resposta antiga;
- gerar novamente cria identificador, snapshot e âncora novos;
- descartar, copiar ou falhar não altera `conteudoJson`;
- conflito de autosave continua bloqueando aplicação.

## 7. Critérios de aceite

- [x] Sugestão expirada não executa transação de aplicação.
- [x] A interface explica por que a aplicação foi bloqueada.
- [ ] Ação guiada devolve o foco ao editor para selecionar outro trecho.
- [ ] Regeneração guiada usa exclusivamente a nova seleção e uma nova âncora.
- [x] A sugestão antiga continua visível/copiável e não pode ser aplicada.
- [x] Rejeitar não altera Peça, versão ou autosave.
- [x] Não existe fallback automático para cursor atual.
- [ ] Testes automatizados cobrem mudança, undo, troca de Peça e nova geração.

