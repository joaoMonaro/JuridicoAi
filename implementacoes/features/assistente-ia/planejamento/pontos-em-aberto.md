# Pontos em aberto — Assistente de IA

> **Estado:** decisões de produto restantes e entregas/validações pendentes
>
> **Última atualização:** 2026-10-18
>
> **Documento principal:** [Assistente de IA](../README.md)

Este documento reúne decisões que ainda afetam produto, segurança, custo ou
arquitetura. Um ponto decisório pode ser encerrado quando existe uma decisão
registrada e um responsável. Isso não marca automaticamente a implementação
como entregue: comportamentos aplicáveis continuam pendentes até haver teste ou
homologação registrados no documento do tópico.

## Retrato conferido em 2026-10-18

A [conferência documental](../evidencias/conferencia-documental-2026-10-18.md)
comparou código e testes existentes dos dois repositórios, sem executar
novamente as suítes ou homologar a interface.

| Frente | Já implementado | O que continua pendente |
|---|---|---|
| PA-01/02 | modelo configurável, contagem exata de entrada, rate limit e concorrência locais, ledger de consumo | perfis de saída, retry automático, orçamento com alertas/bloqueio e coordenação entre instâncias |
| PA-03 a PA-06 | contexto explícito, `store=false` e aviso versionado com confirmação | histórico persistente, exclusão/recuperação/purga, retenção operacional e pseudonimização adicional |
| PA-07/08 | bloqueio por âncora e autosave, mensagens de erro e testes | recuperação guiada da âncora e cenário dedicado de falha de autosave após aplicação com undo |
| PA-18 | chat, composer, chips, propostas, comparação, larguras predefinidas e expansão | feedback completo de limites, preservação entre trocas de Peça, regressão E5 e validação visual/manual |
| PA-11 a PA-14 | controles locais, flags, kill switch auditado, suítes existentes, correlação backend e ledger | infraestrutura real, auditoria manual de acessibilidade, rollout nominal, métricas, dashboards e alertas |

O pedido livre ainda começa em `Perguntar`; intenção automática é E4. Fechar o
painel preserva as mensagens enquanto o editor estiver montado, mas trocar de
Peça ou recarregar descarta a conversa local. Exibir turnos anteriores não
significa enviá-los como histórico ao modelo.

## Resumo e prioridade

| ID | Tema | Prioridade | Bloqueia produção |
|---|---|---:|:---:|
| `PA-11` | Segurança, isolamento e logs | Crítica | Sim |
| `PA-12` | Testes e acessibilidade do frontend | Alta | Sim |
| `PA-13` | Feature flag e rollout | Alta | Sim |
| `PA-14` | Observabilidade e métricas | Alta | Sim |
| `PA-15` | Tamanho do bundle do frontend | Baixa | Não |
| `PA-16` | Fluxo seguro de geração integral da Peça | Alta | Sim |
| `PA-17` | Validação jurídica e contratual para comercialização | Crítica | Sim |
| `PA-18` | Experiência conversacional do assistente | Alta | Sim para piloto externo |

## Ordem recomendada de decisão

1. Implementar e validar o redesign conversacional do `PA-18` antes de ampliar
   as ferramentas visíveis do assistente.
2. Implementar as partes operacionais ainda pendentes de `PA-01` a `PA-06`:
   perfis de saída/retry, orçamento, retenção, pseudonimização e histórico.
3. Concluir a preparação de produção de `PA-11` e a auditoria manual de
   acessibilidade de `PA-12`.
4. Instrumentar `PA-14` e executar o rollout nominal previsto em `PA-13`.
5. Submeter `PA-17` à revisão jurídica e contratual, incluindo homologação
   formal das respostas, antes da comercialização.
6. Definir o fluxo de geração integral em `PA-16`.
7. Tratar a melhoria não bloqueante de bundle em `PA-15`.

## Pontos resolvidos

