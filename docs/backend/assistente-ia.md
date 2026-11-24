# Assistente de IA

O assistente atua sobre uma Peça acessível ao usuário autenticado para responder
dúvidas, propor alterações pontuais ou redigir minutas completas. **A IA nunca
modifica `Peca.ConteudoJson` diretamente**: toda alteração exige revisão e
aprovação explícita no editor.

Mapa de rotas HTTP: [API HTTP — Assistente de IA](api.md#assistente-de-ia).
Ativação, flags e tarifas: [Configurar a IA](../operacao/configuracao-ia.md).
Pendências de produção e pós-MVP: [Preparação para produção e pendências](../../implementacoes/producao-e-pendencias.md).

---

## 1. Comportamento atual (fluxo conversacional v2)

O painel do editor exibe **conversa livre única e anexos**, sem seleção manual
de modo/escopo, atalhos de prompt ou campos paralelos de contexto. A seleção de
trechos no editor está pausada (`PROJ-006`).

- **Contexto automático:** versão salva da Peça, histórico da própria conversa e
  anexos privados vinculados a ela.
- **Materiais do Caso:** dados e documentos antigos do Caso **não** entram
  automaticamente; só são consultados mediante pedido explícito na mensagem
  atual (via ferramenta `consultar_caso`, que revalida titularidade e acesso ao
  mesmo Caso).
- **Política de redação e lacunas:** o assistente pergunta quando a ambiguidade
  muda significativamente o resultado e avança com lacunas marcadas em
  `[PREENCHER: descrição]` quando possível, sem inventar fatos, partes ou
  jurisprudência externa.
- **Revisão e aplicação transacional (`ReviewV2Enabled`):** propostas ficam
  persistidas e abrem comparação visual no editor. A aprovação chama
  `POST /assistente/v2/propostas/{id}/aplicacoes`, que grava Peça, decisões e
  recibo em uma única transação idempotente (reconciliando a versão sem novo
  `PATCH`). Rejeitar nunca grava a Peça.
- **Conversa e memória (`ConversationV2Enabled`, `HistoryMemoryEnabled`):**
  1 conversa privada por `(organization_id, user_id, peca_id)`. Conversas extensas
  usam resumo privado versionado (`ai_memories`) e ferramenta `consultar_historico`.
- **Documentos nativos e recuperação (`AssistenteDocumentos:Enabled`, `RetrievalEnabled`):**
  extração nativa de PDF (Poppler), DOCX (OpenXml) e TXT. Anexos privados não são
  promovidos automaticamente a documentos do Caso. **OCR (`OcrEnabled`) segue
  desativado e em espera**; regiões digitalizadas são sinalizadas como não lidas.
- **Análise extensa e geração durável (`AnalysisEnabled`, `DurableGenerationEnabled`):**
  pedido, resposta e job são persistidos antes da chamada ao modelo. O envio em
  `POST /assistente/v2/conversas/{id}/interacoes` emite SSE 200
  (`solicitacao.iniciada` / `solicitacao.enfileirada`) e encerra; workers
  PostgreSQL (`SKIP LOCKED`, lease de 180 s, prazo de 1.800 s) executam lotes de
  análise e minutas por seções (até 8 seções / 120k caracteres JSON), produzindo
  uma única proposta integral aprovável. Fechar o painel não cancela o job.

> **Fluxo legado v1:** `POST /pecas/{pecaId}/assistente/solicitacoes` permanece
> apenas por compatibilidade quando `ReviewV2Enabled=false` (streaming ligado à
> requisição, aplicação no TipTap condicionada a âncora válida e autosave em
> `SALVO`).

---

## 2. Histórico pessoal e exclusão autorizada

- Autenticação separada (`PersonalHistory`): `/auth/pessoal/login`, `/refresh` e
  `/logout` (cookie `SameSite=Strict`, sem `organizationId`/role).
- Rotas `/assistente/v2/pessoal/*` permitem consultar conversas, anexos e
  propostas próprias mesmo sem `Membership` ativa, sem acesso ao Caso/Peça atual
  e sem gerar ou aplicar conteúdo.
- `DELETE /assistente/v2/pessoal/conversas/{id}` (com `requestId` e
  `confirmarExclusao:true`) retorna `202` e bloqueia a conversa; o worker expurga
  mensagens, anexos privados e derivados do ambiente ativo (mesmo com flags de
  geração desligadas), preservando Peça aprovada e documentos do Caso.
- O estado final declara `RemovidaDoAmbienteAtivo`; reaplicação automática em
  backups restaurados (`P05`) está adiada para pós-MVP.

---

## 3. Controles de acesso, segurança e limites

- **Isolamento:** `OrganizationId` e `UserId` vêm exclusivamente do JWT; acesso
  cross-tenant retorna `404`.
- **Governança em camadas:**
  1. `AssistenteIa:Enabled=true` (trava superior de bootstrap);
  2. kill switch persistido `AI_ENABLED` no PostgreSQL (controlável sem reiniciar);
  3. permissões `AI_ASSISTANT`, `AI_EDIT`, `AI_REVIEW` e `AI_DOCUMENT_GENERATION`
     **habilitadas por padrão** em organizações ativas (registro `false` bloqueia
     via CLI `set-ai-feature`);
  4. aceite do aviso de transparência versionado (ausência retorna HTTP `428`
     `AI_TRANSPARENCY_ACK_REQUIRED`).
- **Privacidade:** provider configurado com `store=false`; instruções presentes em
  documentos ou contexto são tratadas como dados; logs e métricas técnicas nunca
  registram prompt, resposta, conteúdo jurídico, JWT ou chave da API.
- **Limites operacionais (defaults configuráveis):**
  - corpo HTTP: 512 KiB; instrução: 4.000 caracteres; contexto: 80.000 caracteres;
  - chamada ao modelo: 60.000 tokens de entrada / 6.000 de saída; timeout 90 s;
  - execução durável: até 12 chamadas / 180.000 tokens / 8 seções;
  - anexos: 20 MiB / 100 páginas / 25 MP por arquivo; 50 anexos / 500 MiB por conversa;
  - frequência em memória por instância: 10 req/min e 2 simultâneas por usuário;
    30 req/min e 6 simultâneas por organização (coordenação distribuída pendente
    para múltiplas instâncias).
- **Controle financeiro mensal (`P03`):** coordenado no PostgreSQL por organização
  e mês calendário UTC (default **US$ 5,00/mês**). Cada chamada física reserva
  saldo antes do envio (incluindo gravação de cache) e liquida pelo `usage`
  retornado. Chamada com consumo indeterminado retém a reserva sem retry pago
  automático até reconciliação via CLI (`reconcile-ai-reservation` /
  `recover-orphan-ai-reservation`). A API (`GET /assistente/limite-mensal`) e o
  frontend exibem apenas percentuais.

---

## 4. Persistência (tabelas principais no PostgreSQL)

| Tabelas | Responsabilidade |
| :--- | :--- |
| `ai_conversations`, `ai_messages` | Conversas privadas e mensagens persistidas |
| `ai_proposals`, `ai_application_receipts` | Propostas tipadas (`jsonb`) e recibos idempotentes de aplicação |
| `ai_documents`, `ai_document_chunks` | Anexos privados/do Caso, estado de extração e trechos |
| `ai_memories` | Resumo versionado de conversas extensas |
| `ai_document_analyses`, `ai_document_analysis_batches` | Jobs de análise documental extensa e lotes |
| `ai_generations`, `ai_generation_steps` | Jobs de geração durável e checkpoints por etapa/chamada |
| `ai_usage_ledger` | Metadados técnicos de consumo por chamada (sem conteúdo jurídico) |
| `ai_organization_monthly_limits`, `ai_monthly_accounts`, `ai_usage_reservations`, `ai_financial_audit_logs` | Limites mensais UTC, reservas por chamada e auditoria financeira (`P03`) |
| `personal_history_sessions`, `ai_deletion_requests` | Sessões pessoais e protocolos de expurgo |

Verificação contínua e avaliações de IA: [Avaliação conversacional](../../avaliacoes/assistente-conversacional/README.md),
[Qualidade jurídica](../../avaliacoes/qualidade-juridica/README.md) e
[Segurança de IA](../../avaliacoes/seguranca-ia/README.md).
Roteiro de validação de produção: [Checklist de segurança operacional](../../implementacoes/checklists/seguranca-producao.md).
