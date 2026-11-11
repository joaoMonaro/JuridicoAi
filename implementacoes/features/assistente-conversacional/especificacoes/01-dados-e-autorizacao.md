# Persistência e autorização — v1 do detalhamento

> Proposta de implementação, 2026-11-11. Nenhuma migration executada.
> Contrato externo identificado como `assistente-v2` para coexistir com o atual.

## Base conferida

Backend no commit `1abfdae`, frontend `../JuridicoAi-frontend` no commit
`7700c511ff7f8ccd7e91893074e90a77a5a95769`, sem modificações locais no frontend.
Os arquivos atuais de editor, chat e autosave foram lidos. PostgreSQL 17 é o
serviço definido em `docker-compose.yml`. Mensagens não têm persistência atual.

O acesso atual a Casos/Peças é por organização; há papéis Owner e Member, sem ACL
individual por Peça. Esta entrega centraliza `CanReadPeca` e `CanEditPeca` num
serviço de acesso, inicialmente equivalente às regras existentes. Não inventa
uma gestão de permissões por Peça. Exclusão lógica, mudança de organização ou
perda de vínculo precisam impedir operações atuais independentemente do histórico.

## Convenções e integridade

- UUID de aplicação; datas em UTC/timestamptz; bytes e sequências em bigint;
  enums string com CHECK; conteúdo estruturado em JSONB; custos numeric(18,8).
- Toda entidade de conteúdo inclui `organization_id`; conteúdo privado inclui
  `conversation_id` e é autorizado pelo `owner_user_id` da conversa.
- Pais expõem UNIQUE `(id, organization_id)`; filhos têm FK composta correspondente.
  Referências entre grupos, propostas e execução incluem a conversa para impedir
  vínculos cruzados mesmo por erro de aplicação. IDs não são autorização.
- FK de conversa para Peça, usuário e organização usa RESTRICT, sem CASCADE.
  Remoções físicas precisam passar pelo fluxo de exclusão explícito. Cascata interna
  só pode ocorrer no expurgo autorizado da própria conversa.
- JSONs são validados na aplicação e têm CHECK do tipo JSON (object/array), versão
  de contrato e limite de tamanho. Não guardar payloads livres em logs de falha.
- Versões inteiras explícitas para concorrência. Transações que não executam IA
  são curtas. Nenhum lock de banco permanece aberto durante OCR ou chamada ao modelo.

## Tabelas e campos

Todos os nomes abaixo são novos; os existentes permanecem.

| Tabela | Campos específicos além de id e organization_id |
|---|---|
| `ai_conversations` | owner_user_id, peca_id, caso_id, title_snapshot varchar(200), state, next_sequence bigint, version int, deletion_epoch int, created_at, updated_at |
| `ai_messages` | conversation_id, sequence bigint, role, text, state, client_request_id nullable, request_hash nullable, referenced_proposal_id nullable, created_at |
| `ai_attachments` | conversation_id, uploaded_by_id, client_upload_id, original_name varchar(255), mime_type varchar(100), size_bytes, content_sha256 char(64), storage_id varchar(512), state, created_at |
| `ai_message_attachments` | conversation_id, message_id, attachment_id; chave composta sem duplicatas |
| `ai_document_extractions` | conversation_id nullable, attachment_id nullable, documento_caso_id nullable, caso_id, source_sha256, extractor_version, quality_policy_version, state, pages_total, pages_completed, version, created_at |
| `ai_document_chunks` | extraction_id, ordinal, page nullable, locator JSONB, text, text_sha256, quality_state, quality_metrics JSONB, exclusion_codes JSONB, search_vector tsvector |
| `ai_memories` | conversation_id, version, through_sequence, summary, facts JSONB, source_manifest JSONB, created_at |
| `ai_executions` | conversation_id, input_message_id, state, lease_token nullable, deletion_epoch, snapshot_id nullable, capability_snapshot JSONB, budget_json JSONB, usage_json JSONB, cancel_requested_at nullable, started_at nullable, finished_at nullable, failure_code nullable |
| `ai_snapshots` | conversation_id, execution_id, peca_version, document_json JSONB, document_sha256, anchor_map JSONB, created_at |
| `ai_proposals` | conversation_id, execution_id, output_message_id, revision int, decision_version int, state, initial_snapshot_id, current_snapshot_id, applicable_peca_version, created_at |
| `ai_change_groups` | conversation_id, proposal_id, group_key, ordinal, state, explanation, original_anchors JSONB, operations_json JSONB, preview_json JSONB, preview_sha256, source_manifest JSONB, decided_at nullable |
| `ai_application_receipts` | conversation_id, proposal_id, idempotency_key, request_sha256, group_ids JSONB, original_version, resulting_version, resulting_document_json JSONB, resulting_document_sha256, applied_by_id, created_at |
| `ai_events` | conversation_id, execution_id nullable, sequence bigint, event_type varchar(80), payload JSONB, created_at |
| `ai_jobs` | conversation_id nullable, extraction_id nullable, execution_id nullable, kind, state, attempt, max_attempts, available_at, lease_token nullable, lease_until nullable, checkpoint_json JSONB, error_code nullable |
| `ai_budget_reservations` | conversation_id, execution_id, call_key, reserved_tokens, reserved_cost_usd nullable, state, created_at, settled_at nullable |
| `ai_provider_calls` | conversation_id, execution_id, call_key, attempt, provider_request_id nullable, model, input_tokens nullable, output_tokens nullable, state, error_code nullable, started_at, finished_at nullable |
| `ai_deletion_requests` | conversation_id nullable, owner_user_id, request_id, state, phase, authorized_at, completed_at nullable, last_error_code nullable |
| `personal_history_sessions` | id, user_id, refresh_token_hash, expires_at, revoked_at nullable, created_at |

