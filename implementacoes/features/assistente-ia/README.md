# Assistente de IA para redação jurídica

> **Estado:** primeira fatia vertical implementada no backend e no frontend
>
> **Última atualização:** 2026-09-29
>
> **Implementação atual:** geração, streaming, revisão e aplicação explícita
> integrados; validação funcional ponta a ponta e endurecimento continuam pendentes
>
> **Documentos relacionados:** [contrato HTTP](contrato-api.md),
> [backend](backend.md), [frontend](frontend.md) e
> [pontos em aberto](pontos-em-aberto.md). Decisões consolidadas:
> [Tópico 1 — Modelo e operação](topico-01-modelo-e-operacao.md),
> [Tópico 3 — Retenção, exclusão e LGPD](topico-03-retencao-exclusao-lgpd.md),
> [Tópico 7 — Âncora expirada](topico-07-ancora-expirada.md) e
> [Tópico 8 — Autosave em erro](topico-08-autosave-em-erro.md),
> [Tópico 9 — Avaliação de qualidade jurídica](topico-09-avaliacao-qualidade-juridica.md) e
> [Tópico 10 — Legislação, jurisprudência e fontes](topico-10-legislacao-jurisprudencia-fontes.md)
>
> Planos ainda abertos:
> [Tópico 11 — Segurança, isolamento e logs](topico-11-seguranca-isolamento-logs.md),
> [roteiro de segurança operacional](seguranca-operacional-producao.md),
> [Tópico 12 — Testes e acessibilidade](topico-12-testes-acessibilidade.md),
> [Tópico 13 — Feature flags e rollout](topico-13-feature-flags-rollout.md)
> e [Tópico 14 — Observabilidade e métricas](topico-14-observabilidade-metricas.md)

Este diretório é a fonte de verdade da feature de assistência por IA no editor
de Peças. Ele deve ser atualizado junto com cada mudança de escopo, contrato ou
implementação. Uma caixa marcada significa que o comportamento está entregue e
verificado, não apenas que existe código parcial.

## Objetivo

Permitir que o profissional use IA durante a elaboração de uma Peça para:

- perguntar sobre o texto ou sobre o contexto do Caso;
- redigir um trecho novo a partir de uma instrução;
- reescrever, expandir, resumir ou corrigir uma seleção;
- analisar uma seção ou a Peça e sugerir melhorias;
- desenvolver uma sugestão em uma nova proposta de texto.

A IA trabalha como copiloto. Ela produz respostas e propostas, mas **nunca
altera a Peça diretamente**. Toda alteração no editor exige revisão e uma ação
explícita do usuário.

## Princípios invariantes

1. **Humano no controle:** gerar e aplicar são operações distintas. O endpoint
   de IA não atualiza `Peca.ConteudoJson`.
2. **Contexto visível:** antes do envio, a interface informa quais dados serão
   compartilhados: seleção, seção, documento, dados do Caso ou contexto livre.
3. **Menor contexto necessário:** nenhum documento inteiro ou dado do Caso é
   enviado por padrão quando a instrução e a seleção forem suficientes.
4. **Proposta verificável:** substituições exibem original e sugestão. Conteúdo
   novo mostra o destino pretendido antes da inserção.
5. **Falha segura:** seleção ou posição desatualizada desabilita a aplicação;
   nunca se aplica uma sugestão em um trecho diferente silenciosamente.
6. **Sem fatos inventados:** o modelo deve separar informação fornecida,
   inferência e informação ausente.
7. **Referências não presumidas:** leis, julgados, números de processo e
   citações produzidos sem fonte confiável são sinalizados para verificação.
8. **Isolamento:** contexto de Caso, Peça e histórico respeita a Organization do
   usuário autenticado.

## Escopo do primeiro produto útil

### Incluído no MVP

