# Histórico pessoal e exclusão autorizada — 2026-11-12

Recorte implementado no backend e em `../JuridicoAi-frontend`. Não houve implantação,
chamadas ao modelo real ou execução de exclusão em dados do usuário.
Os testes usaram bancos, contas e arquivos fictícios descartáveis.

## Experiência entregue

A rota `/historico-assistente` pode ser aberta pelo menu do usuário ou pelo link
na página de login. O titular entra com seu e-mail/senha numa sessão pessoal,
independente da organização. Pode listar conversas, recuperar mensagens anteriores,
consultar comparações salvas e baixar os anexos privados do próprio chat, inclusive
após perder Membership, acesso à Peça ou vínculo com uma organização desativada.
Conta desabilitada continua sem acesso.

Essa tela é somente de consulta: não abre editor, não atualiza contexto, não faz
chamadas de IA nem oferece aprovação de propostas. A leitura não consulta títulos,
conteúdo atual ou arquivos compartilhados dos Casos. Comparações retornam somente
os grupos persistidos apresentados na conversa, sem snapshots internos ou comandos
de aplicação. Nós do editor são renderizados como elementos/texto React, sem
executar HTML, carregar imagens remotas ou transformar links em acessos ao Caso.

Excluir exige abrir a confirmação da conversa e clicar em **Confirmar exclusão**.
**Manter conversa** fecha sem enviar exclusão. A descrição discrimina mensagens,
propostas e anexos privados; a Peça, suas alterações aprovadas e os documentos
compartilhados do Caso permanecem. O protocolo fica disponível após recarregar a
tela. Falha na resposta é reconciliada pelo mesmo identificador, sem autorização
adicional ou segundo trabalho. A conclusão é `RemovidaDoAmbienteAtivo`;
`eliminadaDasCopias` continua falsa, sem alegar expurgo de backups.

## Autenticação e autorização

- Esquema JWT `PersonalHistory`, audience `JuridicoAI.History`, somente sub e
  sessionId, sem organizationId ou role. Os endpoints pessoais exigem esse esquema;
  as rotas existentes continuam exigindo o esquema organizacional.
- Access de 15 minutos, sessão/refresh de 7 dias, usuário ativo e sessão vigente
  verificados no banco em cada autenticação. Perda de Membership não é tratada
  como desativação da identidade.
- Refresh em cookie HttpOnly, Secure, SameSite=Strict, path `/auth/pessoal`; rotação
  por comparação atômica do hash. São reaproveitados Argon2id, geração aleatória e
  SHA-256 existentes. Tokens, senhas e conteúdo não entram em logs ou URLs.
- O cookie possui um segredo aleatório de revogação estável e um segredo de
  renovação rotacionado, ambos armazenados somente como hashes. Um cookie anterior
  não renova acesso, mas pode revogar a própria sessão. Isso fecha a corrida entre
  refresh e logout, inclusive entre abas; não dá acesso a outra sessão.
- Frontend isola o token pessoal do organizacional, compartilha uma renovação em
  voo e impede que sua conclusão restaure um token após logout. **Sair da conta**
  no workspace revoga também a sessão pessoal; falhas não são anunciadas como
  encerramento bem-sucedido.
- Login/refresh/logout pessoais usam o rate limit de autenticação existente,
  validação de Origin e limite de corpo de 16 KiB. Exclusão aceita até 4 KiB.
- Origens de desenvolvimento localhost/127.0.0.1 admitem portas livres. Produção
  configura `Auth:PersonalHistoryAllowedOrigins` com origens exatas. Usar HTTPS e
  frontend/API no mesmo site para o cookie Strict; em desenvolvimento, manter o
  mesmo hostname nos dois serviços.

## Persistência e remoção

Migration aditiva `20261112164027_AddPersonalAssistantHistory` cria
`personal_history_sessions`, `ai_deletion_requests` e estado/marcador de exclusão
em `ai_conversations`, além dos índices de paginação. Aplicar junto às migrations
anteriores. Rollback destrutivo é bloqueado quando existem sessões/protocolos.

O aceite bloqueia a conversa na mesma transação que cria o protocolo e cancela
leases de geração, análise e extração privada. O filtro de conversas ativas impede
novos pedidos, leituras, uploads e aplicações sobre a conversa em exclusão. Uma
nova conversa para a mesma Peça só pode ser aberta após remover a anterior.