Proveniência da extração: exatamente um de attachment_id/documento_caso_id deve
estar preenchido. Para anexo, conversation_id é obrigatório e coincide com a
conversa do anexo; para documento de Caso é nulo. `caso_id` coincide com o do pai.
Qualidade é por trecho, não apenas por arquivo. Busca nunca inclui trechos reprovados.

O recibo contém o resultado daquela aplicação para reconciliação de falha de rede;
é privado e é removido com a conversa. O ledger técnico de consumo existente
pode permanecer sem conteúdo jurídico, incluindo IDs de correlação sem FK que
impeça expurgo. Não criar uma cópia permanente do chat numa tabela de auditoria.

## Índices e unicidade

- `ai_conversations`: UNIQUE parcial `(organization_id, owner_user_id, peca_id)`
  WHERE state IN ('Ativa','EmExclusao'); outra conversa só após concluir exclusão.
- Lista pessoal: `(owner_user_id, updated_at DESC, id DESC)`; lista por organização
  acrescenta organization_id, sem consultar títulos atuais do Caso.
- `ai_messages` e `ai_events`: UNIQUE `(conversation_id, sequence)` e índice de
  paginação ascendente; mensagens têm UNIQUE parcial `(conversation_id,
  client_request_id)` quando não nulo. Hash de pedido detecta reutilização indevida.
- Anexos: UNIQUE `(conversation_id, client_upload_id)`; não deduplicar originais
  entre conversas. Hash serve para versionar, não para revelar arquivo alheio.
- Extrações de Caso: UNIQUE parcial `(organization_id, documento_caso_id,
  source_sha256, extractor_version, quality_policy_version)`; equivalente por
  attachment_id para anexos. Chunks: UNIQUE `(extraction_id, ordinal)` e GIN em
  search_vector. Caminho de acesso autorizado antes do ranking/limite.
- Execuções: UNIQUE parcial conversation_id WHERE state em estados não terminais.
  V1 rejeita segundo pedido com execução ativa (409), preservando texto no composer;
  simplifica ordenação sem perder mensagens em fila invisível.
- Propostas: UNIQUE `(execution_id, revision)`; grupos UNIQUE `(proposal_id,
  group_key)`; recibos UNIQUE `(conversation_id, idempotency_key)`.
- Trabalhos: índice `(state, available_at, id)`; UNIQUE lógico por execução/tipo
  ou extração/etapa/página, permitindo reaquisição sem duplicar publicação.
- Reservas: UNIQUE `(execution_id, call_key)`; chamadas UNIQUE `(execution_id,
  call_key, attempt)`; ledger técnico sem texto referencia a chamada para evitar
  dupla contabilização. Resultados de uso incerto ficam identificados como tal.

Mensagens e eventos compartilham contador de ordenação da conversa, reservado
sob lock curto. Lacunas são permitidas; duplicação e regressão não. Não usar
timestamps como ordenação exclusiva ou cursor.

## Estados e concorrência

