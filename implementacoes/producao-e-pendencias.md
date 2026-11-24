# Preparação para produção e pendências abertas

Atualizado em **2026-11-24**. Consolida o que falta para publicar o [MVP](../MVP.md)
em nuvem e as pendências técnicas/jurídicas pós-MVP. O acompanhamento priorizado
de lançamento fica no [TODO geral](../TODO.md).

---

## 1. Política de ambientes e testes com IA

O mesmo código roda em desenvolvimento local e em nuvem, separando banco,
storage, segredos e limites por ambiente:

| Aspecto | Desenvolvimento local | Homologação / Produção (nuvem) |
| :--- | :--- | :--- |
| Transporte | HTTP em loopback permitido | HTTPS obrigatório (`HTTPS_REQUIRED`) |
| Origem (`Cors:AllowedOrigins`) | Localhost automático | Lista explícita de origens HTTPS |
| Cadastro (`Auth:RegistrationEnabled`) | `true` em Development | `false`; provisionamento via CLI |
| Banco e storage | PostgreSQL local / descartável + pasta local | PostgreSQL privado + storage persistente privado |
| Migrations | `RunMigrations=true` opcional | `migrate-database` antes do runtime (`RunMigrations=false`) |
| IA em testes | Simulada por padrão | Simulada por padrão; IA real só por solicitação explícita |

**Regra contra consumo acidental de tokens:** build, suíte padrão e integração
com PostgreSQL usam apenas IA simulada — mesmo que exista chave real no ambiente.
Rodadas pagas exigem solicitação explícita, orçamento definido e comando dedicado
(`--confirm-real-run`).

---

## 2. O que já está validado localmente (pronto no código)

- **Segurança da API (`PROD-003` / `P06`):** HTTPS obrigatório em Production,
  cookie de refresh `Secure`/`HttpOnly`/`SameSite=Strict`, corrida refresh/logout
  corrigida, CORS estrito, proxies confiáveis (`KnownProxies`/`KnownNetworks`),
  rate limit por IP real, cadastro fechado por configuração e acesso automático ao
  agente para organizações ativas.
- **Assistente conversacional v2:** conversa livre, histórico/memória versionada,
  anexos privados com leitura nativa (PDF/DOCX/TXT), recuperação documental,
  análise e geração duráveis por seções, revisão visual com aprovação transacional,
  histórico pessoal (`/auth/pessoal/*`) e exclusão do ambiente ativo.
- **Controle financeiro (`P03`) e monitoramento local (`P04`):** limite mensal UTC
  compartilhado no PostgreSQL (default US$ 5,00/org), reserva por chamada com
  contabilização de cache, CLI de reconciliação e stack OTLP/Prometheus/Grafana/Alertmanager
  em [ops/monitoring/](../ops/monitoring/README.md).
- **Ensaio local de publicação:** build limpo em containers `Production`,
  migrations controladas, exportação Word/PDF, persistência após recriação e
  backup/restauração conferidos por SHA-256 em [ops/rehearsal/](../ops/rehearsal/README.md).

---

## 3. Pendências para publicação do MVP em nuvem

- **Hospedagem e persistência (`PROD-004`):** escolher provedor, domínio,
  certificado HTTPS, PostgreSQL privado, storage persistente compatível com
  `IFileStorage`, proxies confiáveis e injeção segura de segredos.
- **Configuração real de IA (`P03` / `PROD-002`):** definir modelo, chave,
  tarifas reais, `PricingVersion` e limite mensal do piloto; blindar testes
  padrão contra qualquer saída remota acidental.
- **Disponibilidade e operação (`P04` / `PROD-005`):** configurar canal real de
  alertas, responsável e retenção de logs; separar `readiness` de `liveness`
  (conferindo banco, migrations e storage).
- **Backup e deploy (`PROD-006` / `PROD-007` / `P08`):** configurar destino
  externo de backup e retenção (RPO/RTO), repetir ensaio de backup/restauração
  e deploy/rollback na nuvem, e validar drenagem de workers.
- **Homologação do piloto (`PROJ-003` / `P02` / `PROD-008`):** validar a jornada
  completa online com o advogado empresarial (fatos/anexos no chat → minuta →
  aprovação → edição → exportação Word/PDF conferida no Microsoft Word).

---

## 4. Pendências pós-MVP e itens em espera

### Jurídico, segurança operacional e acessibilidade (preservados do planejamento inicial)

- **`PA-17` — Validação jurídica e contratual para comercialização (crítico antes de comercializar):**
  definir base legal por operação (incluindo dados sensíveis), papéis de
  controlador/operador/suboperador, termos de uso, política de privacidade,
  acordo de tratamento, transferência internacional, fluxo de incidentes/direitos
  dos titulares e homologação jurídica formal das respostas da IA.
- **`PA-11` — Segurança operacional em produção:** executar o
  [checklist de segurança em produção](checklists/seguranca-producao.md) no
  ambiente publicado (proxy, APM, scrub de logs com marcadores sintéticos) e
  implementar coordenação distribuída de rate limit antes de escalar para
  múltiplas instâncias.
- **`PA-12` — Auditoria manual de acessibilidade:** executar o
  [checklist manual de teclado e leitores de tela](checklists/acessibilidade.md)
  (NVDA/VoiceOver/Orca, zoom 200%, foco e contraste).
- **`PA-10` — Regra permanente de fontes:** o assistente não pesquisa legislação
  ou jurisprudência externa no MVP e nunca pode alegar consulta externa não
  realizada; citações sem fonte fornecida devem ser sinalizadas para conferência.
- **`PA-15` — Otimização de bundle:** reduzir chunks do frontend acima de 500 kB.

### Evoluções técnicas do assistente e da plataforma

- **`P01` (ampliação) e `P07`:** executar cenários pesados da suíte conversacional
  (`CONV-002`, `CONV-003`, `CONV-004` — análise integral e memória extensa) com
  orçamento dedicado e revalidar a suíte de [segurança de IA](../avaliacoes/seguranca-ia/README.md)
  no modelo atual (`gpt-6.1-terra`).
- **`M03` — Política de cotas:** revisar a distinção entre interação do usuário
  e etapas internas do worker (hoje a espera pela cota local domina a latência).
- **`P05` — Exclusões pós-restauração (em espera):** volume independente e
  reaplicação automática após restauração de backup; código preservado na branch
  `feature/p05-exclusoes-restauracao`.
- **`P09` — Calibração de OCR (em espera):** pipeline Tesseract existe mas segue
  desativado (`AssistenteDocumentos:OcrEnabled=false`). Retomada exige corpus
  representativo separado para calibração e validação, medição de erros em
  campos materiais (nomes, valores, datas) e manutenção de sinalização em
  regiões ilegíveis (`tools/documents/calibrate_ocr.py`).
- **`PROJ-006` — Seleção de trechos no editor (em espera):** pausada no painel
  simplificado; antes de retomar, definir se a seleção é referência ou limite de
  edição e como reage a edições/reenvios.
- **`PROJ-004` / `PROJ-005`:** pesquisa de jurisprudência na web em fontes
  oficiais e RAG sobre base jurídica de referência.
- **Melhorias menores:** automação de CI/CD, HTTP 400 (em vez de 500) para JSON
  inválido em Peças/templates, fila persistida para limpeza de arquivos órfãos
  de documentos do Caso e controle de orçamento no avaliador C#.
