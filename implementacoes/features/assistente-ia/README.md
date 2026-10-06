# Assistente de IA para redação jurídica

> **Estado:** núcleo funcional validado; redesign conversacional aprovado e
> ainda não implementado
>
> **Última atualização:** 2026-10-06
>
> **Implementação atual:** geração, streaming, revisão e aplicação explícita
> integrados; testes unitários, de componente, E2E, segurança e qualidade
> jurídica executados. A validação manual confirmou os fluxos, mas identificou
> alta carga cognitiva e baixa legibilidade no painel atual. O próximo trabalho
> de produto é a experiência conversacional do PA-18.
>
> **Documentos relacionados:** [contrato HTTP](especificacoes/contrato-api.md),
> [backend](especificacoes/backend.md), [frontend](especificacoes/frontend.md) e
> [pontos em aberto](planejamento/pontos-em-aberto.md). Decisões consolidadas:
> [PA-01 — Modelo e operação](decisoes/pa-01-modelo-e-operacao.md),
> [PA-03 — Retenção, exclusão e LGPD](decisoes/pa-03-retencao-exclusao-lgpd.md),
> [PA-07 — Âncora expirada](decisoes/pa-07-ancora-expirada.md),
> [PA-08 — Autosave em erro](decisoes/pa-08-autosave-em-erro.md),
> [PA-09 — Avaliação de qualidade jurídica](decisoes/pa-09-avaliacao-qualidade-juridica.md)
> e [PA-10 — Legislação, jurisprudência e fontes](decisoes/pa-10-legislacao-jurisprudencia-fontes.md).
>
> Planos ainda abertos:
> [PA-11 — Segurança, isolamento e logs](decisoes/pa-11-seguranca-isolamento-logs.md),
> [roteiro de segurança operacional](evidencias/seguranca-operacional-producao.md),
> [PA-12 — Testes e acessibilidade](decisoes/pa-12-testes-acessibilidade.md),
> [PA-13 — Feature flags e rollout](decisoes/pa-13-feature-flags-rollout.md),
> [PA-14 — Observabilidade e métricas](decisoes/pa-14-observabilidade-metricas.md)
> e [PA-18 — Experiência conversacional](decisoes/pa-18-experiencia-conversacional.md).
> Implementação do redesign:
> [especificação de experiência](especificacoes/experiencia-conversacional.md),
> [plano incremental](planejamento/redesign-conversacional.md) e
> [evidência manual](evidencias/validacao-manual-experiencia-2026-10-06.md).

Este diretório é a bancada viva da feature de assistência por IA no editor de
Peças. Ele deve ser atualizado junto com cada mudança de escopo, contrato ou
implementação. O comportamento estável já disponível é resumido em
[`docs/backend/assistente-ia.md`](../../../docs/backend/assistente-ia.md). Uma
caixa marcada significa que o comportamento está entregue e verificado, não
apenas que existe código parcial.

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

## Direção atual da experiência

O painel baseado em formulário cumpriu a primeira validação funcional, mas não
será a interface definitiva. O produto adotará um chat integrado ao editor:

- uma caixa única para perguntas e pedidos de alteração;
- seleção e demais contextos representados por chips removíveis;
- respostas, revisões e propostas no corpo da conversa;
- aceitar, editar, rejeitar e gerar outra versão dentro da mensagem;
- painel redimensionável ou expandido para conteúdo jurídico extenso;
- modos e operações técnicas ocultos no fluxo normal.

Essa mudança simplifica a interação sem conceder autonomia de escrita. Âncora,
autosave, confirmação humana, isolamento e feature flags permanecem obrigatórios.

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

- [x] É possível perguntar sem seleção e nada muda na Peça.
- [x] É possível redigir um trecho usando contexto livre e inseri-lo no cursor
      somente depois de clicar em uma ação explícita.
- [x] É possível reescrever uma seleção vendo original e sugestão.
- [x] Alterar o documento depois do envio invalida uma aplicação insegura.
- [x] Rejeitar ou cancelar não altera o `conteudoJson` nem incrementa a versão.
- [x] Aceitar gera uma única transação TipTap, pode ser desfeito e entra no
      autosave normal.
- [x] Aplicação da IA fica bloqueada até o autosave estar em `SALVO`, sem
      impedir edição manual, undo ou retry nos demais estados.
- [x] O endpoint de geração não chama `IPecaRepository.AtualizarAsync` nem
      qualquer caminho equivalente de escrita na Peça.
- [x] Uma Organization não acessa nem usa contexto de outra Organization.
- [x] O usuário visualiza os escopos enviados e pode remover os opcionais.
- [ ] Referências sem fonte aparecem com alerta de verificação.
- [x] Timeout, cancelamento e indisponibilidade do provedor não perdem
      o texto que o usuário já escreveu.
- [x] Logs testados não contêm prompt, resposta, CPF/CNPJ ou conteúdo integral da Peça.

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
- [x] Restringir aplicação da IA ao estado `SALVO` conforme o PA-08.
- [x] Configurar e executar a suíte frontend definida no PA-12.

### F2.1 — experiência conversacional

