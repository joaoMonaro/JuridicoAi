# Tópico 8 — Aplicação da IA com autosave em erro

> **Estado da decisão:** aprovado
>
> **Estado da implementação:** parcial
>
> **Data da decisão:** 2026-09-27
>
> **Ponto encerrado:** `PA-08`

Este documento define quando uma sugestão da IA pode alterar a Peça diante dos
estados do autosave. A edição manual local e a aplicação de conteúdo gerado pela
IA possuem permissões diferentes.

## 1. Decisão aprovada

O usuário pode continuar editando manualmente quando o autosave falhar. Uma
sugestão da IA, porém, só pode ser aplicada quando a Peça estiver sincronizada
com o backend.

```text
SALVO
  -> edição manual permitida
  -> aplicação da IA permitida

SALVANDO
  -> edição manual permitida
  -> aplicação da IA aguarda o resultado do autosave

ERRO / NÃO SALVO
  -> edição manual permitida
  -> aplicação da IA bloqueada

CONFLITO
  -> aplicação da IA bloqueada
  -> exige resolução do conflito
```

Essa condição é adicional à validade da âncora. Estar em `SALVO` não torna uma
sugestão expirada aplicável.

## 2. Justificativa

Exemplo de estado inseguro:

```text
Backend: versão 14
Frontend: conteúdo local da versão 15, ainda não persistido
IA: sugestão calculada sobre o conteúdo local
```

Aplicar outra alteração automática nesse estado acumularia mudanças sobre uma
base que o servidor ainda não confirmou. Em caso de falha, conflito, recarga ou
troca de Peça, ficaria difícil determinar a versão persistida e comunicar com
clareza quais mudanças existem apenas localmente.

O bloqueio não impede o usuário de trabalhar. Ele restringe somente a ação que
escreve uma sugestão da IA no editor.

## 3. Matriz de permissões

Os nomes abaixo correspondem ao tipo `SaveState` existente no frontend.

| Estado | Editar manualmente | Consultar/gerar com IA | Aplicar sugestão da IA | Ação esperada |
|---|:---:|:---:|:---:|---|
| `SALVO` | Sim | Sim | Sim, se as demais validações passarem | Aplicar e iniciar novo ciclo de autosave |
| `SALVANDO` | Sim | Sim | Não | Aguardar conclusão |
| `NAO_SALVO` | Sim | Sim | Não | Salvar ou tentar novamente |
| `ERRO` | Sim | Sim | Não | Exibir erro e permitir retry |
| `CONFLITO` | Conforme fluxo do editor | Sim, sem escrita | Não | Resolver o conflito |

Consultar ou gerar continua permitido porque essas ações não escrevem na Peça.
O snapshot enviado à IA representa o conteúdo local autorizado pelo usuário.
Aplicar a resposta continua condicionado a `SALVO`, à âncora válida e à ausência
de conflito.

## 4. Experiência do usuário

Em `ERRO` ou `NAO_SALVO`, apresentar:

> Não foi possível salvar a Peça. Suas alterações continuam neste dispositivo,
> mas sugestões da IA não podem ser aplicadas até que a sincronização seja
> restabelecida.

A interface deve manter:

- edição manual;
- undo e redo locais;
- ação para tentar salvar novamente;
- sugestão visível, editável e copiável;
- descarte da sugestão sem alterar a Peça.

Em `SALVANDO`, a ação de aplicar fica desabilitada e informa que está aguardando
a sincronização. O sistema não deve enfileirar uma aplicação automática: depois
de chegar a `SALVO`, o usuário ainda confirma a ação explicitamente.

Quando o retry terminar em `SALVO`, a aplicação só é reabilitada se a Peça, a
revisão local e a âncora continuarem válidas. Se o usuário editou o documento
nesse intervalo, prevalece a regra de âncora expirada do
[Tópico 7](topico-07-ancora-expirada.md).

## 5. Aplicação seguida de nova falha

Uma aplicação iniciada em `SALVO` altera o TipTap em uma única transação e
dispara o autosave normal. Durante esse novo ciclo, o estado passa por
`NAO_SALVO`/`SALVANDO`.

Se esse autosave falhar:

- a alteração aplicada permanece local e pode ser desfeita;
- a interface não afirma que ela foi salva;
- novas aplicações da IA ficam bloqueadas;
- o usuário pode tentar salvar novamente;
- não ocorre rollback automático nem uma segunda aplicação automática.

## 6. Relação com a implementação atual

Já está implementado:

- estados tipados `SALVO`, `NAO_SALVO`, `SALVANDO`, `ERRO` e `CONFLITO`;
- edição manual independente do estado do autosave;
- retry de salvamento;
- undo local por meio das transações do TipTap;
- bloqueio da aplicação da IA em `CONFLITO`.

Ainda precisa ser implementado ou refinado:

- permitir aplicação somente em `SALVO`;
- mensagens específicas para `SALVANDO`, `ERRO` e `NAO_SALVO`;
- reavaliação conjunta de autosave e âncora após retry;
- indicação de que uma sugestão aplicada pode continuar apenas local caso o
  autosave posterior falhe;
- testes automatizados do fluxo completo.

## 7. Plano técnico do frontend

- centralizar a permissão de escrita em uma função pura, por exemplo
  `canApplyAiSuggestion`;
- exigir simultaneamente `saveState === "SALVO"`, âncora válida, texto aplicável
  e ausência de fase final;
- não executar `applySuggestion` enquanto o estado estiver bloqueado;
- não capturar o clique para execução futura durante `SALVANDO`;
- manter `retry()` do autosave como ação independente;
- após qualquer transição de estado, recalcular a permissão usando o snapshot
  original;
- manter a aplicação como uma única transação TipTap para preservar undo;
- deixar o autosave existente persistir a alteração, sem `PATCH` paralelo no
  cartão da IA.

## 8. Testes obrigatórios

- `SALVO` permite aplicação quando âncora e sugestão são válidas;
- `SALVANDO` desabilita aplicação e não a executa ao terminar;
- `NAO_SALVO` permite digitação e bloqueia aplicação;
- `ERRO` permite digitação, undo e retry, mas bloqueia aplicação;
- `CONFLITO` continua bloqueando aplicação;
- retry bem-sucedido reabilita somente uma sugestão cuja âncora ainda é válida;
- edição durante retry expira a âncora e mantém a sugestão inaplicável;
- falha depois de uma aplicação mantém o conteúdo local e permite undo;
- copiar, descartar e gerar resposta não alteram o estado salvo da Peça;
- nenhum estado bloqueado chama `applySuggestion` ou grava a Peça;
- conflito HTTP 409 mantém a aplicação bloqueada e informa o usuário;
- recarregar ou trocar de Peça com alterações locais exige o aviso já definido
  pelo fluxo do editor.

## 9. Critérios de aceite

- [ ] Somente `SALVO` permite iniciar a aplicação de uma sugestão.
- [ ] Edição manual permanece disponível em `ERRO` e `NAO_SALVO`.
- [ ] `SALVANDO` nunca agenda aplicação automática.
- [ ] A mensagem de conteúdo local não salvo é exibida nos estados aplicáveis.
- [ ] Retry de autosave permanece disponível e tem resultado observável.
- [ ] Undo local permanece disponível após aplicação e falha posterior.
- [ ] Âncora e estado de sincronização são validados no momento do clique.
- [ ] Testes automatizados comprovam permissões, recuperação e conflito 409.
