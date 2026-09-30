# Tópico 12 — Testes e acessibilidade do frontend

> **Estado da decisão:** estratégia aprovada
>
> **Estado da implementação:** em andamento — unitários, componentes e E2E principal configurados
>
> **Data da decisão:** 2026-09-27
>
> **Ponto relacionado:** `PA-12` permanece aberto e bloqueia produção

Este documento define a suíte mínima do frontend para o assistente de IA. O
`PA-12` só poderá ser encerrado quando os testes existirem, forem executáveis de
forma reproduzível e houver evidência da auditoria de acessibilidade.

## 1. Pirâmide de testes aprovada

| Nível | Ferramenta | Responsabilidade principal |
|---|---|---|
| Unitário | Vitest | parser SSE, funções de editor, âncoras e regras de estado |
| Componente | React Testing Library | comportamento do painel, interação, foco e ARIA |
| E2E | Playwright | fluxo completo entre editor, stream, aplicação e autosave |

`axe-core` será usado para verificações automatizadas de acessibilidade em
componentes e fluxos E2E. Testes automatizados complementam, mas não substituem,
verificação por teclado, contraste e leitor de tela.

## 2. Infraestrutura necessária

O frontend deverá possuir, no mínimo:

```text
Vitest
ambiente DOM para testes
React Testing Library
user-event
matchers de DOM
Playwright
axe-core integrado aos testes
```

Scripts-alvo:

```json
{
  "scripts": {
    "test": "vitest",
    "test:run": "vitest run",
    "test:e2e": "playwright test",
    "test:a11y": "playwright test e2e/accessibility.spec.ts",
    "test:ci": "npm run test:run && npm run test:e2e"
  }
}
```

Os nomes podem ser ajustados à configuração final, mantendo um comando não
interativo único para CI. Nenhum teste chama a OpenAI real ou usa dados reais de
clientes.

## 3. Parser SSE

O parser deve ser extraído para uma unidade testável que receba bytes/chunks e
produza eventos tipados. A suíte precisa cobrir:

| Cenário | Resultado esperado |
|---|---|
| evento completo em um chunk | evento emitido uma única vez |
| evento dividido em vários chunks | remontagem sem perda ou duplicação |
| vários eventos no mesmo chunk | todos processados na ordem correta |
| UTF-8 multibyte dividido | caractere reconstruído corretamente |
| `CRLF` e `LF` | ambos aceitos sem alterar o conteúdo do `data` |
| múltiplas linhas `data:` | conteúdo combinado conforme SSE |
| stream encerrado no meio | `AI_STREAM_INCOMPLETE`; sugestão não aplicável |
| `solicitacao.falhou` | erro tipado, terminal e exibível |
| cancelamento | leitura interrompida sem atualização tardia de estado |
| `[DONE]` | ignorado como marcador; não substitui o evento final do produto |
| evento terminal duplicado | callback terminal executado apenas uma vez |
| delta duplicado | não acrescentado novamente quando possuir o mesmo ID/sequência |
| evento desconhecido | ignorado com segurança, sem encerrar o fluxo |
| JSON inválido | erro de protocolo controlado |

O contrato do produto continua exigindo exatamente um evento terminal:
`solicitacao.concluida` ou `solicitacao.falhou`. Receber `[DONE]` sem um desses
eventos resulta em stream incompleto.

### Deduplicação

Não é seguro deduplicar deltas comparando apenas o texto, pois repetições podem
ser conteúdo legítimo. O contrato usa o campo SSE `id` no formato
`<solicitacaoId>:<sequencia>`. O frontend ignora uma sequência repetida ou antiga,
aceita textos iguais em sequências diferentes e rejeita lacunas, ausência de ID
ou troca de solicitação como protocolo inválido.

Reconexão automática e retomada por `Last-Event-ID` ficam fora do MVP. Mesmo sem
reconexão, eventos terminais repetidos não podem executar callbacks ou alterar
estado duas vezes.

## 4. TipTap e regras de aplicação

Os testes unitários devem usar uma instância real do editor em ambiente DOM
sempre que o comportamento depender de seleção, transação, histórico ou árvore
ProseMirror. Mocks isolados não comprovam undo, redo ou transação única.

Cobertura obrigatória:

