# TODO — Assistente conversacional

Atualizado em **2026-11-18**. Referência completa:
[estado atual da implementação](estado-atual.md).
O [plano E0–E5](especificacoes/05-entrega-e-validacao.md)
foi reconciliado com o código e as evidências; os IDs abaixo têm critérios de
conclusão no estado atual.

Este TODO acompanha exclusivamente esta feature. As entregas restantes do escopo,
as melhorias observadas na avaliação e os itens em espera estão separados abaixo.

A preparação transversal do ambiente está em
[produção e testes locais](../preparacao-producao/README.md), ainda em planejamento.
Esse plano relaciona P03–P08 sem encerrar as pendências daqui. Testes com IA real
só devem ser executados quando explicitamente solicitados, com orçamento
definido; possuir uma chave ou habilitar a IA da aplicação não autoriza testes pagos.

## Prioridade — Concluir o assistente conversacional sem OCR

Entregues tecnicamente, atrás de flags: conversa livre, revisão/aplicação
transacional, histórico persistido e memória versionada, documentos nativos e
recuperação por ferramentas, análise extensa, geração durável/minutas por seções,
histórico pessoal e exclusão autorizada do ambiente ativo. Evidências e limites
estão no [registro consolidado](estado-atual.md#o-que-já-foi-feito).
Isso ainda não declara homologação jurídica ou liberação em produção.

**Ajuste de produto em 2026-11-17:** painel com conversa/anexos e sem atalhos ou
configuração manual do contexto; materiais do Caso consultados mediante pedido
expresso. Seleção pausada em PROJ-006. Ver
[registro dos ajustes e validação](evidencias/painel-simplificado-2026-11-17.md).

## Avaliação e homologação

- [x] **P01, recorte inicial — Avaliação real reduzida:** quatro cenários/seis
  interações aprovados nos checks técnicos com GPT-6.1 Terra; custo calculado
  total de US$ 0,04549805, abaixo do teto de US$ 1,00.
- [ ] **P01, ampliação — Avaliação real completa:** executar CONV-002/003/004
  (análise integral documental e memória extensa) com orçamento adicional definido;
  registrar/corrigir achados. A investigação de latência está na melhoria M01.
- [ ] **P02 — Revisão humana:** avaliar qualidade jurídica, fatos e fontes nos
  resultados reais; checks automáticos não substituem esta aprovação.

[Resultados, consumo e limites da rodada](evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).
A suíte completa mantém sete cenários/nove interações. A aprovação técnica do
recorte não conclui a homologação jurídica nem os cenários adiados. A chave
utilizada foi temporária; não ficou configurada para novas chamadas.

## Próximos passos de desenvolvimento

Sequência para o trabalho de implementação e preparação restante. O diagnóstico,
a correção, a instrumentação e a comparação real de M01 foram concluídos. A
espera remanescente pela política de cotas está em M03. O desenvolvimento de
código de P03 foi implementado e validado em PostgreSQL descartável e frontend
(sem tarifas reais preenchidas no ambiente nem homologação do piloto). A revisão
dos resultados disponíveis (P02) continua aberta. Esta lista não libera o piloto
sem as validações e condições operacionais.
Os cenários pagos adiados de P01 ficam para a rodada ampliada com orçamento próprio.

| Ordem | Frente | Próxima entrega concreta |
|---|---|---|
| 1 | M01 — Latência | Concluído: diagnóstico, correção local e comparação real; espera remanescente acompanhada em M03 |
| 2 | P03 — Controle financeiro | Implementado em código (configuração inicial US$ 5,00); falta apenas configurar tarifas/limites reais no ambiente de deploy |
| 3 | P04 — Monitoramento | Instrumentação, métricas e alertas de consumo, falhas, jobs sem progresso e exclusões pendentes |
| 4 | P05 — Backup/exclusão | Registro externo mínimo, reaplicação automatizada e ensaio de restauração sem reativar conteúdo excluído |
| 5 | P06 — Rollout | Gate v2 por organização, separado das flags operacionais, aplicado nas capacidades e operações autorizadas |
| 6 | P07 — Editor/experiência | Fechar a matriz de compatibilidade e corrigir regressões identificadas na jornada |
| 7 | P08 — Implantação | Configuração e roteiro de deploy/rollback ensaiados, preservando dados e exclusões autorizadas |

### Entregas ainda abertas do escopo

- [x] **P03 — Desenvolver controle financeiro de produção (código e testes concluídos; configuração operacional pendente no deploy).**
  Implementadas tabelas e migrations (`ai_monthly_accounts`, `ai_usage_reservations`,
  `ai_organization_monthly_limits`, `ai_financial_audit_logs`), reserva atômica em
  PostgreSQL por chamada física com serialização consistente por organização,
  liquidação pelo `usage` retornado (com cache e congelamento de modelo/tarifas/período UTC
  sem fallback após congelamento e bloqueio em divergência de modelo), retenção de
  reservas `Indeterminate`, recuperação auditada de reservas órfãs `Reserved` e
  reconciliação via CLI (`set-ai-monthly-limit`, `reconcile-ai-reservation`,
  `recover-orphan-ai-reservation`), endpoint `GET /assistente/limite-mensal` (somente
  percentuais) e integração no painel do editor + fake API E2E. Configuração inicial
  definida em US$ 5,00/organização/mês (não implantada); tarifas do modelo continuam
  vazias até configuração real de deploy.
  Ver [evidência P03](evidencias/p03-limite-mensal-2026-11-18.md).
- [ ] **P04 — Completar instrumentação e monitoramento.** Registrar tempos por
  etapa e métricas de chamadas/tokens/custo, falhas, leases, filas sem progresso e
  exclusões abertas. Configurar alertas e procedimento de resposta, sem conteúdo
  jurídico ou credenciais nos logs. Os limiares operacionais precisam ser definidos.
- [ ] **P05 — Desenvolver proteção das exclusões após restauração.** Exportar
  autorizações aceitas para registro mínimo independente do backup; implementar
  importação/reaplicação idempotente antes de abrir tráfego. Definir retenção e
  ensaiar restauração conjunta de banco/arquivos, preservando Peças e fontes do
  Caso. Seguir o [procedimento operacional](especificacoes/06-operacao-exclusao-restauracao.md).
- [ ] **P06 — Implementar gate de rollout v2 por organização.** Separar habilitação
  do piloto das flags de operação; refletir a autorização nas capacidades e
  revalidá-la no backend. Conferir a seleção v1/v2, isolamento multiusuário e
  desligamento sem apagar histórico ou interromper exclusões já autorizadas.
- [ ] **P07 — Fechar compatibilidade e experiência.** Consolidar fixtures de
  schema/formatação, listas, links, headings, emoji e blocos antigos; validar
  documentos longos, acessibilidade, paginação, undo/autosave e exportação sem
  previews. Corrigir os problemas encontrados e verificar a apresentação dos
  alertas de leitura parcial. Registrar versões/licenças e revisar o aviso
  vigente. Compilador, comparação e aplicação transacional já estão implementados.
- [ ] **P08 — Preparar e ensaiar implantação.** Configurar armazenamento
  persistente, HTTPS/origens e permissões; aplicar migrations, drenar workers
  antigos e conferir temporários legados. Ensaiar deploy/rollback sem downgrade
  destrutivo, perda de histórico ou reexposição de conversas em exclusão.

P03 exige configuração das tarifas e do ambiente; P05/P06 exigem desenvolvimento
adicional; P04 combina instrumentação e operação; P07 exige validação e eventuais
correções; P08 exige configuração e ensaio. Permanecem condições para o piloto,
não apenas melhorias opcionais.

## Melhorias evidenciadas na avaliação reduzida

Origem: [rodada real com GPT-6.1 Terra](evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).
Estas melhorias não substituem as entregas P01–P08 ainda pendentes.

- [x] **M01 — Investigar e reduzir a latência da consulta após aprovação.**
  Prioridade no [TODO geral do projeto](../../../TODO.md): **medium** (PROJ-001).
  CONV-001/3 levou **63,941 s** para responder apenas o nome da contratante e o
  valor já aprovados. **Recorte local concluído em 2026-11-14:** reproduzidos o
  reagendamento adicional de aproximadamente 60 s e o consumo de cota em tentativas
  negadas. Worker reavalia capacidade local em até 2 s, sem contornar o limitador;
  admissão foi corrigida e etapas ganharam medições sem conteúdo jurídico.
  Ver [diagnóstico, correções e testes](evidencias/m01-latencia-local-2026-11-14.md).
  **Comparação real concluída em 2026-11-14:** cenário original de três interações
  aprovado; consulta de 63,941 s para **22,697 s**, com geração no provedor de
  2,555 s e aproximadamente 18,905 s até a admissão, após nove bloqueios locais.
  Custo total de US$ 0,03260130. Ver [teste real](evidencias/m01-latencia-real-2026-11-14.md).
  Redução observada em uma rodada, sem SLA universal. A espera remanescente pela
  política de cotas fica em M03. Relaciona-se a P04/P07.
- [x] **M02 — Corrigir a preparação do cenário de isolamento entre usuários.**
  O avaliador fazia upload com o segundo titular fictício antes de confirmar seu
  aviso de IA e recebia HTTP 428 (`AI_TRANSPARENCY_ACK_REQUIRED`). O setup passou a
  consultar/confirmar o aviso com a sessão desse titular. Correção verificada com
  API/PostgreSQL sem IA, 20 testes offline e execução real de CONV-007 aprovada.
  A proteção do produto foi preservada; a falha estava na preparação do teste.

- [ ] **M03 — Rever a política de cotas para etapas internas e mensagens.**
  Prioridade: **medium**. A consulta real ainda aguardou aproximadamente 18,9 s
  pela reposição da cota, após duas minutas de cinco etapas cada. Avaliar a
  distinção entre interação do usuário e operação interna, mantendo proteção de
  concorrência, abuso e orçamento. Definir a política antes de mudar limites ou
  unidade de admissão; não elevar cotas automaticamente para aparentar melhoria.
  Acompanhar com as medições existentes e articular com P03/P04. Não é falha do
  modelo nem novo retry externo; não bloqueia o desenvolvimento financeiro P03.

Na leitura parcial, os alertas estruturados estavam presentes e os checks
passaram. Conferir sua apresentação no frontend continua no fechamento de P07;
esta rodada de API não demonstrou um defeito visual nem homologou essa experiência.

## Em espera — OCR

- [ ] **P09 — Calibração do OCR**, pausada por orientação do responsável pelo produto.

O [registro de OCR em espera](../../standBy/calibracao-ocr/README.md)
preserva os critérios de retomada. O pipeline existe e permanece desativado;
imagens e regiões digitalizadas continuam sinalizadas como não lidas. Leitura
nativa de PDF/DOCX/TXT segue no escopo ativo.

## Em espera — seleção de trechos

- [ ] **PROJ-006 — Decidir o comportamento da seleção de trechos.** Pausada
  por decisão do produto em 2026-11-17. O painel não deve capturar nem enviar
  seleção/cursor em novos pedidos ou reenvios. Definir futuramente referência
  versus limite da edição, indicação e remoção do foco e tratamento de seleção
  desatualizada em edição, retomada e reenvio. Preservar leitura de históricos e
  propostas antigas. Ver o [TODO da raiz](../../../TODO.md#melhorias-futuras--fora-do-mvp).

O usuário continua podendo descrever o trecho e a alteração desejada no chat.
A retomada da seleção não é critério de lançamento do MVP.
