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

## Interface disponível

O frontend apresenta um chat com composer único, ações rápidas, chips removíveis
de contexto, respostas e cartões de proposta na conversa. A seleção é capturada
ao abrir o painel ou ao transferir o foco do editor para ele. Sem seleção, o
pedido começa sem conteúdo da Peça; os demais contextos são adicionados
explicitamente pelo menu `Adicionar contexto`.

Os cartões permitem editar, comparar com o original, copiar, rejeitar, tentar
novamente e aplicar no destino indicado. Há larguras predefinidas e modo
expandido. O pedido livre começa em `Perguntar`; ações rápidas selecionam
reescrita, revisão ou redação. Intenção automática ainda não está implementada.

A conversa permanece em memória ao fechar e reabrir o painel enquanto o editor
da mesma Peça estiver montado. Trocar de Peça remonta o editor e perde esse
estado; recarregar a página também o descarta. Não há histórico persistente nem
envio das mensagens anteriores ao provider como contexto conversacional.

Em propostas com seleção, `Tentar novamente` reutiliza o snapshot anterior.
Após expiração da âncora, é necessário selecionar o trecho desejado e enviar um
novo pedido pelo composer. A recuperação guiada ainda está pendente.

A entrega visual está em implementação no PA-18: código e testes de componente
existem, mas regressão completa e validação manual da nova experiência ainda
não estão registradas como concluídas. Veja a
[conferência documental de 2026-10-18](../../implementacoes/features/assistente-ia/evidencias/conferencia-documental-2026-10-18.md).

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

Rate limit e concorrência são mantidos em memória por instância. A coordenação
distribuída permanece pendente antes de operar múltiplas instâncias. A saída usa
um único teto configurável, de 6.000 tokens por padrão; perfis por operação e
retry automático ainda não estão implementados.

O sistema registra metadados técnicos de execução e consumo sem usar o
conteúdo jurídico como telemetria. Orçamento mensal com bloqueio financeiro,
retenção automatizada e dashboards completos ainda são itens da implementação
em andamento.

O transporte externo é SSE, mas o backend aguarda o provider acumular e validar
a resposta estruturada antes de emitir o texto em um único `conteudo.delta`.
Exibição incremental do texto durante a geração continua pendente.

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