- captura de cursor e seleção;
- texto original, `from`, `to` e revisão no snapshot;
- extração da seção atual por headings;
- serialização determinística do documento;
- âncora válida;
- âncora expirada por edição dentro ou fora da seleção;
- âncora expirada após troca de Peça;
- posição fora dos limites;
- seleção com texto diferente na mesma posição;
- aplicação de substituição e inserção;
- conversão de texto em parágrafos sem interpretar HTML;
- exatamente uma transação por aplicação;
- undo restaura o estado anterior;
- redo reaplica a mesma alteração;
- sugestão expirada nunca chama a transação;
- aplicação bloqueada em `SALVANDO`, `NAO_SALVO`, `ERRO` e `CONFLITO`;
- aplicação permitida em `SALVO`, quando as demais condições são válidas.

Os testes de âncora seguem o [Tópico 7](topico-07-ancora-expirada.md), e os de
sincronização seguem o [Tópico 8](topico-08-autosave-em-erro.md).

## 5. Componentes

Com React Testing Library, validar o comportamento observável pelo usuário:

- abrir e fechar painel e drawer;
- enviar com e sem seleção;
- impedir envio quando faltarem dados obrigatórios;
- mostrar quais contextos serão compartilhados;
- renderizar início, deltas, conclusão e falha;
- cancelar e impedir atualização tardia;
- mostrar original, sugestão e diff;
- editar a sugestão sem alterar a Peça;
- aplicar somente após ação explícita;
- copiar, gerar novamente e descartar;
- bloquear âncora expirada com mensagem adequada;
- bloquear aplicação enquanto o autosave não estiver em `SALVO`;
- preservar instrução e contexto após erro recuperável;
- manter alertas de verificação visíveis;
- anunciar loading, erro e conclusão sem excesso de mensagens.

Os testes devem consultar os elementos por papel, nome acessível e rótulo. IDs
de teste ficam restritos a situações em que não exista uma semântica adequada.

## 6. E2E

Playwright deve executar o frontend contra backend ou servidor de teste
determinístico, usando provider fake. A suíte não acessa rede externa.

Fluxo principal obrigatório:

```text
abrir Peça
    -> selecionar trecho
    -> pedir alteração
    -> receber stream fragmentado
    -> revisar proposta
    -> aplicar
    -> autosave concluir
    -> recarregar página
    -> conteúdo aplicado continuar persistido
```

Outros cenários obrigatórios:

- perguntar sem seleção não altera a Peça;
- cancelar durante o stream mantém o documento intacto;
- editar durante a geração expira a âncora;
- erro do provider preserva o texto local;
- erro de autosave bloqueia nova aplicação e permite retry;
- retry bem-sucedido restabelece aplicação somente com âncora válida;
- conflito HTTP 409 bloqueia aplicação e orienta resolução;
- reload com alteração não salva apresenta aviso de perda;
- trocar de Peça aborta stream e impede resposta tardia;
- aplicar, desfazer e refazer produz conteúdo e versão esperados;
- nenhum fluxo aplica uma sugestão duas vezes.

O teste E2E deve observar a requisição de autosave e confirmar persistência após
reload; verificar somente a aparência do editor não comprova a gravação.

## 7. Acessibilidade

### Verificação automatizada

Executar `axe-core` pelo menos em:

- painel lateral e drawer;
- composer vazio, preenchido e inválido;
- estado de streaming e cancelamento;
- cartão de sugestão e diff;
- alertas, erro de provider e erro de autosave;
- confirmação de contexto;
- estado de âncora expirada.

O gate não permite violações críticas ou sérias. Violações moderadas ou menores
precisam ser corrigidas ou justificadas e registradas antes do fechamento.

### Teclado e foco

- toda ação é alcançável e executável sem mouse;
- ordem de tabulação acompanha a ordem visual e semântica;
- foco entra no painel em ponto previsível;
- foco retorna ao editor ao fechar ou aplicar;
- `Escape` afeta somente a camada ativa;
- modal/drawer prende foco apenas enquanto estiver modal;
- loading não rouba foco;
- erro e conclusão são anunciados de forma programática;
- diff não depende apenas de cor.

### ARIA, contraste e leitor de tela

- botões de ícone possuem nome acessível;
- campos possuem label e mensagens associadas;
- regiões, diálogo/drawer e alertas possuem papéis coerentes;
- atualizações importantes usam região viva sem anunciar cada token;
- contraste é verificado nos estados normal, hover, foco, erro e desabilitado;
- executar smoke test documentado com leitor de tela, incluindo abertura,
  solicitação, leitura da proposta, aplicação e erro.

## 8. Atalhos e plataformas

O atalho do assistente deve respeitar:

```text
Windows/Linux -> Ctrl + Alt + A
macOS         -> Cmd  + Alt + A
```

