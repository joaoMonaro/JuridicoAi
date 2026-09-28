# Plano de implementação — frontend

> **Estado:** primeira fatia vertical implementada
>
> **Última atualização:** 2026-09-26
>
> **Repositório:** `../JuridicoAi-frontend`

## Base existente

O frontend usa React 19, TypeScript, Vite, Tailwind e TipTap 3. A Peça é editada
em `src/features/pecas/editor/legal-document-editor.tsx`; o componente mantém a
instância `Editor`, envia `editor.getJSON()` ao autosave e exibe conflito de
versão. O workspace está em `src/pages/CaseWorkspacePage.tsx`.

Esta feature deve integrar-se ao TipTap já existente. Não deve criar um segundo
estado canônico do documento nem editar diretamente o JSON mantido no React.

## Estado implementado

Em 2026-09-26, estão disponíveis:

- painel lateral responsivo aberto pela toolbar ou por `Ctrl/Cmd + Alt + A`;
- consulta dinâmica de capacidades e limites do backend;
- modos `Perguntar`, `Redigir`, `Reescrever` e `Revisar` com ações rápidas;
- escopos sem documento, seleção, seção atual e documento completo;
- seleção opcional de contextos textuais e dados estruturados do Caso;
- consumo do `POST` SSE com progresso, cancelamento e erros;
- resultado editável, comparação original/proposta, alertas e achados;
- aplicação explícita no TipTap em uma transação, integrada ao undo/redo e ao
  autosave existente;
- bloqueio da aplicação quando a revisão local mudou ou há conflito de versão.

O código está concentrado em `src/features/ai-assistant`, com integração em
`legal-document-editor.tsx` e uma variante de baixo nível `apiFetch` no cliente
HTTP. Build e lint passam. O repositório ainda não possui infraestrutura de
testes automatizados; por isso, os fluxos funcionais e a acessibilidade completa
permanecem como trabalho de validação, sem alegação de homologação ponta a ponta.

## Experiência alvo

O painel do assistente ocupa a lateral direita do workspace e pode ser aberto
pela toolbar, por ação sobre uma seleção e por atalho. Em telas estreitas, vira
um drawer. O editor continua utilizável durante uma resposta, mas qualquer
mudança posterior ao envio pode invalidar a âncora de aplicação.

Ao carregar a aplicação autenticada, o frontend consulta as capacidades do
backend. A entrada do assistente fica oculta ou identificada como indisponível
quando a feature estiver desabilitada; modos, limites e suporte a histórico não
são presumidos no bundle.

Para o rollout, essa consulta deve ocorrer antes de renderizar a toolbar e o
atalho. `AI_ASSISTANT` controla a entrada principal; `AI_EDIT`, `AI_REVIEW` e
`AI_DOCUMENT_GENERATION` filtram modos e ações. A API repete todas as
validações. Consulte o
[Tópico 13 — Feature flags e rollout](topico-13-feature-flags-rollout.md).

Estados principais:

```text
Fechado
  -> Compondo
  -> Enviando
  -> Recebendo
  -> Concluído
       -> Aplicado | Rejeitado | Editando | Nova tentativa
  -> Erro | Cancelado
```

O texto digitado pelo usuário permanece disponível em erro, cancelamento,
troca de escopo e reabertura do painel na mesma Peça.

## Estrutura implementada

```text
src/features/ai-assistant/
├── api.ts
├── components/
│   └── assistant-panel.tsx
├── editor.ts
└── types.ts
```

Nesta fatia, o painel mantém o estado transitório da solicitação, enquanto
`api.ts` encapsula o stream e `editor.ts` reúne as funções puras de captura,
validação de âncora e aplicação. A separação em componentes e hooks menores deve
ser feita quando testes ou novos fluxos justificarem essa complexidade.

## Integração com o editor

O painel precisa receber a mesma instância TipTap usada pelo documento. A
composição recomendada é manter `useEditor` no componente que contém editor e
painel, ou extrair um container de workspace do documento. Não usar eventos
globais adicionais para transportar seleção, conteúdo ou respostas de IA.

Os eventos globais atuais `peca:save` e `peca:patch` podem permanecer para suas
funções existentes, mas o novo fluxo deve usar props/contexto tipado.

### Captura de contexto

No envio, criar um snapshot imutável:

```ts
interface EditorRequestSnapshot {
  pecaId: string;
  pecaVersion: number;
  editorRevision: number;
  scope: "Nenhum" | "Selecao" | "SecaoAtual" | "DocumentoCompleto";
  text: string;
  sectionTitle: string | null;
  selection: {
    from: number;
    to: number;
    originalText: string;
  } | null;
  cursorPosition: number | null;
}
```

`editorRevision` é um contador local incrementado a cada transação que muda o
documento. Ele é diferente de `Peca.versao`: o primeiro detecta mudanças ainda
não salvas; o segundo representa concorrência no backend.