- Conversa: Ativa → EmExclusao → excluída fisicamente. Durante a remoção, novos
  trabalhos, mensagens, anexos e aplicações são bloqueados.
- Proposta: Pendente / ParcialmenteResolvida / Resolvida / Desatualizada /
  Substituida / Incompleta. Grupo: Pendente / Aplicado / Rejeitado / Desatualizado.
- Execução e arquivo seguem os estados do contrato HTTP. Um token de lease
  renovado funciona como fence: worker antigo não publica após perder posse.
- Locks de mutação seguem ordem conversa → proposta → Peça, para aprovação,
  rejeição e exclusão. Expurgo revalida deletion_epoch antes de cada etapa.

## Duas autorizações diferentes

| Ação | Regra |
|---|---|
| Ler chat ou anexos privados históricos | Usuário autenticado ativo é titular da conversa |
| Excluir chat próprio | Mesma titularidade e autorização explícita de exclusão |
| Consultar Caso/Peça atuais | Autenticação organizacional e acesso atual ao recurso |
| Enviar mensagem, upload, executar IA | Titularidade + acesso atual à Peça + flags e transparência |
| Aprovar | Regra anterior + acesso de edição + operações permitidas verificadas novamente |
| Ler documento compartilhado por link histórico | Exige acesso atual ao Caso; nunca resolve como arquivo privado |
| Owner consultar chat de Member | Negado; cargo na organização não concede titularidade |

Consulta pessoal retorna somente conteúdo já persistido do próprio chat e seus
anexos. Não hidrata referências buscando versões atuais do Caso. Memórias e
snapshots internos não são endpoints de leitura histórica; exibir somente as
mensagens e propostas já apresentadas ao titular. Links compartilhados revogados
aparecem como indisponíveis. O texto já presente na conversa permanece legível.

## Autenticação pessoal para histórico

O JWT atual falha sem Membership ativa (`Program.cs:OnTokenValidated`), e o login
atual exige organização. Portanto remover o teste de Membership da autenticação
geral seria incorreto. Adicionar esquema separado `PersonalHistory`:

- Login pessoal com email/senha existentes e rate limit de autenticação. Emite
  sessão pessoal revogável, audience `JuridicoAI.History`, sub/userId e sessionId;
  não contém authority de organização, role ou escopo de escrita.
- Access token 15 minutos; refresh pessoal 7 dias, rotacionado, cookie HttpOnly
  Secure com path próprio. Origem permitida e verificação de Origin nos endpoints
  baseados em cookie. Não colocar token em URL ou logs.
- Endpoints pessoais aceitam somente esse esquema; endpoints gerais continuam
  aceitando somente o esquema atual. Um token pessoal nunca satisfaz IAuthContext.
- Conta desabilitada ou sessão revogada continua sem acesso: “perdeu a Peça”
  não significa contornar bloqueio de identidade.
- Sessão organizacional válida pode ler o chat próprio pela rota organizacional;
  a página de histórico usa o login pessoal quando não houver sessão pessoal.
  Não alterar o login existente nem ampliar tokens antigos silenciosamente.

Essa separação implementa a decisão já aprovada inclusive quando o usuário deixou
a organização. Novas políticas de contas desabilitadas exigiriam outro escopo.

## Exclusão e backups

Autorização de exclusão gera um trabalho rastreável. Bloquear contexto imediatamente,
cancelar execuções e expurgar mensagens, eventos, anexos, OCR, memória, snapshots,
recibos e índices. Manter só o protocolo de exclusão sem texto, título ou nome de
arquivo. Repetir DELETE com o mesmo requestId retorna o mesmo protocolo.

Não prometer expurgo físico imediato de backups. Proposta operacional: conclusão
de remoção do ambiente ativo em até 24h, backups com retenção máxima de 30 dias e
registro mínimo externo de exclusões para reaplicação obrigatória antes de abrir
uma restauração. Esses prazos são configuração de implantação a validar, não SLA
já contratado. A API separa `RemovidaDoAmbienteAtivo` de `EliminadaDasCopias`.

Arquivos temporários de processamento podem ser removidos ao concluir o trabalho;
isso não apaga anexos originais nem histórico. Arquivamento, revogação e inatividade
não iniciam expurgo. Remover um DocumentoCaso revoga sua busca e invalida memórias
derivadas para novos pedidos; não apaga mensagens históricas do chat sem autorização.
