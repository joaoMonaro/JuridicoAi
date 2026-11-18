# Assistente de IA

Conferida com o código em **2026-11-12**. O assistente conversacional é a
implementação ativa: conversa livre, contexto/histórico, memória, documentos
nativos, análise e geração duráveis, minutas por seções, revisão transacional,
histórico pessoal e exclusão autorizada do ambiente ativo.

Atualização em **2026-11-14**: correção local de reagendamento/admissão e
instrumentação por etapa verificadas. Comparação real concluída: consulta de
63,941 s para 22,697 s; espera restante dominada pela cota local, acompanhada em M03. Ver [diagnóstico local](../../implementacoes/features/assistente-conversacional/evidencias/m01-latencia-local-2026-11-14.md)
e [teste real de M01](../../implementacoes/features/assistente-conversacional/evidencias/m01-latencia-real-2026-11-14.md).


Atualização em **2026-11-17**: o painel usa uma única caixa de conversa e anexos,
sem atalhos de prompts, modos, escopos ou campos paralelos de contexto. A seleção
de trechos está pausada, inclusive em reenvios; a pendência está em **PROJ-006**
no [TODO geral](../../TODO.md). A versão salva da Peça, a conversa e os anexos
vinculados compõem o contexto automático. Dados e documentos antigos do Caso só
são consultados mediante pedido explícito na conversa. A política orienta a
perguntar quando a ambiguidade muda significativamente o resultado e a avançar
com lacunas sinalizadas quando possível, sem inventar fatos.

- [Estado atual e evidências](../../implementacoes/features/assistente-conversacional/estado-atual.md).
- [Contratos HTTP, persistência e configuração efetivos](../../implementacoes/features/assistente-conversacional/especificacoes/07-contratos-implementados.md).
- [TODO da feature, pendências e melhorias](../../implementacoes/features/assistente-conversacional/TODO.md).

As novas flags estão desligadas por padrão. A rodada real reduzida com GPT-6.1
Terra passou nos checks técnicos (seis interações, US$ 0,04549805); avaliação
ampliada, homologação jurídica e preparação do piloto continuam pendentes. OCR
permanece desativado e em espera. O [dossiê anterior](../../implementacoes/standBy/assistente-ia/README.md)
preserva planos históricos, sem substituir os contratos atuais.

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
servidor, dentro dos limites de contexto. A intenção natural é o padrão do
endpoint de interações; o adaptador legado de mensagens conserva seus contratos
por compatibilidade.
Veja [contratos efetivos, evidências e limites](../../implementacoes/features/assistente-conversacional/evidencias/e1-e3-historico-2026-11-11.md).

A conversa livre é o comportamento padrão da conversa persistida, sem flag
separada de intenção automática. Com revisão e histórico habilitados,
`suportaConversaLivre` informa a disponibilidade desse fluxo ao frontend. O endpoint
`POST /assistente/v2/conversas/{id}/interacoes` recebe texto natural e versão da
Peça. A mesma geração decide entre responder, propor edição e gerar minuta.
Propostas abrem diretamente a comparação; só a aprovação grava a Peça.
Pedidos seguintes podem ajustar uma proposta pendente, preservando o aplicado.
Conversa exige `AI_ASSISTANT`; propostas e aplicação exigem `AI_EDIT`; minuta
inicial/substituição integral também exigem `AI_DOCUMENT_GENERATION`.
Veja [ativação, contratos, testes e limites](../../implementacoes/features/assistente-conversacional/evidencias/e3-conversa-livre-2026-11-11.md).

## Documentos e recuperação

Com `AssistenteDocumentos:Enabled=true`, o frontend recebe a capacidade `documentos`
e pode anexar arquivos, acompanhar processamento e sinalizar leitura parcial.
O contexto automático inclui trechos confiáveis dos anexos vinculados à
conversa. Documentos antigos do Caso não entram no catálogo/contexto inicial;
sua consulta exige pedido explícito na conversa e acesso vigente ao mesmo Caso.
O aviso passa a exigir aceite `-documents-v1`.
OCR permanece desligado por padrão; não se anuncia leitura de digitalizações.
Veja [ativação, limites e evidências documentais](../../implementacoes/features/assistente-conversacional/evidencias/e2-documentos-2026-11-11.md).