| ID | Decisão | Responsável | Registro |
|---|---|---|---|
| `PA-01` | GPT-5.6 Terra como modelo único do MVP; duas novas tentativas transitórias; sem fallback de modelo. | Produto / proprietário do projeto | [Tópico 1](../decisoes/pa-01-modelo-e-operacao.md) |
| `PA-02` | Limites por operação, 60 mil tokens de entrada, orçamento de US$ 10 e controles operacionais por usuário e Organization. | Produto / proprietário do projeto | [Tópico 1](../decisoes/pa-01-modelo-e-operacao.md) |
| `PA-03` | Retenção por categoria, histórico próprio, logs por 90 dias, backups por 30 dias e exclusão recuperável por 30 dias. | Produto / proprietário do projeto | [Tópico 3](../decisoes/pa-03-retencao-exclusao-lgpd.md) |
| `PA-04` | Minimização e pseudonimização por necessidade, com proteção reforçada para dados sensíveis relevantes. | Produto / proprietário do projeto | [Tópico 3](../decisoes/pa-03-retencao-exclusao-lgpd.md) |
| `PA-05` | Aviso versionado antes do primeiro uso, sem tratá-lo automaticamente como base legal geral. | Produto / proprietário do projeto | [Tópico 3](../decisoes/pa-03-retencao-exclusao-lgpd.md) |
| `PA-06` | Histórico persistente por Caso no JuridicoAI, com exclusão independente e sem usar o provider como fonte primária. | Produto / proprietário do projeto | [Tópico 3](../decisoes/pa-03-retencao-exclusao-lgpd.md) |
| `PA-07` | Âncora expirada bloqueia aplicação; o usuário seleciona outro trecho e gera novamente ou descarta. Inserção no cursor atual não é fallback do MVP. | Produto / proprietário do projeto | [Tópico 7](../decisoes/pa-07-ancora-expirada.md) |
| `PA-08` | Edição manual continua disponível, mas sugestões da IA só podem ser aplicadas quando o autosave está em `SALVO`. | Produto / proprietário do projeto | [Tópico 8](../decisoes/pa-08-autosave-em-erro.md) |
| `PA-09` | A suíte fictícia de 30 casos, o executor e o gate automático foram implementados; a baseline `1.0.3` com prompt `assistente-juridico-v3` foi aceita como referência técnica congelada do MVP. | Produto / proprietário do projeto | [Tópico 9](../decisoes/pa-09-avaliacao-qualidade-juridica.md) |
| `PA-10` | O MVP não pesquisa externamente; somente fontes fornecidas no contexto podem ser tratadas como confirmadas. O scraper fica fora do fluxo e da apresentação. | Produto / proprietário do projeto | [Tópico 10](../decisoes/pa-10-legislacao-jurisprudencia-fontes.md) |

## Privacidade e dados

### PA-17 — Validação jurídica e contratual para comercialização

As decisões técnicas de `PA-03`, `PA-04`, `PA-05` e `PA-06` estão registradas no
[Tópico 3](../decisoes/pa-03-retencao-exclusao-lgpd.md), mas não encerram:

- base legal de cada operação, inclusive dados sensíveis;
- papéis formais de controlador, operador e suboperador;
- contrato, política de privacidade, termos e acordo de tratamento;
- mecanismo de transferência internacional;
- governança de fornecedores e suboperadores;
- direitos dos titulares, incidentes e hipóteses excepcionais de conservação.
- homologação jurídica formal da rubrica, dos limites e da qualidade das
  respostas da IA para uso comercial.

Critério de fechamento: revisão jurídica formal, documentos aprovados,
configuração do provider verificada e autorização expressa para comercialização.

## Produto e edição

### PA-16 — Fluxo seguro de geração integral da Peça

O modelo e o limite de 8.000 tokens foram definidos no
[Tópico 1](../decisoes/pa-01-modelo-e-operacao.md). Ainda é necessário definir:

- se a geração parte de documento vazio, template ou Peça existente;
- como o usuário informa estrutura, fatos e seções esperadas;
- se a revisão e aplicação serão integrais ou seção por seção;
- como preview, diff, undo e autosave funcionarão para o documento inteiro;
- como evitar substituição acidental de conteúdo já existente;
- quais dados e fontes são obrigatórios antes de gerar.