Testar:

- modificador correto por plataforma;
- combinação sem modificador não abre o painel;
- atalho não dispara ao compor texto quando houver conflito conhecido;
- repetição da tecla não abre múltiplas instâncias;
- foco resultante é previsível;
- atalhos de undo/redo continuam funcionando;
- rótulo visual corresponde à plataforma.

A comprovação pode combinar testes automatizados da interpretação dos eventos
com execução Playwright em matriz Windows, Linux e macOS. Quando uma plataforma
não estiver disponível no CI, deve existir smoke test manual registrado nela.

## 9. Execução e estabilidade

Antes do merge de mudanças no parser, editor ou painel:

- unitários e componentes executam no CI;
- E2E crítico executa com provider fake determinístico;
- artefatos de falha preservam trace, screenshot e vídeo sem dados reais;
- testes SSE e E2E críticos passam em três execuções consecutivas antes de
  serem considerados estáveis;
- teste instável não pode ser apenas ignorado; deve ser corrigido ou possuir
  responsável e prazo registrados.

Não será usado apenas percentual global de cobertura como critério. Todos os
cenários críticos listados neste documento são obrigatórios, ainda que a média
de cobertura do projeto seja alta.

## 10. Situação encontrada

No momento da decisão:

- o frontend não possui Vitest instalado;
- React Testing Library não está instalada;
- Playwright não está instalado;
- axe-core não está instalado;
- não existem scripts `test` ou `test:e2e`;
- não foram encontrados arquivos de teste automatizado;
- o parser SSE está acoplado ao cliente HTTP;
- o parser usa `TextDecoder` incremental, mas não trata `[DONE]`;
- os eventos não possuem ID/sequência para deduplicar deltas.

Portanto, nenhuma caixa de entrega pode ser marcada apenas com base neste plano.

### Situação atual — 2026-09-30

Entregas iniciais concluídas no frontend:

- Vitest `5`, jsdom `29`, React Testing Library, jest-dom e user-event
  instalados como dependências de desenvolvimento;
- configuração isolada em `vitest.config.ts`, com ambiente jsdom, alias `@`,
  limpeza de mocks e setup comum;
- `src/test/setup.ts` registra os matchers de DOM e limpa a renderização após
  cada teste;
- scripts `test`, `test:run` e `test:ci` disponíveis; nesta etapa,
  `test:ci` executa somente a suíte Vitest e será ampliado quando o Playwright
  for configurado;
- teste inicial do componente `Button` comprova renderização React, consulta
  por papel e nome acessível, interação real com user-event e matchers de DOM;
- parser SSE extraído do cliente HTTP para `sse-parser.ts`, separando leitura
  incremental, interpretação do protocolo e despacho dos eventos do assistente;
- quinze testes do stream cobrem evento completo, múltiplos eventos, fragmentação
  byte a byte, UTF-8 dividido, `LF`, `CRLF`, múltiplas linhas `data:`, evento
  desconhecido, falha tipada, cancelamento, `[DONE]`, JSON inválido, terminal
  duplicado, delta duplicado, repetição textual legítima, sequência antiga,
  lacuna, mistura de solicitação, ausência de ID e encerramento no meio;
- encerramento sem `solicitacao.concluida` ou `solicitacao.falhou` retorna
  `AI_STREAM_INCOMPLETE`, e um evento parcial nunca se torna sugestão aplicável;
- dezoito testes com uma instância real do TipTap cobrem cursor, seleção,
  seção atual, documento completo determinístico, revisão e validade da âncora,
  limites de posição, substituição, inserção, texto literal sem interpretação
  de HTML, transação única, undo e redo;
- aplicação da sugestão agora também valida âncora, revisão e estado de
  sincronização dentro da própria função de edição, e não apenas no botão do
  painel;
- `SALVANDO`, `NAO_SALVO`, `ERRO` e `CONFLITO` bloqueiam aplicação sem criar
  transação; `SALVO` permite quando as demais condições forem válidas;
- o painel informa o motivo do bloqueio para todos os estados não sincronizados,
  corrigindo a implementação anterior que verificava apenas `CONFLITO`;
- quatorze testes de componente com React Testing Library cobrem abertura e
  fechamento, validação, envio com e sem seleção, contexto compartilhado,
  streaming, conclusão, falha, cancelamento, edição e aplicação explícita,
  cópia, regeneração, rejeição, âncora expirada e bloqueios do autosave;
- instrução e contexto adicional permanecem preenchidos após erro recuperável,
  e alertas da sugestão continuam visíveis para revisão humana;