Com `AssistenteDocumentos:RetrievalEnabled=true`, o catálogo substitui o envio
integral de documentos no fluxo livre. Ferramentas autorizadas buscam/leem trechos,
com cobertura persistida e evento SSE `contexto.documental`. Pedidos de leitura
integral incompleta ficam sem grupos aplicáveis. Veja [contratos efetivos e limites](../../implementacoes/features/assistente-conversacional/evidencias/e2-recuperacao-documental-2026-11-11.md).

## Painel e compatibilidade

O painel oferece conversa, uma caixa de mensagem, anexos, envio/interrupção,
progresso e avisos. Propostas continuam sujeitas à revisão e aprovação visual.
Não há botões que preenchem prompts, escolha manual de modo/escopo, seleção de
contextos cadastrados ou campo paralelo de instrução.

Antes de enviar, a interface sincroniza a Peça. Seleção e cursor não são
capturados nem enviados como foco, inclusive ao reenviar um pedido histórico.
O usuário pode descrever naturalmente o trecho que deseja alterar. As propostas
históricas permanecem legíveis; a pausa não apaga dados ou muda suas âncoras.

A capacidade `suportaConversaLivre` é necessária para novos envios no painel.
Quando indisponível, a interface informa a condição e mantém a leitura do
histórico disponível; não retorna aos controles antigos. Endpoints e contratos
legados permanecem para compatibilidade, sem reativar sua interface.

Fechar e reabrir o painel da mesma Peça preserva o estado local. A retomada após
reload depende do histórico persistido, habilitado pelas flags de conversa.
Aplicação e reconciliação continuam sujeitas a acesso, versão, grupos e hashes.

## Endpoints legados e compartilhados

| Método | Rota | Função |
|---|---|---|
| `GET` | `/assistente/capacidades` | informa disponibilidade, modos e limites visíveis ao usuário |
| `GET` | `/assistente/limite-mensal` | retorna período UTC e percentuais utilizado, reservado e disponível da organização autenticada |
| `GET` | `/assistente/transparencia` | retorna o aviso atual e se o usuário já o confirmou |
| `POST` | `/assistente/transparencia/confirmacoes` | registra a confirmação da versão atual do aviso |
| `POST` | `/pecas/{pecaId}/assistente/solicitacoes` | gera uma resposta ou sugestão em stream SSE |

As rotas acima exigem JWT organizacional. O contrato legado de solicitação/SSE
está na [especificação v1](../../implementacoes/standBy/assistente-ia/especificacoes/contrato-api.md).
As rotas de conversa, propostas, documentos, geração e sessão pessoal estão no
[inventário implementado](../../implementacoes/features/assistente-conversacional/especificacoes/07-contratos-implementados.md).
A sessão pessoal usa JWT/esquema separado; não autoriza os endpoints organizacionais.

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
distribuída dessas cotas permanece pendente antes de operar múltiplas instâncias. A saída usa
um único teto configurável, de 6.000 tokens por padrão; perfis por operação e
retry automático ainda não estão implementados.

O limite financeiro mensal é compartilhado pela organização e coordenado no
PostgreSQL entre instâncias. A configuração inicial do piloto é US$ 5,00 por
mês calendário UTC, com ajuste persistido por organização via operação auditada.
Cada chamada física reserva saldo antes do envio; o consumo informado pelo
provedor liquida a reserva, inclusive quando a resposta gerada é inválida.
Consumo desconhecido mantém a reserva até reconciliação operacional. A virada
de mês preserva o histórico e as reservas vinculadas ao período anterior.

O endpoint de consulta e a interface mostram somente percentuais, usando o
termo **limite mensal**. A insuficiência de saldo impede novas chamadas de IA e
preserva histórico, propostas, edição manual e exportação. Tarifas reais e sua
versão precisam ser configuradas; ausência de preço válido bloqueia o provedor.
Configurações de simulação sem controle financeiro são proibidas em produção.
Veja [configuração, reconciliação e evidências de P03](../../implementacoes/features/assistente-conversacional/evidencias/p03-limite-mensal-2026-11-18.md).