Direção atual: gerar uma proposta separada, nunca alterar a Peça durante a
geração e exigir revisão e aplicação explícita. Não reutilizar `Redigir` sem um
contrato que diferencie trecho, seção e documento completo.

Critério de fechamento: fluxo de produto aprovado, contrato tipado, protótipo
homologado e testes que comprovem que cancelar, rejeitar ou falhar não altera a
Peça.

### PA-18 — Experiência conversacional do assistente

A decisão de produto está registrada em
[PA-18 — Experiência conversacional](../decisoes/pa-18-experiencia-conversacional.md).
A validação manual comprovou o núcleo funcional, mas o painel atual foi
considerado complexo, pesado e inadequado para leitura das respostas.

Direção aprovada:

- chat como interface principal;
- uma caixa única para pedido livre;
- seleção e demais contextos como chips removíveis;
- ações rápidas opcionais, sem seletor obrigatório de modo;
- respostas, achados e propostas dentro da conversa;
- aceitar, editar e rejeitar no cartão da proposta;
- painel redimensionável ou expandido;
- nenhuma alteração autônoma na Peça.

A [especificação](../especificacoes/experiencia-conversacional.md) e o
[plano incremental](redesign-conversacional.md) orientam a implementação.

Estado conferido em 2026-10-18: E1 a E3 já possuem implementação substancial,
incluindo testes de componente para chat, chips, propostas e expansão. A
separação estrutural do painel, o feedback completo de limites e a preservação
entre trocas de Peça permanecem pendentes. E5 ainda exige regressão E2E,
teclado, axe-core e validação manual da nova interface; os resultados anteriores
do painel não encerram esse gate. E4 e E6 são entregas posteriores à primeira
validação visual.

Critério de fechamento: primeira entrega conversacional implementada, testes de
regressão aprovados e validação manual confirmando redução de complexidade e
melhora de legibilidade.

## Segurança e validação

### PA-11 — Segurança, isolamento e logs

A política está definida no
[Tópico 11 — Segurança, isolamento e logs](../decisoes/pa-11-seguranca-isolamento-logs.md).
Foram aprovados:

- tenant derivado exclusivamente da identidade validada pelo backend;
- filtro de `OrganizationId` para Caso, Documento, anexo, conversa e sugestão;
- 404/403 sem conteúdo para tentativa de acesso cruzado;
- conteúdo externo sempre tratado como dado, nunca como instrução;
- proibição de corpos, conteúdo jurídico, dados pessoais e segredos em logs;
- limites configuráveis de payload, upload, tokens e timeout;
- 10 requisições/minuto e 2 simultâneas por usuário;
- 30 requisições/minuto e 6 simultâneas por Organization.

Evidências já existentes:

- a suíte real `seguranca-ia` `1.0.0` foi aprovada tecnicamente em 2026-09-30,
  sem falhas automáticas nos seis casos; novas versões de modelo, prompt ou
  suíte exigem nova execução e aprovação;
- payload, tokens, contexto e upload excessivos, resposta inválida do modelo,
  timeout, desconexão, quatro limitadores, `Retry-After` e liberação da lease
  possuem cobertura automatizada;
- os recursos atuais, inclusive envolvidos, endereços e listagens, possuem
  cenários de acesso cruzado;
- logs automáticos do cliente OpenAI foram desabilitados e os caminhos de erro
  da aplicação possuem testes com marcadores sensíveis.

Ainda precisam de evidência ou implementação:

- adicionar cobertura HTTP quando forem criadas conversa, mensagem e sugestão;
- inspecionar logs e telemetria da infraestrutura real para conteúdo,
  CPF/CNPJ, JWT e API keys;
- coordenação distribuída dos limitadores antes de múltiplas instâncias;
- validação das configurações de produção.

Critério de fechamento: controles implementados, suíte automatizada executada,
configuração de produção revisada e ausência comprovada de dados sensíveis nos
logs de sucesso e falha. A política aprovada, sozinha, não fecha o ponto.

### PA-12 — Testes e acessibilidade do frontend