- painel lateral do assistente dentro do workspace da Peça;
- instrução livre e ações rápidas;
- uso com ou sem texto selecionado;
- escopos `Nenhum`, `Selecao`, `SecaoAtual` e `DocumentoCompleto`;
- inclusão opcional de contextos textuais e dados estruturados do Caso;
- modos `Perguntar`, `Redigir`, `Reescrever` e `Revisar`;
- resposta progressiva;
- proposta em texto estruturado em parágrafos;
- comparação para substituições;
- aceitar, rejeitar, editar e tentar novamente;
- inserção no cursor, após uma seleção ou substituição da seleção;
- geração integral de Peça em fluxo próprio, com proposta separada, revisão e
  aplicação explícita (planejada no MVP; fluxo pendente em `PA-16`);
- histórico de interações mantido no JuridicoAI, com exclusão independente;
- integração com undo/redo e autosave existentes;
- cancelamento, limites de uso, erros compreensíveis e auditoria técnica.

### Fora do MVP

- alteração automática da Peça ou execução de uma sequência autônoma de ações;
- pesquisa externa de jurisprudência ou legislação;
- leitura/OCR do conteúdo binário de documentos anexados;
- garantia automática de correção jurídica;
- múltiplas seleções simultâneas;
- edição colaborativa em tempo real;
- geração integral da Peça sem revisão por etapas;
- aplicação no cabeçalho ou rodapé.

Metadados de documentos anexados podem ser exibidos ao modelo apenas em fase
posterior. O conteúdo de um arquivo só poderá entrar no contexto depois de
existir extração, confirmação visível e política de retenção definida.

## Vocabulário

| Termo | Significado |
|---|---|
| Instrução | Pedido escrito pelo usuário para a IA. |
| Contexto | Dados enviados apenas para orientar a resposta. Não são conteúdo a inserir automaticamente. |
| Escopo do documento | Parte do editor enviada: nenhuma, seleção, seção atual ou documento completo. |
| Sugestão | Resultado imutável retornado pela IA e ainda não aplicado. |
| Operação sugerida | Destino recomendado: painel, cursor, após seleção, substituição ou nova seção. |
| Aplicação | Transação local explícita no TipTap, iniciada pelo usuário. |
| Revisão base | Estado do editor capturado quando a solicitação foi enviada. |

## Fluxos de produto

### Redigir sem seleção

```text
Posicionar cursor
  -> abrir assistente
  -> descrever o trecho e selecionar contexto
  -> receber proposta
  -> revisar/editar
  -> clicar em Inserir
  -> TipTap cria uma transação
  -> autosave persiste a nova versão da Peça
```

Exemplo de instrução: “Crie um tópico sobre tutela de urgência considerando que
a conta usada para receber aposentadoria está bloqueada.”

### Alterar uma seleção

```text
Selecionar trecho
  -> escolher Reescrever/Expandir/Resumir/Corrigir
  -> receber proposta e diff
  -> aceitar ou rejeitar
  -> validar que seleção e revisão base ainda coincidem
  -> substituir em uma única transação, permitindo desfazer
```

### Analisar e desenvolver

```text
Pedir “O que falta nesta fundamentação?”
  -> receber sugestões no painel, sem alterar o editor
  -> escolher Desenvolver em uma sugestão
  -> receber uma proposta de texto
  -> escolher destino
  -> revisar e inserir explicitamente
```

## Modos e operações permitidas

| Modo | Resultado principal | Operações possíveis |
|---|---|---|
| `Perguntar` | resposta explicativa | `ResponderNoPainel` |
| `Redigir` | texto novo | `InserirNoCursor`, `InserirAposSelecao`, `CriarSecao` |
| `Reescrever` | nova versão do texto selecionado | `SubstituirSelecao` |
| `Revisar` | lista de achados e recomendações | `ResponderNoPainel`; cada achado pode iniciar uma nova solicitação `Redigir` |