- a região viva anuncia somente o estado da geração, sem anunciar cada delta,
  e a conclusão é comunicada uma única vez por `role="status"`;
- Playwright configurado inicialmente para Chromium, com trace, screenshot e
  vídeo preservados somente em falha e diretórios de artefatos ignorados pelo
  Git;
- servidor HTTP fake local fornece Caso, duas Peças, respostas por operação,
  capacidades, SSE fragmentado e persistência em memória, sem chamar backend
  externo ou OpenAI; cenários configuráveis simulam resposta lenta, erro `503`,
  falha de autosave e conflito `409`;
- o E2E principal abre a Peça, seleciona texto, recebe o stream fragmentado,
  revisa e aplica a sugestão, observa o `PATCH` do autosave e confirma a
  persistência após reload;
- cinco E2E de resiliência comprovam que cancelamento e erro do provider mantêm
  o documento intacto, edição concorrente expira a âncora, erro de autosave
  bloqueia aplicação até um retry bem-sucedido e conflito de versão bloqueia a
  sugestão com orientação para recarregar;
- `@axe-core/playwright` integrado ao Chromium com comando dedicado
  `test:a11y` e gate para qualquer violação crítica ou séria;
- seis estados possuem scan automatizado: painel vazio, streaming, sugestão com
  diff, erro do provider, erro de autosave e âncora expirada;
- a primeira execução identificou e levou à correção do nome acessível do campo
  de proposta e do contraste de botões, item selecionado e alertas; depois das
  correções, os seis scans passaram sem violações críticas ou sérias;
- oito testes unitários validam identificação de plataforma, rótulo do atalho,
  modificador correto e rejeição de eventos repetidos ou ambíguos;
- cinco E2E de teclado cobrem `Ctrl+Alt+A`, `Cmd+Alt+A` com plataforma simulada,
  foco inicial, ordem principal de `Tab`/`Shift+Tab`, cancelamento e fechamento
  com `Escape`, aplicação com `Enter`, retorno ao editor, undo e redo;
- o painel agora aguarda as capacidades carregarem antes de focar o campo de
  instrução; atalhos repetidos são ignorados e o rótulo mostra `Ctrl` ou `Cmd`
  conforme a plataforma;
- cinco E2E de integridade completam a seção 6: pergunta sem seleção não altera
  a Peça, troca de Peça aborta o stream, sugestão não é aplicada duas vezes,
  undo/redo geram um único autosave e uma única nova versão, e reload com
  alteração não salva apresenta o aviso nativo antes do descarte;
- o editor registra proteção `beforeunload` enquanto estiver em `NAO_SALVO`,
  `SALVANDO`, `ERRO` ou `CONFLITO`, removendo-a quando retorna a `SALVO`;
- as verificações que exigem hardware, sistema e tecnologia assistiva reais
  foram registradas no
  [checklist manual de acessibilidade](checklist-manual-acessibilidade.md);
- `test:ci` executa Vitest e Playwright sequencialmente em modo não interativo;
- teste, lint e build executados com sucesso: `56/56` testes Vitest, `104/104`
  testes do backend e `22/22` testes Playwright aprovados; o E2E crítico
  principal, os cinco cenários de resiliência, os seis scans de acessibilidade,
  os cinco fluxos de teclado e os cinco fluxos de integridade passaram em três
  execuções consecutivas.

Continuam pendentes nesta infraestrutura: scan do estado de confirmação de
contexto e execução do checklist manual de teclado físico, foco visual,
contraste e leitor de tela.

## 11. Critérios para encerrar o PA-12

- [x] Vitest, React Testing Library, Playwright e axe-core estão configurados.
- [x] Existe comando único e não interativo para a suíte de CI.
- [x] Todos os cenários SSE obrigatórios existem e passam.
- [x] TipTap possui testes de seleção, âncora, aplicação, undo, redo e transação
      única.
- [x] O fluxo E2E principal comprova aplicação, autosave e persistência após
      reload.
- [x] E2E cobre conflito, erro, cancelamento e âncora expirada.
- [ ] Axe não encontra violações críticas ou sérias nos estados definidos.
- [ ] Auditoria de teclado, foco, ARIA e contraste está registrada.
- [ ] Smoke test com leitor de tela está registrado.
- [ ] Atalhos foram comprovados em Windows/Linux e macOS.
- [ ] A suíte foi executada com sucesso e a evidência está preservada.

A aprovação desta estratégia, isoladamente, não encerra o ponto.
