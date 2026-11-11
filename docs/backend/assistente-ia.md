# Assistente de IA

Esta página resume o comportamento implementado do assistente. Especificações,
decisões e pendências anteriores permanecem no
[dossiê em espera](../../implementacoes/standBy/assistente-ia/README.md).
O [assistente conversacional](../../implementacoes/features/assistente-conversacional/README.md)
é a implementação ativa. A revisão persistida já possui integração atrás de
flag. Histórico usado como contexto e conversa livre com intenção automática
também estão integrados. Memória para conversas extensas, leitura documental,
worker e avaliação com modelo real permanecem pendentes.

## Comportamento atual

O assistente atua sobre uma Peça acessível ao usuário autenticado e pode
responder, redigir, reescrever ou revisar conteúdo. A API gera uma sugestão,
e a geração não grava `Peca.ConteudoJson`. O fluxo padrão, com
`AssistenteIa:ReviewV2Enabled=false`, funciona assim:

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

Com `ReviewV2Enabled=true` e `AI_EDIT` autorizado, a capacidade
`suportaRevisaoPersistida` permite registrar sugestões para revisão no documento.
Nesse fluxo, a aprovação chama um serviço determinístico que grava a Peça,
decisões e recibo em uma transação; o editor reconcilia a versão sem novo PATCH.
Propostas e pedidos associados ficam privados e persistidos. A flag fica
desligada por padrão. Veja [rotas, ativação e limites implementados](../../implementacoes/features/assistente-conversacional/evidencias/e1-revisao-persistida-2026-11-11.md).

Com `ConversationV2Enabled=true` (que exige `ReviewV2Enabled`), a capacidade
`suportaHistorico` ativa mensagens/respostas persistidas e recuperação no painel.
O gerador recebe o histórico elegível da própria conversa e a Peça atual do
servidor, dentro dos limites de contexto. Os modos ainda são os existentes.
Veja [contratos efetivos, evidências e limites](../../implementacoes/features/assistente-conversacional/evidencias/e1-e3-historico-2026-11-11.md).

Com `AutomaticConversationEnabled=true` (exige as duas flags anteriores),
`suportaConversaLivre` remove a escolha de modos do frontend. O endpoint
`POST /assistente/v2/conversas/{id}/interacoes` recebe texto natural e versão da
Peça. A mesma geração decide entre responder, propor edição e gerar minuta.
Propostas abrem diretamente a comparação; só a aprovação grava a Peça.
Pedidos seguintes podem ajustar uma proposta pendente, preservando o aplicado.
Conversa exige `AI_ASSISTANT`; propostas e aplicação exigem `AI_EDIT`; minuta
inicial/substituição integral também exigem `AI_DOCUMENT_GENERATION`.
Veja [ativação, contratos, testes e limites](../../implementacoes/features/assistente-conversacional/evidencias/e3-conversa-livre-2026-11-11.md).

## Interface legada e compatibilidade

O comportamento abaixo corresponde às flags conversacionais desligadas. Com a
conversa livre ativa, Peça e histórico são automáticos, modos ficam ocultos e
propostas persistidas vão diretamente à revisão no documento.

O frontend apresenta um chat com composer único, ações rápidas, chips removíveis
de contexto, respostas e cartões de proposta na conversa. A seleção é capturada
ao abrir o painel ou ao transferir o foco do editor para ele. Sem seleção, o
pedido começa sem conteúdo da Peça; os demais contextos são adicionados
explicitamente pelo menu `Adicionar contexto`.

Os cartões permitem editar, comparar com o original, copiar, rejeitar, tentar
novamente e aplicar no destino indicado. Há larguras predefinidas e modo
expandido. O pedido livre começa em `Perguntar`; ações rápidas selecionam
reescrita, revisão ou redação. Intenção automática exige a flag adicional descrita acima.

A conversa permanece em memória ao fechar e reabrir o painel enquanto o editor
da mesma Peça estiver montado. Trocar de Peça remonta o editor e perde esse
estado; recarregar a página também o descarta. No fluxo padrão não há histórico
persistente. O recorte de revisão persistida recupera propostas e salva seus
pedidos/respostas, mas ainda não recupera todas as mensagens na UI nem envia o
histórico ao provider como contexto conversacional. `suportaHistorico` permanece
`false` nesse recorte isolado; a flag adicional `ConversationV2Enabled` ativa a
integração de histórico descrita acima.

Em propostas com seleção, `Tentar novamente` reutiliza o snapshot anterior.
Após expiração da âncora, é necessário selecionar o trecho desejado e enviar um
novo pedido pelo composer. A recuperação guiada ainda está pendente.

A entrega visual permanece parcial no PA-18, cujo restante está em espera.
Código e testes de componente existem, mas regressão completa e validação manual da nova experiência ainda
não estão registradas como concluídas. Veja a
[conferência documental de 2026-10-18](../../implementacoes/alreadyDone/assistente-ia/evidencias/conferencia-documental-2026-10-18.md).

## Endpoints

| Método | Rota | Função |
|---|---|---|
| `GET` | `/assistente/capacidades` | informa disponibilidade, modos e limites visíveis ao usuário |
| `GET` | `/assistente/transparencia` | retorna o aviso atual e se o usuário já o confirmou |
| `POST` | `/assistente/transparencia/confirmacoes` | registra a confirmação da versão atual do aviso |
| `POST` | `/pecas/{pecaId}/assistente/solicitacoes` | gera uma resposta ou sugestão em stream SSE |

Todas as rotas exigem JWT. O contrato detalhado da solicitação e dos eventos
SSE está na [especificação HTTP](../../implementacoes/standBy/assistente-ia/especificacoes/contrato-api.md).

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
[segurança operacional](../../implementacoes/standBy/assistente-ia/evidencias/seguranca-operacional-producao.md).

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
em espera.

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