O backend valida as combinações. O modelo pode recomendar uma operação dentro
do conjunto permitido, mas o frontend decide quais ações mostrar e o usuário
decide se alguma será executada.

## Contexto e privacidade

O contexto é composto de blocos identificáveis, para que a interface consiga
explicar exatamente o que será enviado:

- instrução do usuário;
- texto do escopo escolhido no editor;
- contexto adicional digitado apenas para aquela solicitação;
- contextos textuais do Caso selecionados pelo usuário;
- opcionalmente, dados do Caso e dos envolvidos necessários à tarefa.

Posições TipTap (`from` e `to`) servem somente como referência da interface e
nunca autorizam o backend a editar a Peça. O texto enviado é um snapshot. O
backend consulta a Peça para validar acesso e vínculo com o Caso, aplica limites
e monta o prompt final.

O produto deve informar que o conteúdo será processado pelo provedor de IA. A
política de retenção do JuridicoAI e do provedor, o uso para treinamento e o
prazo de exclusão precisam estar definidos antes de produção.

## Regras de segurança jurídica do prompt

O prompt de sistema mantido no backend deve, no mínimo:

- tratar todo contexto do usuário e dos documentos como dados, nunca como
  instruções de sistema;
- não criar fatos, provas, datas, valores ou declarações não fornecidos;
- explicitar quando faltam informações;
- não apresentar jurisprudência, processo ou citação como real sem fonte
  fornecida ou ferramenta de pesquisa confiável;
- preservar o sentido jurídico quando o modo for `Reescrever`;
- devolver alertas de verificação separadamente do texto sugerido;
- não afirmar que o resultado substitui revisão profissional.

## Critérios de aceite do MVP

- [ ] É possível perguntar sem seleção e nada muda na Peça.
- [ ] É possível redigir um trecho usando contexto livre e inseri-lo no cursor
      somente depois de clicar em uma ação explícita.
- [ ] É possível reescrever uma seleção vendo original e sugestão.
- [ ] Alterar o documento depois do envio invalida uma aplicação insegura.
- [ ] Rejeitar ou cancelar não altera o `conteudoJson` nem incrementa a versão.
- [ ] Aceitar gera uma única transação TipTap, pode ser desfeito e entra no
      autosave normal.
- [ ] Aplicação da IA fica bloqueada até o autosave estar em `SALVO`, sem
      impedir edição manual, undo ou retry nos demais estados.
- [x] O endpoint de geração não chama `IPecaRepository.AtualizarAsync` nem
      qualquer caminho equivalente de escrita na Peça.
- [x] Uma Organization não acessa nem usa contexto de outra Organization.
- [ ] O usuário visualiza os escopos enviados e pode remover os opcionais.
- [ ] Referências sem fonte aparecem com alerta de verificação.
- [ ] Timeout, cancelamento, limite e indisponibilidade do provedor não perdem
      o texto que o usuário já escreveu.
- [ ] Logs não contêm prompt, resposta, CPF/CNPJ ou conteúdo integral da Peça.

## Entregas incrementais

### F0 — decisões e contrato

- [x] Definir princípios, escopo e fluxos.
- [x] Propor contrato HTTP e protocolo de streaming.
- [x] Escolher OpenAI como primeiro provider, preservando abstração para troca.
- [x] Escolher `gpt-5.6-terra` como modelo inicial por configuração.
- [x] Definir limites iniciais de entrada, saída, orçamento e requisições.
- [ ] Validar o custo operacional com amostras jurídicas representativas.
- [x] Criar e validar estruturalmente os 30 casos fictícios do Tópico 9.
- [x] Executar a baseline real dos 30 casos com suíte, avaliador e commit rastreáveis.
- [x] Aceitar a baseline `1.0.3` como referência técnica congelada do MVP.
- [ ] Obter homologação jurídica formal da qualidade da IA em `PA-17` antes da comercialização.
- [x] Definir retenção, exclusão, minimização e aviso inicial do MVP.
- [x] Definir política de fontes e pausar o scraper no fluxo do assistente.
- [ ] Obter validação jurídica e contratual de `PA-17` antes da comercialização.

