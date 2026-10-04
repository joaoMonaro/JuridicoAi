# Assistente de IA

Esta página resume o comportamento implementado do assistente. Especificações,
decisões e pendências em evolução permanecem na
[documentação viva da feature](../../implementacoes/features/assistente-ia/README.md).

## Comportamento atual

O assistente atua sobre uma Peça acessível ao usuário autenticado e pode
responder, redigir, reescrever ou revisar conteúdo. A API gera uma sugestão,
mas nunca grava diretamente `Peca.ConteudoJson`.

```text
solicitação do usuário
    -> autenticação e Organization obtidas do token
    -> validação de flags, transparência, Peça, contexto e limites
    -> chamada à OpenAI Responses API com store=false
    -> resposta estruturada transmitida por SSE
    -> revisão e aplicação explícita no TipTap
    -> autosave normal da Peça com controle de versão
```

A aplicação no editor só é permitida quando a âncora original ainda é válida e
o autosave está em `SALVO`. Âncora expirada, erro de sincronização ou conflito
de versão bloqueiam a aplicação sem impedir que o usuário continue editando
manualmente.

## Endpoints

| Método | Rota | Função |
|---|---|---|
| `GET` | `/assistente/capacidades` | informa disponibilidade, modos e limites visíveis ao usuário |
| `GET` | `/assistente/transparencia` | retorna o aviso atual e se o usuário já o confirmou |
| `POST` | `/assistente/transparencia/confirmacoes` | registra a confirmação da versão atual do aviso |
| `POST` | `/pecas/{pecaId}/assistente/solicitacoes` | gera uma resposta ou sugestão em stream SSE |

Todas as rotas exigem JWT. O contrato detalhado da solicitação e dos eventos
SSE está na [especificação HTTP](../../implementacoes/features/assistente-ia/especificacoes/contrato-api.md).

## Controles de acesso e segurança

- `OrganizationId` e `UserId` vêm exclusivamente da identidade autenticada;
- Peça, Caso e contextos são consultados dentro da mesma Organization;
- a configuração `AssistenteIa:Enabled` é a trava superior da aplicação;
- o kill switch persistido `AI_ENABLED` pode interromper novas operações;
- `AI_ASSISTANT`, `AI_EDIT`, `AI_REVIEW` e `AI_DOCUMENT_GENERATION` são
  liberadas nominalmente por Organization;
- o primeiro uso exige confirmação do aviso de transparência versionado;
- instruções encontradas em documentos ou contexto são tratadas como dados,
  não como instruções de sistema;
- logs técnicos não registram prompt, resposta, conteúdo jurídico, JWT ou
  chave do provider.

Os comandos administrativos para flags e kill switch estão em
[User Secrets no desenvolvimento](../user-secrets.md). O roteiro de preparação
para produção está em
[segurança operacional](../../implementacoes/features/assistente-ia/evidencias/seguranca-operacional-producao.md).

## Limites implementados

Os valores são configuráveis em `AssistenteIa` e possuem defaults para:

- corpo HTTP de até 512 KiB;
- entrada de até 60.000 tokens;
- 10 solicitações por minuto e 2 simultâneas por usuário;
- 30 solicitações por minuto e 6 simultâneas por Organization;
- timeout do provider de 90 segundos.

O sistema registra metadados técnicos de execução e consumo sem usar o
conteúdo jurídico como telemetria. Orçamento mensal com bloqueio financeiro,
retenção automatizada e dashboards completos ainda são itens da implementação
em andamento.

## Configuração local

O assistente nasce desabilitado. Para habilitá-lo em desenvolvimento, configure
por user-secrets ao menos `AssistenteIa:ApiKey`, `AssistenteIa:Model` e
`AssistenteIa:Enabled=true`; depois habilite as flags necessárias para a
Organization. Consulte [User Secrets](../user-secrets.md) para os comandos e
cuidados com os valores sensíveis.

## Verificação

A implementação possui testes de backend para contrato HTTP, isolamento,
limites, segurança de logs, transparência e controles de feature. O frontend
possui testes Vitest, React Testing Library, Playwright e axe-core. As respostas
jurídicas são acompanhadas por uma
[suíte de avaliações](../../avaliacoes/qualidade-juridica/README.md); ela reduz
regressões, mas não substitui homologação jurídica profissional.
