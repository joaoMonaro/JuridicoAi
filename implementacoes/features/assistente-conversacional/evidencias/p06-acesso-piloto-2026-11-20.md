# P06 — Cadastro fechado e acesso automático ao agente

Data: **2026-11-20**. Implementação e verificação técnica locais; sem deploy ou
homologação jurídica.

## Decisão de produto

O cadastro público pode ser bloqueado por configuração. O participante criado
para o piloto já tem acesso ao agente; não haverá lista adicional de usuários
autorizados nem gate de rollout v2. Configurações administrativas do agente serão
definidas a partir dessa base, incluindo uso de créditos. Esta entrega preserva
os controles administrativos e financeiros já existentes, sem criar painel novo.

## Implementação

- `Auth:RegistrationEnabled=false` por padrão; perfil Development define `true`.
  O ambiente pode sobrescrever por `Auth__RegistrationEnabled` ou user-secrets.
- Cadastro desabilitado retorna HTTP 403, código `REGISTRATION_DISABLED`, antes
  de consultar e-mail, criar dados ou emitir sessão/cookie.
- Provisionamento via `provision-organization`, login e refresh seguem disponíveis.
- `AI_ASSISTANT`, `AI_EDIT`, `AI_REVIEW` e `AI_DOCUMENT_GENERATION` são habilitadas
  por padrão para organizações ativas, inclusive existentes, sem inserir registros
  de liberação. Organizações inexistentes ou inativas não recebem acesso.
- Registros administrativos explícitos `false` continuam bloqueando a permissão;
  `true` restaura o acesso. Alterações via `set-ai-feature` continuam auditadas.
- Capacidades e operações usam o mesmo serviço de permissões existente. Flags
  operacionais, kill switch, aviso de transparência, isolamento de dados e limite
  financeiro mensal continuam sendo exigidos.
- Sem migration, alteração das contas locais ou chamada real ao provedor de IA.

## Verificação

Compilação sem avisos/erros e **79 testes aprovados, sem falhas ou testes ignorados**
nas duas execuções focadas:

1. `AuthApplicationServiceTests` e `AssistenteIaGovernanceTests`: **34 aprovados**.
   Inclui bloqueio padrão com resposta 403/código estável e ausência de dados/cookie,
   cadastro explicitamente habilitado e provisionamento/login/refresh com cadastro fechado.
2. Filtro `.Piloto_`, `AssistenteIaHttpSecurityTests`,
   `AssistenteMonthlyLimitPostgresTests` e `PersonalHistoryPostgresTests`:
   **45 aprovados**. Inclui conta provisionada com login e acesso automático,
   organização existente sem flags, recusa de organização inexistente/inativa,
   bloqueio/restauração administrativa auditados e isolamento entre organizações,
   além das regressões existentes de créditos, histórico e segurança HTTP.

Usado PostgreSQL 17 em contêiner descartável exclusivo, com porta em loopback,
credenciais fictícias, bancos temporários e sem volume persistente. Nenhum banco
existente foi utilizado. IA simulada; nenhuma chamada paga ou consumo real.

## Pendências operacionais

No deploy, configurar `Auth__RegistrationEnabled=false`, provisionar o
participante e conferir as contas ativas. Fechar cadastro não revoga contas
anteriormente criadas. Configurar modelo, tarifas e limite financeiro conforme P03.
HTTPS, cookies, CORS e a corrida refresh/logout continuam nas tarefas próprias.
A validação da jornada com o advogado permanece aberta no MVP/P01/P02.