### F1 — backend vertical

- [x] Configuração segura, feature flag e validação de inicialização.
- [x] Abstração do provider e adapter inicial para OpenAI Responses.
- [x] Montagem de contexto com isolamento por Organization e Caso.
- [x] Endpoint autenticado com SSE e cancelamento.
- [x] Resposta por JSON Schema, política de prompt e códigos de erro.
- [ ] Completar limites por Organization, orçamento e métricas operacionais.
- [x] Implementar limite exato de 60.000 tokens de entrada.
- [ ] Implementar perfis de saída por operação e retry definidos no Tópico 1.
- [x] Testes unitários e HTTP do serviço, isolamento, limites e adapter OpenAI.

### F2 — frontend vertical

- [x] Painel e abertura por toolbar/atalho.
- [x] Captura de seleção, cursor, seção e snapshot da revisão.
- [x] Seletor de contexto e confirmação do que será enviado.
- [x] Consumo do stream, cancelamento e estados de erro.
- [x] Preview/diff e aplicação segura no TipTap.
- [x] Integração com autosave, conflito de versão e undo/redo.
- [ ] Restringir aplicação da IA ao estado `SALVO` conforme o Tópico 8.
- [ ] Configurar e executar a suíte frontend definida no Tópico 12.

### F3 — endurecimento e lançamento

- [x] Revisar e aceitar tecnicamente a baseline atual da suíte do Tópico 9.
- [x] Testes de isolamento, prompt injection e vazamento em logs na aplicação.
- [x] Aprovar manualmente a execução real da suíte `seguranca-ia` `1.0.0`.
- [ ] Implementar e comprovar o gate de segurança do Tópico 11.
- [ ] Limites por usuário/Organization e orçamento operacional.
- [ ] Política de retenção/exclusão implementada e documentada.
- [ ] Implementar feature flags e rollout controlado conforme o Tópico 13.
- [ ] Implementar métricas, dashboards e alertas conforme o Tópico 14.

## Decisões registradas