A estratégia está definida no
[Tópico 12 — Testes e acessibilidade](../decisoes/pa-12-testes-acessibilidade.md):

- unitários com Vitest;
- componentes com React Testing Library;
- E2E com Playwright;
- acessibilidade automatizada com axe-core;
- auditoria manual de teclado, foco, contraste e leitor de tela.

Critério de fechamento: ferramentas configuradas, cenários críticos presentes,
suíte executada com sucesso e auditoria manual registrada. O plano, sozinho,
não fecha o ponto.

Estado em 2026-10-03: Vitest, React Testing Library, Playwright e axe-core
estão configurados; parser SSE, TipTap, painel, autosave, resiliência,
integridade, teclado simulado e acessibilidade automatizada possuem testes
aprovados. Permanecem a auditoria com teclado físico em plataformas reais e o
smoke test com leitor de tela, registrados no
[checklist manual](../evidencias/checklist-manual-acessibilidade.md).

Após o redesign de 2026-10-06 há testes de componente adaptados e uma execução
parcial registrada no plano. A conferência de 2026-10-18 não atribui os
resultados históricos de E2E/axe-core à regressão completa da interface nova;
essa comprovação permanece em E5 do PA-18.

## Lançamento e operação

### PA-13 — Feature flag e rollout

A estratégia está definida no
[Tópico 13 — Feature flags e rollout](../decisoes/pa-13-feature-flags-rollout.md).
Foram aprovados:

- `AI_ENABLED` como kill switch global;
- `AI_ASSISTANT`, `AI_EDIT`, `AI_REVIEW` e `AI_DOCUMENT_GENERATION` por
  Organization;
- validação obrigatória no backend e ocultação correspondente no frontend;
- ausência de flag tratada como desabilitada;
- rollout por tenant nomeado, sem percentual: desenvolvimento, tenant interno,
  piloto, grupo de 2 a 5 clientes e disponibilidade geral.

Critério de fechamento: persistência e validação implementadas, isolamento
testado, kill switch e rollback comprovados e primeiras etapas do rollout
executadas com evidência. A estratégia, sozinha, não fecha o ponto.

Estado em 2026-10-01: persistência, validação backend, ocultação seletiva no
frontend, falha fechada, isolamento, revogação e rollback possuem cobertura
automatizada. O kill switch persistido propaga para a próxima operação sem
restart, e toda alteração exige operador e justificativa e gera auditoria
append-only. Permanecem pendentes uma interface administrativa autenticada e a
execução documentada das etapas com tenant interno e cliente piloto.

### PA-14 — Observabilidade e métricas

A estratégia está definida no
[Tópico 14 — Observabilidade e métricas](../decisoes/pa-14-observabilidade-metricas.md).
Ela responde a funcionamento, utilidade e custo com:

- métricas técnicas de volume, sucesso, falha, cancelamento, TTFT e duração;
- ledger financeiro de tokens e custo por Organization/operação;
- eventos explícitos de sugestão gerada, aplicada, rejeitada, regenerada,
  expirada e solicitação cancelada;
- `CorrelationId` desde o frontend até a chamada instrumentada ao provider;
- dashboards e alertas sem prompt, resposta ou conteúdo da Peça.

Critério de fechamento: coleta implementada e testada, dashboards disponíveis,
alertas com responsáveis/runbooks e política de retenção e acesso validada. A
estratégia, sozinha, não fecha o ponto.

### PA-15 — Tamanho do bundle do frontend

O build atual gera um chunk JavaScript principal superior a 500 kB. Avaliar
lazy loading do workspace ou do painel de IA e separação de dependências antes
que novas funções ampliem esse custo.

Este item não bloqueia o piloto, mas deve possuir medição antes/depois quando
for implementado.

## Registro de resolução

Ao fechar um item, registrar no [documento principal](../README.md):

```text
Data | ID | decisão | responsável | evidência/link
```

Depois, remover o item deste documento ou marcá-lo como resolvido com link para
a decisão consolidada. Não encerrar um ponto apenas porque existe código parcial.
