# TODO — Projeto JuridicoAI

Atualizado em **2026-11-24**. Acompanhamento geral das prioridades do projeto.
**Somente a seção "Essencial para publicar o MVP" reúne os bloqueios de lançamento.**
Referência de aceite: [MVP de redação de peças jurídicas](MVP.md).
Detalhamento técnico e pendências pós-MVP: [Preparação para produção e pendências](implementacoes/producao-e-pendencias.md).

## Essencial para publicar o MVP

### 1. Ambiente online e preservação dos dados — prioridade alta

- [x] Ensaio local isolado concluído em 2026-11-22 ([ops/rehearsal](ops/rehearsal/README.md)):
  imagens limpas, migrations controladas, leitura/exportação Word/PDF,
  persistência após recriação, backup/restauração por SHA-256 e recusa de
  configuração insegura.
- [ ] Escolher hospedagem e publicar o MVP em nuvem, com PostgreSQL privado,
  storage persistente e privado, domínio/certificado HTTPS, `Cors:AllowedOrigins`,
  `ReverseProxy:KnownProxies`/`KnownNetworks` e segredos separados dos testes locais.
- [ ] Aplicar migrations (`migrate-database`) e instalar dependências de leitura
  PDF/DOCX/TXT e exportação Word/PDF; manter OCR desativado.
- [ ] Verificar que reiniciar ou atualizar a instância preserva Peças, conversas,
  anexos e exclusões do ambiente ativo.
- [ ] Configurar backup básico de banco e documentos com destino externo e
  comprovar uma restauração com dados fictícios; registrar procedimento de rollback.

### 2. IA configurada e consumo controlado — prioridade alta

- [x] Controle financeiro mensal (`P03`) e contabilização de gravação de cache
  implementados e validados com IA simulada.
- [ ] Configurar modelo, chave, tarifas reais, `PricingVersion` e limite mensal
  da organização no ambiente publicado.
- [ ] Conferir o bloqueio por limite e acompanhar consumo e tempo de resposta no
  uso inicial, sem aumentar cotas automaticamente.

### 3. Jornada completa validada com o advogado — prioridade alta

- [x] Ensaio ampliado local (2026-11-23): 79 testes de navegador e 16
  verificações com API real e IA simulada aprovados.
- [ ] **PROJ-003 / P02 — Validar a entrada e redação pelo chat no MVP online:**
  concluir um trabalho representativo com o advogado empresarial (iniciar/retomar
  Peça, fornecer fatos/anexos no chat, obter minuta, revisar fatos/fontes/lacunas
  e confirmar que nenhuma alteração é aplicada sem aprovação).
- [ ] Confirmar aprovação/rejeição, edição manual, salvamento e retomada após
  recarga ou queda de conexão, sem perda de texto ou duplicação.
- [ ] Conferir Word (inclusive abertura no Microsoft Word) e PDF contra o texto
  salvo e o layout escolhido; corrigir defeitos impeditivos.

### 4. Conferência final e liberação — após os itens anteriores

- [ ] Reexecutar os checks na versão candidata (incluindo PostgreSQL descartável)
  e validar o fluxo publicado.
- [ ] Definir responsável pelo piloto e acompanhamento operacional (erros,
  consumo, jobs e exclusões pendentes) aproveitando `ops/monitoring/` (`P04`).
- [ ] Registrar commit, verificações e aceite do [MVP](MVP.md); promover conforme
  [versionamento](docs/versionamento.md).

## Melhorias técnicas posteriores — não bloqueiam o MVP

- [ ] Automatizar build, testes e deploy em CI.
- [ ] Implementar `readiness` real (banco, migrations e storage) e configurar
  canal externo de alertas + retenção de logs (`P04` / `PROD-005`).
- [ ] Executar [checklist de segurança operacional](implementacoes/checklists/seguranca-producao.md) (`PA-11`)
  e [checklist manual de acessibilidade](implementacoes/checklists/acessibilidade.md) (`PA-12`).
- [ ] Concluir validação jurídica, contratual e LGPD antes de comercialização (`PA-17`).
- [ ] Ampliar avaliações reais (`CONV-002`/`003`/`004` e revalidação de
  [segurança de IA](avaliacoes/seguranca-ia/README.md) no modelo atual) com
  orçamento definido.
- [ ] **M03:** rever política de cotas entre interação do usuário e etapas
  internas do worker (espera local de ~18,9 s observada na consulta real).
- [ ] Corrigir HTTP 500 → 400 para JSON inválido em Peças/templates; fila
  persistida para limpeza de arquivos órfãos de documentos do Caso; adicionar
  orçamento/timeout ao avaliador C#; otimizar bundle > 500 kB (`PA-15`).

## Melhorias futuras — fora do MVP

- [ ] **P05 — Proteção de exclusões após restauração (em espera):** código
  preservado em `feature/p05-exclusoes-restauracao`.
- [ ] **P09 — Calibração de OCR (em espera):** pipeline desativado até corpus
  dedicado e calibração.
- [ ] **PROJ-006 — Seleção de trechos no editor (em espera):** pausada no painel
  simplificado.
- [ ] **PROJ-004 — Pesquisa de jurisprudência na web** em fontes oficiais.
- [ ] **PROJ-005 — RAG sobre base jurídica de referência.**

## Principais entregas concluídas

- [x] **PROD-003 (2026-11-20):** HTTPS obrigatório em Production, cookie `Secure`,
  CORS estrito, proxies confiáveis, rate limit por IP real e correção da corrida
  refresh/logout da sessão principal.
- [x] **P06 (2026-11-20):** cadastro público fechado por configuração
  (`Auth:RegistrationEnabled=false`), provisionamento CLI e acesso automático ao
  agente em organizações ativas.
- [x] **P04 local (2026-11-19):** instrumentação OTLP, `GET /health/live` e
  stack local Prometheus/Grafana/Alertmanager ([ops/monitoring](ops/monitoring/README.md)).
- [x] **P03 código (2026-11-18):** controle financeiro mensal por organização no
  PostgreSQL (default US$ 5,00/mês UTC) e CLI de reconciliação.
- [x] **PROJ-002 (2026-11-17):** extração do scraper TJSP para `../JuridicoAI.Scrapper`
  e remoção de `/api/jurisprudencias`, MongoDB e Playwright da API.
- [x] **PROJ-001 / M01 (2026-11-14):** correção do reagendamento/admissão local
  do assistente (consulta real caiu de 63,9 s para 22,7 s).