| Data | Decisão | Estado |
|---|---|---|
| 2026-09-26 | O endpoint de IA nunca grava `ConteudoJson`; aplicação ocorre no TipTap após confirmação. | Aceita |
| 2026-09-26 | O contexto do documento é explícito e começa no menor escopo necessário. | Aceita |
| 2026-09-26 | O primeiro formato de saída editável é texto dividido em parágrafos, não JSON TipTap produzido pelo modelo. | Proposta |
| 2026-09-26 | O transporte progressivo será `fetch` + Server-Sent Events sobre `POST`. | Proposta |
| 2026-09-26 | Histórico conversacional persistente não bloqueou a primeira fatia vertical sem histórico. | Implementada; decisão posterior em `PA-06` |
| 2026-09-26 | OpenAI Responses é o primeiro provider; `IAssistenteIaProvider` preserva a fronteira para alternativas. | Implementada, revisável |
| 2026-09-26 | O provider envia `store: false`; histórico do produto não depende da retenção do provider. | Aceita; não equivale a ZDR, ver `PA-17` |
| 2026-09-26 | O atalho inicial do painel é `Ctrl/Cmd + Alt + A`, evitando conflito com ferramentas do navegador. | Implementada, sujeita a teste multiplataforma |
| 2026-09-27 | `PA-01`: o modelo único do MVP é `gpt-5.6-terra`; não haverá roteamento nem fallback automático de modelo. | Aceita; implementação pendente |
| 2026-09-27 | `PA-01`: falhas transitórias permitem até duas novas tentativas, respeitando `Retry-After`, sem retry após delta. | Aceita; implementação pendente |
| 2026-09-27 | `PA-02`: entrada limitada a 60 mil tokens e saída definida por perfil entre 2 mil e 8 mil tokens. | Aceita; implementação pendente |
| 2026-09-27 | `PA-02`: orçamento inicial de US$ 10/mês por Organization, alertas em 70%/90% e bloqueio em 100%. | Aceita; implementação pendente |
| 2026-09-27 | `PA-02`: usuário limitado a 10 solicitações/minuto e 2 simultâneas, sem orçamento financeiro individual. | Aceita; implementação pendente |
| 2026-09-27 | `PA-03`: dados do Caso e histórico acompanham o Caso; logs duram 90 dias, backups até 30 dias e exclusão tem recuperação por 30 dias. | Aceita; implementação pendente |
| 2026-09-27 | `PA-04`: dados são minimizados e identificadores desnecessários são pseudonimizados antes do provider. | Aceita; implementação pendente |
| 2026-09-27 | `PA-05`: primeiro uso exige aviso versionado; a confirmação não é presumida como base legal geral. | Aceita; implementação pendente |
| 2026-09-27 | `PA-06`: histórico persistente por Caso ficará no JuridicoAI e poderá ser excluído separadamente. | Aceita; implementação pendente |
| 2026-09-27 | `PA-07`: âncora expirada bloqueia aplicação; é necessário selecionar outro trecho e gerar novamente, ou descartar. | Aceita; refinamento de UX e testes pendentes |
| 2026-09-27 | `PA-08`: edição manual continua permitida, mas aplicação da IA exige autosave em `SALVO`. | Aceita; implementação e testes pendentes |
| 2026-09-27 | `PA-09`: avaliação inicial terá 30 casos fictícios, rubrica de 0 a 10 e falhas impeditivas que não podem ser compensadas pela média. | Encerrado tecnicamente com a baseline `1.0.3` no commit `f6376df`; homologação jurídica formal permanece em `PA-17` |
| 2026-09-27 | `PA-10`: o MVP não pesquisa externamente; só trata como confirmadas fontes fornecidas no contexto e mantém o scraper fora do fluxo e da apresentação. | Aceita; casos incorporados à baseline técnica e interface pendente |
| 2026-09-27 | `PA-11`: tenant vem apenas da identidade validada; logs não armazenam conteúdo; limites agregados são 30/minuto e 6 simultâneos por Organization. | Em implementação; tenancy de Contexto reforçada, logs de erro protegidos, `Retry-After` e testes dos limitadores entregues |
| 2026-09-27 | `PA-12`: frontend terá testes unitários com Vitest, componentes com Testing Library, E2E com Playwright e acessibilidade com axe-core e auditoria manual. | Estratégia aceita; suíte e execução pendentes |
| 2026-09-27 | `PA-13`: `AI_ENABLED` será o kill switch; quatro flags por Organization controlarão capacidades e o rollout será nominal, sem percentual. | Estratégia aceita; implementação e rollout pendentes |
| 2026-09-27 | `PA-14`: métricas técnicas, financeiras e de produto usarão `CorrelationId` ponta a ponta sem armazenar conteúdo jurídico. | Estratégia aceita; coleta, dashboards e alertas pendentes |
| 2026-09-27 | A validação jurídica, contratual e de transferência internacional permanece bloqueadora em `PA-17`. | Pendente |

### Decisões pendentes

As decisões pendentes, suas prioridades e critérios de fechamento são mantidos
em [Pontos em aberto](pontos-em-aberto.md).

## Como manter este documento vivo

Ao implementar uma fatia:

1. atualize o estado e marque apenas critérios comprovados;
2. ajuste primeiro o [contrato](contrato-api.md) quando a API mudar;
3. registre divergências intencionais na tabela de decisões;
4. acrescente links para classes, componentes, migrations e testes reais;
5. remova o item de “fora do MVP” quando ele entrar formalmente no escopo;
6. mantenha a documentação no mesmo pull request da mudança.