O worker de exclusão executa independentemente das flags de IA e de Membership.
Remove arquivos privados com checkpoints, temporários privados de extração,
memória, recibos, etapas/seções de geração, análises/lotes, mensagens, propostas,
extrações/índices privados e a conversa. Não mantém locks durante I/O de arquivos.
O expurgo de linhas é transacional e ordenado pelas FKs; armazenamento indisponível
preserva o protocolo e retoma a remoção. Worker antigo não publica após perder o
fence e não recria conteúdo já removido. O ledger técnico, sem texto jurídico,
permanece sem impedir o expurgo.

Depois da conclusão, o protocolo guarda apenas IDs, datas e estado/código técnico;
a lista de caminhos de arquivos é esvaziada. Não há retenção automática por TTL nem
expurgo por inatividade, revogação ou arquivamento. Cada expurgo deriva de uma
confirmação explícita do titular autenticado.

## API

Autenticação: `POST /auth/pessoal/login`, `/refresh`, `/logout`.

Sob `/assistente/v2/pessoal`:

- `GET /conversas?antes=...&limite=30`: cursor por atualização e ID, sem buscar
  títulos atuais do Caso; até 50 itens por página.
- `GET /conversas/{id}`, `/conversas/{id}/mensagens?antes=...&limite=50` e
  `/conversas/{id}/anexos`.
- `GET /conversas/{id}/anexos/{anexoId}/arquivo`: apenas anexo privado da conversa.
  ID de fonte compartilhada ou de outro chat retorna 404.
- `GET /propostas/{id}`: comparação já persistida; sem snapshot ou ação atual.
- `DELETE /conversas/{id}`: `{requestId, confirmarExclusao:true}`, 202 e protocolo;
  replay continua válido após o conteúdo ter sido removido.
- `GET /exclusoes/{requestId}` e `/exclusoes`: protocolo próprio e até 30 recentes.

Respostas pessoais são no-store. Esses contratos não dependem de IA estar
habilitada: manutenção/rollback de geração não deve apagar o acesso ao histórico.

## Evidências de validação

- Backend completo: **281 aprovados, 1 OCR ignorado**, incluindo oito novas provas
  PostgreSQL de titularidade, ausência de Membership, paginação com empate,
  preservação de Peça/documentos compartilhados/anexo alheio, grafo completo de
  análise/geração, retomada de armazenamento e exclusão durante chamada ao modelo.
- Frontend: **133 testes unitários**, build e lint aprovados. Inclui isolamento de
  sessão, refresh concorrente, logout durante refresh e idempotência da exclusão.
- Playwright: **30 cenários focados aprovados** na seleção de conversa livre,
  histórico, revisão persistida e área pessoal; os cinco cenários pessoais foram
  reexecutados após o fechamento da autenticação. Não é a suíte E2E integral.
- Prova HTTP com a API e o PostgreSQL reais: autenticação assinada, isolamento dos
  esquemas, leitura após remover Membership, anexo privado legível e fonte
  compartilhada negada, outro titular negado, cookie/Origin/rotação, conta
  desabilitada negada, logout com cookie anterior revogando access, exclusão
  confirmada/idempotente e preservação da Peça/arquivo do Caso. **Zero chamadas de
  IA**. Script reproduzível em `tools/smoke-tests/personal-history-http.py`.
- Serviços de teste usam portas livres, sem ocupar 5173, e são encerrados ao final.

## Implantação e limites operacionais

Backups não são apagados por este código. O procedimento de restauração e os
critérios ainda pendentes estão em
[operação de exclusão e restauração](../especificacoes/06-operacao-exclusao-restauracao.md).
A política de retenção, o registro externo de exclusões e a prova de restauração
em infraestrutura de produção continuam pendentes; não há SLA de expurgo contratado.

O novo namespace temporário permite remover cópias privadas de extração por
conversa. Na implantação, drenar workers anteriores e verificar temporários
legados sem identificação de conversa antes de considerar o ambiente saneado.
Não apagar diretórios legados indiscriminadamente durante trabalhos ativos.

Antes de voltar a um binário sem o filtro `EmExclusao`, concluir todos os expurgos
já autorizados em manutenção. Preservar migrations/protocolos; não executar um
rollback que reexponha conversas ainda em remoção.

Avaliação com modelo real, orçamento operacional/rollout e calibração do OCR não
foram declarados concluídos por este recorte. OCR permanece em stand by.