O sistema registra metadados técnicos de execução e consumo sem usar conteúdo
jurídico como telemetria. Monitoramento completo, retenção/reaplicação de
exclusões em backups e preparação do piloto continuam em P04–P08 do TODO.
Não há expiração automática de chats nem eliminação de backup entregue.

No transporte ligado à requisição, a resposta estruturada é acumulada/validada
antes de emitir `conteudo.delta`. No modo durável, SSE confirma o enfileiramento
e o resultado é recuperado por consultas ao job/histórico. O produto não anuncia
stream de texto parcial como minuta aprovável.

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

## Memória de conversas extensas

`AssistenteIa:HistoryMemoryEnabled` (default false) habilita resumo privado versionado,
origens verificadas e `consultar_historico`, independentemente da leitura de documentos.
A atualização automática compartilha limites e ledger da geração. Não substitui nem
exclui o histórico, e não altera a aprovação visual de mudanças. Aplicar a migration
`20261112120759_AddAssistantVersionedMemory` antes de habilitar. Confira
[ativação, limites, invalidação e evidências](../../implementacoes/features/assistente-conversacional/evidencias/e2-memoria-conversa-2026-11-12.md).

## Análise documental extensa durável

`AssistenteDocumentos:AnalysisEnabled` (default false) habilita sínteses por lote,
consolidação e preparação final do mesmo pedido em segundo plano. Exige documentos,
recuperação e conversa livre. Migrations, estados, orçamento, retomada e evidências
estão no [registro da implementação](../../implementacoes/features/assistente-conversacional/evidencias/e2-analise-documental-duravel-2026-11-12.md).
Aprovação visual continua sendo a única aplicação na Peça. A geração geral do
chat v2 também possui worker e geração por seções, descritos a seguir; o endpoint
v1 independente mantém o fluxo ligado à requisição.


## Geração geral durável e minutas por seções

`AssistenteIa:DurableGenerationEnabled` habilita a execução durável do chat v2.
O fluxo de conversa padrão requer revisão e histórico persistidos
(`ReviewV2Enabled` e `ConversationV2Enabled`). Pedido,
resposta e job são persistidos antes de contexto/modelo. O envio retorna SSE 200
com `solicitacao.enfileirada`; o frontend acompanha
`/assistente/v2/conversas/{conversaId}/geracoes/{id}` e recupera mensagens finais.
Fechar o painel ou perder a conexão não cancela o job. Cancelamento e retomada
são comandos separados.

Planejamento, geração e revisão das seções produzem uma única proposta integral.
Rascunhos incompletos permanecem privados e não ficam aprováveis. Defaults:
12 chamadas/180 mil tokens, 8 seções, lease de 180 s e prazo de 1.800 s. O ledger
contabiliza chamadas físicas; uso incerto não dispara retry pago automático.
A revisão interna não homologa mérito jurídico. Ver
[ativação e evidências](../../implementacoes/features/assistente-conversacional/evidencias/e2-e3-geracao-duravel-2026-11-12.md).

## Histórico pessoal e exclusão autorizada

A rota frontend `/historico-assistente` usa sessão pessoal separada, com
`/auth/pessoal/login`, `/refresh` e `/logout`. As consultas sob
`/assistente/v2/pessoal` exigem titular ativo/sessão vigente, mas não Membership.
Permitem ler material histórico próprio e anexos privados, sem consultar o
Caso/Peça atuais, gerar conteúdo ou aplicar propostas.

O DELETE pessoal exige `requestId` e `confirmarExclusao:true`; retorna protocolo
202. A conversa é bloqueada e o worker remove mensagens, anexos privados e
contexto derivado no ambiente ativo. Peça aprovada e documentos do Caso são
preservados. Não há exclusão automática por inatividade, e a operação aceita
continua sendo processada com as flags de geração desligadas.

O estado final declara somente `RemovidaDoAmbienteAtivo`; remoção de backups não
foi comprovada. Registro externo/reaplicação de exclusões e ensaio de restauração
seguem pendentes. Ver [histórico pessoal e expurgo](../../implementacoes/features/assistente-conversacional/evidencias/e1-historico-pessoal-exclusao-2026-11-12.md)
e [procedimento operacional](../../implementacoes/features/assistente-conversacional/especificacoes/06-operacao-exclusao-restauracao.md).