Para `SecaoAtual`, caminhar pela árvore TipTap até o heading anterior e coletar
até o próximo heading de nível igual ou superior. Se não houver headings, usar
o bloco atual e informar visualmente o escopo efetivo; nunca promover
silenciosamente para o documento completo.

O texto do documento deve ser serializado de forma determinística, preservando
separação de parágrafos e títulos. Marcas visuais não necessárias não entram no
MVP. A interface apresenta contagem aproximada e o nome de cada fonte de
contexto antes do envio.

No MVP, a interface também informa que o assistente não realiza pesquisa
jurídica externa. Não deve existir botão, estado ou texto que sugira consulta ao
scraper. Alertas `REFERENCIA_NAO_VERIFICADA` permanecem visíveis junto da
resposta e antes da aplicação. Consulte o
[Tópico 10 — Legislação, jurisprudência e fontes](topico-10-legislacao-jurisprudencia-fontes.md).

### Validade da âncora

Antes de habilitar “Substituir” ou “Inserir após”:

1. `peca.id` ainda é o mesmo;
2. `editorRevision` ainda corresponde à revisão base;
3. `from` e `to` continuam dentro do documento;
4. o texto atual no intervalo é igual a `originalText`.

Para inserção no cursor, revisão e posição também precisam coincidir. Se alguma
condição falhar, a sugestão continua legível e copiável, mas a ação original é
desabilitada. O usuário deve selecionar outro trecho e gerar novamente, ou
descartar a sugestão. A resposta antiga nunca é aplicada à nova seleção.

Mapeamento de posições através de transações pode ser acrescentado depois. O
MVP privilegia segurança e previsibilidade.

A política completa está no
[Tópico 7 — Âncora expirada](topico-07-ancora-expirada.md). Inserir no cursor
atual não é fallback do MVP; se existir futuramente, será uma operação explícita
de inserção, não reaplicação da alteração original.

### Aplicação

- converter texto final em nós TipTap permitidos;
- separar parágrafos por linhas em branco;
- não interpretar HTML retornado pelo modelo;
- aplicar em uma única `editor.chain()`/transação;
- focar o destino e preservar undo/redo;
- deixar o `onUpdate` existente acionar autosave;
- não chamar `PATCH /pecas` diretamente a partir do cartão de sugestão;
- marcar a sugestão como aplicada somente se `chain.run()` retornar sucesso.

Uma sugestão da IA só pode ser aplicada quando o autosave estiver em `SALVO`.
Em `SALVANDO`, a aplicação aguarda, sem ser enfileirada automaticamente. Em
`ERRO`, `NAO_SALVO` ou `CONFLITO`, ela fica bloqueada. A edição manual, o undo e
o retry de salvamento continuam disponíveis.

Se o autosave iniciado depois de uma aplicação falhar, a alteração permanece
local e pode ser desfeita, mas a interface não a apresenta como persistida e
bloqueia novas aplicações. A política completa está no
[Tópico 8 — Aplicação com autosave em erro](topico-08-autosave-em-erro.md).

## Painel e controles

### Composer

- seletor de modo;
- textarea de instrução;
- ações rápidas contextuais;
- chips das fontes de contexto;
- campo “Contexto adicional” separado da instrução;
- botão enviar com resumo do que será compartilhado;
- botão cancelar durante geração.

Ações rápidas iniciais:

- com seleção: `Reescrever`, `Expandir`, `Resumir`, `Corrigir clareza`;
- sem seleção: `Redigir trecho`, `Sugerir melhorias`, `Perguntar sobre a peça`.

As ações rápidas preenchem modo e instrução, mas permanecem editáveis.

### Resultado

- delta progressivo em região com `aria-live="polite"` sem anunciar cada token;
- cabeçalho com modo e escopo usado;
- texto final editável em uma cópia, nunca no original recebido;
- diff para `SubstituirSelecao`;
- alertas separados e sempre visíveis;
- achados de revisão estruturados, cada um com ação `Desenvolver`;
- ações `Inserir/Substituir`, `Copiar`, `Rejeitar`, `Tentar novamente`;
- `Desfazer no editor` após aplicação, usando o histórico TipTap existente.

Editar a proposta no painel não afeta o documento. O botão de aplicação sempre
mostra o verbo e o destino, por exemplo “Substituir seleção” ou “Inserir no
cursor atual”; evitar um botão genérico “Aceitar”.

## Consumo do stream

`stream-assistant-request.ts` usa o `api-client` para obter autenticação, mas
precisará de uma variante que exponha o `ReadableStream` em vez de desserializar
JSON. O parser deve:

- aceitar chunks divididos no meio de uma linha UTF-8;
- processar múltiplos eventos em um mesmo chunk;
- ignorar comentários/keep-alive;
- validar `event` e JSON antes de atualizar estado;
- considerar apenas `solicitacao.concluida` como resultado aplicável;
- mapear `ProblemDetails` quando a resposta nem chegar a iniciar o SSE;
- abortar por `AbortController` ao cancelar ou trocar de Peça.

Não montar tipos duplicados divergentes do
[contrato compartilhado](contrato-api.md). Enquanto não houver geração
automática de cliente, manter os nomes lado a lado e revisar ambos no mesmo PR.

## Autosave e concorrência

A aplicação é uma edição normal do TipTap e entra na fila já existente de
autosave. Para integrar o assistente com segurança, o hook de autosave deverá
expor de forma tipada:

- estado atual;
- última versão confirmada;
- `flush(): Promise` com resultado observável;
- `retry(): Promise` com resultado observável;
- indicação de conflito que possa desabilitar aplicação.

O assistente envia a versão conhecida para diagnóstico, mas não depende de
salvar antes de perguntar: o snapshot explícito representa exatamente o texto
autorizado pelo usuário. O `PATCH` continua sendo a única operação que persiste
uma aplicação.

Se houver 409 depois de aplicar localmente, a UI não deve alegar que a sugestão
foi salva. O fluxo existente de recarga precisa alertar sobre descarte de
alterações locais; merge automático fica fora do MVP.

## Acessibilidade e atalhos

- painel, drawer, botões e menus operáveis por teclado;
- foco retorna ao editor ao fechar/aplicar;
- `Escape` fecha menu ou cancela somente a camada ativa;
- atalhos não conflitam com undo, save e formatação;
- diff não depende apenas de vermelho/verde;
- loading possui rótulo e cancelamento;
- alertas têm texto e relação programática com a sugestão.

Atalho implementado, sujeito a teste nos sistemas suportados:

```text
Ctrl/Cmd + Alt + A -> abrir/focar assistente
```

## Testes mínimos

A estratégia, ferramentas, matriz de cenários e critérios de fechamento estão
no [Tópico 12 — Testes e acessibilidade](topico-12-testes-acessibilidade.md).
O alvo é Vitest para unidades, React Testing Library para componentes,
Playwright para E2E e axe-core para verificações automatizadas.

### Funções puras

- seleção e cursor são capturados corretamente;
- extração da seção respeita headings;
- serialização de texto é determinística;
- conversão texto -> TipTap não aceita HTML;
- âncora válida/inválida em cada condição;
- sugestão vira uma única transação.

### Hook e protocolo

- evento completo em um chunk;
- SSE fragmentado em vários chunks;
- vários eventos no mesmo chunk;
- UTF-8 multibyte fragmentado;
- `[DONE]`, duplicação, conclusão, falha, cancelamento e EOF inesperado;
- troca de Peça aborta stream e impede atualização tardia;
- nova tentativa cria novo `clientRequestId`.

### Componente/integração

- sem seleção, perguntar não oferece escrita;
- com seleção, reescrever mostra diff;
- documento alterado durante geração invalida aplicação;
- aceitar altera TipTap somente após clique;
- rejeitar e cancelar não alteram JSON nem autosave;
- aplicar pode ser desfeito;
- conflito de versão bloqueia aplicação;
- alertas de verificação permanecem visíveis.

### E2E e acessibilidade

- pedir alteração, receber stream, aplicar, concluir autosave e validar reload;
- conflito, erro, retry, cancelamento e troca de Peça;
- axe-core nos estados principais, sem violações críticas ou sérias;
- teclado, foco, ARIA, contraste e smoke test com leitor de tela;
- `Ctrl + Alt + A` em Windows/Linux e `Cmd + Alt + A` em macOS.

## Sequência recomendada

- [x] Integrar editor, revisão local e estado do autosave por interfaces tipadas.
- [x] Implementar captura/serialização de contexto.
- [x] Criar painel responsivo para desktop/drawer.
- [x] Implementar parser SSE e integração com o endpoint.
- [x] Implementar cartão, edição de proposta e comparação.
- [x] Implementar validação de âncora e transações TipTap.
- [x] Integrar autosave, conflito, cancelamento e troca de Peça.
- [ ] Restringir aplicação da IA a `SALVO` e testar erro, retry e conflito.
- [ ] Refinar a UX de âncora expirada com seleção guiada e nova geração.
- [ ] Adicionar infraestrutura e testes automatizados dos fluxos críticos.
- [ ] Executar a suíte e auditoria de acessibilidade definidas no Tópico 12.
- [ ] Adicionar telemetria sem conteúdo sensível.
- [ ] Consumir capacidades por Organization e ocultar toolbar, atalho e modos
      conforme o Tópico 13.
- [ ] Propagar `CorrelationId` e emitir eventos de produto sem conteúdo conforme
      o Tópico 14.
- [x] Atualizar checklist e links no documento principal.