- [x] Validar manualmente os fluxos e registrar problemas da interface atual.
- [x] Aprovar chat como interface principal e preservar controle humano.
- [x] Especificar contexto automático, mensagens e cartões de sugestão.
- [ ] Validar wireframe ou protótipo de baixa fidelidade.
- [ ] Implementar estrutura de conversa e composer único.
- [ ] Substituir formulário de contexto por chips e menu progressivo.
- [ ] Levar resposta, revisão e proposta para o corpo da conversa.
- [ ] Adicionar expansão/redimensionamento para respostas extensas.
- [ ] Preservar conversa local por Peça durante a sessão.
- [ ] Executar regressão automatizada e validação manual do PA-18.

### F3 — endurecimento e lançamento

- [x] Revisar e aceitar tecnicamente a baseline atual da suíte do Tópico 9.
- [x] Testes de isolamento, prompt injection e vazamento em logs na aplicação.
- [x] Aprovar manualmente a execução real da suíte `seguranca-ia` `1.0.0`.
- [ ] Implementar e comprovar o gate de segurança do Tópico 11.
- [ ] Limites por usuário/Organization e orçamento operacional.
- [ ] Política de retenção/exclusão implementada e documentada.
- [x] Implementar e testar os controles técnicos de feature flags do Tópico 13.
- [x] Preparar o kill switch operacional e a auditoria do Tópico 13.
- [ ] Executar e registrar o rollout nominal do Tópico 13.
- [ ] Implementar métricas, dashboards e alertas conforme o Tópico 14.

## Decisões registradas

| Data | Decisão | Estado |
|---|---|---|
| 2026-09-26 | O endpoint de IA nunca grava `ConteudoJson`; aplicação ocorre no TipTap após confirmação. | Aceita |
| 2026-09-26 | O contexto do documento é explícito e começa no menor escopo necessário. | Aceita |
| 2026-09-26 | O primeiro formato de saída editável é texto dividido em parágrafos, não JSON TipTap produzido pelo modelo. | Implementada |
| 2026-09-26 | O transporte progressivo será `fetch` + Server-Sent Events sobre `POST`. | Implementada |
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
| 2026-09-27 | `PA-05`: primeiro uso exige aviso versionado; a confirmação não é presumida como base legal geral. | Implementada em 2026-10-01; texto jurídico e URL definitiva da política ainda dependem do `PA-17` |
| 2026-09-27 | `PA-06`: histórico persistente por Caso ficará no JuridicoAI e poderá ser excluído separadamente. | Aceita; implementação pendente |
| 2026-09-27 | `PA-07`: âncora expirada bloqueia aplicação; é necessário selecionar outro trecho e gerar novamente, ou descartar. | Implementada e coberta por testes |
| 2026-09-27 | `PA-08`: edição manual continua permitida, mas aplicação da IA exige autosave em `SALVO`. | Implementada e coberta por testes |
| 2026-09-27 | `PA-09`: avaliação inicial terá 30 casos fictícios, rubrica de 0 a 10 e falhas impeditivas que não podem ser compensadas pela média. | Encerrado tecnicamente com a baseline `1.0.3` no commit `f6376df`; homologação jurídica formal permanece em `PA-17` |
| 2026-09-27 | `PA-10`: o MVP não pesquisa externamente; só trata como confirmadas fontes fornecidas no contexto e mantém o scraper fora do fluxo e da apresentação. | Aceita; casos incorporados à baseline técnica e interface pendente |
| 2026-09-27 | `PA-11`: tenant vem apenas da identidade validada; logs não armazenam conteúdo; limites agregados são 30/minuto e 6 simultâneos por Organization. | Controles locais e testes entregues; validação da infraestrutura de produção e coordenação distribuída pendentes |
| 2026-09-27 | `PA-12`: frontend terá testes unitários com Vitest, componentes com Testing Library, E2E com Playwright e acessibilidade com axe-core e auditoria manual. | Suíte automatizada executada; checklist manual permanece pendente |
| 2026-10-01 | A confirmação do aviso de IA é persistida por usuário, Organization e versão; nova versão exige reconfirmação e a geração é bloqueada no backend até lá. | Implementada e coberta por testes backend, componente, E2E e axe-core |
| 2026-09-27 | `PA-13`: `AI_ENABLED` será o kill switch; quatro flags por Organization controlarão capacidades e o rollout será nominal, sem percentual. | Kill switch persistido e auditoria implementados; rollout nominal pendente |
| 2026-09-27 | `PA-14`: métricas técnicas, financeiras e de produto usarão `CorrelationId` ponta a ponta sem armazenar conteúdo jurídico. | Estratégia aceita; coleta, dashboards e alertas pendentes |
| 2026-09-27 | A validação jurídica, contratual e de transferência internacional permanece bloqueadora em `PA-17`. | Pendente |
| 2026-10-06 | `PA-18`: o assistente adotará chat como interface principal, contexto por chips e propostas dentro da conversa, sem aplicação autônoma. | Primeira fatia conversacional implementada; chips automáticos e validação visual pendentes |

### Decisões pendentes

As decisões pendentes, suas prioridades e critérios de fechamento são mantidos
em [Pontos em aberto](planejamento/pontos-em-aberto.md).

## Como manter este documento vivo

Ao implementar uma fatia:

1. atualize o estado e marque apenas critérios comprovados;
2. ajuste primeiro o [contrato](especificacoes/contrato-api.md) quando a API mudar;
3. registre divergências intencionais na tabela de decisões;
4. acrescente links para classes, componentes, migrations e testes reais;
5. remova o item de “fora do MVP” quando ele entrar formalmente no escopo;
6. mantenha a documentação no mesmo pull request da mudança.
