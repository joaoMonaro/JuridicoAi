# Pontos em aberto — Assistente de IA

> **Estado:** decisões pendentes
>
> **Última atualização:** 2026-09-29
>
> **Documento principal:** [Assistente de IA](README.md)

Este documento reúne decisões que ainda afetam produto, segurança, custo ou
arquitetura. Um ponto decisório pode ser encerrado quando existe uma decisão
registrada e um responsável. Isso não marca automaticamente a implementação
como entregue: comportamentos aplicáveis continuam pendentes até haver teste ou
homologação registrados no documento do tópico.

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

## Ordem recomendada de decisão

1. Submeter `PA-17` à revisão jurídica e contratual, incluindo a homologação
   jurídica formal da qualidade das respostas antes da comercialização.
2. Implementar e validar as decisões de `PA-01` a `PA-06` já encerradas.
3. Fechar segurança e testes em `PA-11` e `PA-12` e implementar a decisão de
   autosave registrada no `PA-08`.
4. Preparar operação e lançamento com `PA-13` e `PA-14`.
5. Definir o fluxo de geração integral em `PA-16`.
6. Tratar a melhoria não bloqueante de bundle em `PA-15`.

## Pontos resolvidos

| ID | Decisão | Responsável | Registro |
|---|---|---|---|
| `PA-01` | GPT-5.6 Terra como modelo único do MVP; duas novas tentativas transitórias; sem fallback de modelo. | Produto / proprietário do projeto | [Tópico 1](topico-01-modelo-e-operacao.md) |
| `PA-02` | Limites por operação, 60 mil tokens de entrada, orçamento de US$ 10 e controles operacionais por usuário e Organization. | Produto / proprietário do projeto | [Tópico 1](topico-01-modelo-e-operacao.md) |
| `PA-03` | Retenção por categoria, histórico próprio, logs por 90 dias, backups por 30 dias e exclusão recuperável por 30 dias. | Produto / proprietário do projeto | [Tópico 3](topico-03-retencao-exclusao-lgpd.md) |
| `PA-04` | Minimização e pseudonimização por necessidade, com proteção reforçada para dados sensíveis relevantes. | Produto / proprietário do projeto | [Tópico 3](topico-03-retencao-exclusao-lgpd.md) |
| `PA-05` | Aviso versionado antes do primeiro uso, sem tratá-lo automaticamente como base legal geral. | Produto / proprietário do projeto | [Tópico 3](topico-03-retencao-exclusao-lgpd.md) |
| `PA-06` | Histórico persistente por Caso no JuridicoAI, com exclusão independente e sem usar o provider como fonte primária. | Produto / proprietário do projeto | [Tópico 3](topico-03-retencao-exclusao-lgpd.md) |
| `PA-07` | Âncora expirada bloqueia aplicação; o usuário seleciona outro trecho e gera novamente ou descarta. Inserção no cursor atual não é fallback do MVP. | Produto / proprietário do projeto | [Tópico 7](topico-07-ancora-expirada.md) |
| `PA-08` | Edição manual continua disponível, mas sugestões da IA só podem ser aplicadas quando o autosave está em `SALVO`. | Produto / proprietário do projeto | [Tópico 8](topico-08-autosave-em-erro.md) |
| `PA-09` | A suíte fictícia de 30 casos, o executor e o gate automático foram implementados; a baseline `1.0.3` com prompt `assistente-juridico-v3` foi aceita como referência técnica congelada do MVP. | Produto / proprietário do projeto | [Tópico 9](topico-09-avaliacao-qualidade-juridica.md) |
| `PA-10` | O MVP não pesquisa externamente; somente fontes fornecidas no contexto podem ser tratadas como confirmadas. O scraper fica fora do fluxo e da apresentação. | Produto / proprietário do projeto | [Tópico 10](topico-10-legislacao-jurisprudencia-fontes.md) |

## Privacidade e dados

### PA-17 — Validação jurídica e contratual para comercialização

As decisões técnicas de `PA-03`, `PA-04`, `PA-05` e `PA-06` estão registradas no
[Tópico 3](topico-03-retencao-exclusao-lgpd.md), mas não encerram:

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
[Tópico 1](topico-01-modelo-e-operacao.md). Ainda é necessário definir:

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

## Segurança e validação

### PA-11 — Segurança, isolamento e logs

A política está definida no
[Tópico 11 — Segurança, isolamento e logs](topico-11-seguranca-isolamento-logs.md).
Foram aprovados:

- tenant derivado exclusivamente da identidade validada pelo backend;
- filtro de `OrganizationId` para Caso, Documento, anexo, conversa e sugestão;
- 404/403 sem conteúdo para tentativa de acesso cruzado;
- conteúdo externo sempre tratado como dado, nunca como instrução;
- proibição de corpos, conteúdo jurídico, dados pessoais e segredos em logs;
- limites configuráveis de payload, upload, tokens e timeout;
- 10 requisições/minuto e 2 simultâneas por usuário;
- 30 requisições/minuto e 6 simultâneas por Organization.

Ainda precisam de evidência específica:

- ampliar o caso real `FONTES-005` e o teste de separação de prompt para as
  demais tentativas de prompt injection previstas;
- ampliar a cobertura HTTP de acesso cruzado para envolvidos, endereços,
  listagens e entidades futuras; a suíte HTTP já cobre Caso, Peça, Contexto,
  Documento, arquivo e assistente, sem leitura do arquivo físico alheio;
- inspeção de logs e telemetria de infraestrutura para conteúdo, CPF/CNPJ, JWT
  e API keys; logs automáticos do cliente OpenAI foram desabilitados e os
  caminhos de erro da aplicação já possuem testes com marcadores sensíveis;
- respostas inválidas do modelo em nível HTTP; payload e tokens excessivos,
  timeout, desconexão, quatro limitadores, `Retry-After` e liberação da lease
  possuem cobertura automatizada;
- coordenação distribuída dos limitadores antes de múltiplas instâncias;
- validação das configurações de produção.

Critério de fechamento: controles implementados, suíte automatizada executada,
configuração de produção revisada e ausência comprovada de dados sensíveis nos
logs de sucesso e falha. A política aprovada, sozinha, não fecha o ponto.

### PA-12 — Testes e acessibilidade do frontend

A estratégia está definida no
[Tópico 12 — Testes e acessibilidade](topico-12-testes-acessibilidade.md):

- unitários com Vitest;
- componentes com React Testing Library;
- E2E com Playwright;
- acessibilidade automatizada com axe-core;
- auditoria manual de teclado, foco, contraste e leitor de tela.

Critério de fechamento: ferramentas configuradas, cenários críticos presentes,
suíte executada com sucesso e auditoria manual registrada. O plano, sozinho,
não fecha o ponto.

## Lançamento e operação

### PA-13 — Feature flag e rollout

A estratégia está definida no
[Tópico 13 — Feature flags e rollout](topico-13-feature-flags-rollout.md).
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

### PA-14 — Observabilidade e métricas

A estratégia está definida no
[Tópico 14 — Observabilidade e métricas](topico-14-observabilidade-metricas.md).
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

Ao fechar um item, registrar no [documento principal](README.md):

```text
Data | ID | decisão | responsável | evidência/link
```

Depois, remover o item deste documento ou marcá-lo como resolvido com link para
a decisão consolidada. Não encerrar um ponto apenas porque existe código parcial.
