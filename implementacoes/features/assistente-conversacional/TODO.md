# TODO — Assistente conversacional

Atualizado em **2026-11-12**. Referência completa:
[estado atual da implementação](estado-atual.md).
O [plano E0–E5](especificacoes/05-entrega-e-validacao.md)
foi reconciliado com o código e as evidências; os IDs abaixo têm critérios de
conclusão no estado atual.

Este TODO acompanha exclusivamente esta feature. As entregas restantes do escopo,
as melhorias observadas na avaliação e os itens em espera estão separados abaixo.

## Prioridade — Concluir o assistente conversacional sem OCR

Entregues tecnicamente, atrás de flags: conversa livre, revisão/aplicação
transacional, histórico persistido e memória versionada, documentos nativos e
recuperação por ferramentas, análise extensa, geração durável/minutas por seções,
histórico pessoal e exclusão autorizada do ambiente ativo. Evidências e limites
estão no [registro consolidado](estado-atual.md#o-que-já-foi-feito).
Isso ainda não declara homologação jurídica ou liberação em produção.

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

Sequência para o trabalho de implementação e preparação restante. Começar pela
investigação M01 abaixo; a revisão dos resultados já disponíveis (P02) continua
aberta. Esta lista não libera o piloto sem as validações e condições operacionais.
Os cenários pagos adiados de P01 ficam para a rodada ampliada com orçamento próprio.

| Ordem | Frente | Próxima entrega concreta |
|---|---|---|
| 1 | M01 — Latência | Medir as etapas da consulta de 63,941 s, identificar a causa e corrigir esperas comprovadas; validar localmente |
| 2 | P03 — Controle financeiro | Reserva e liquidação financeiras persistidas, com limite agregado entre instâncias e bloqueio antes da próxima chamada |
| 3 | P04 — Monitoramento | Instrumentação, métricas e alertas de consumo, falhas, jobs sem progresso e exclusões pendentes |
| 4 | P05 — Backup/exclusão | Registro externo mínimo, reaplicação automatizada e ensaio de restauração sem reativar conteúdo excluído |
| 5 | P06 — Rollout | Gate v2 por organização, separado das flags operacionais, aplicado nas capacidades e operações autorizadas |
| 6 | P07 — Editor/experiência | Fechar a matriz de compatibilidade e corrigir regressões identificadas na jornada |
| 7 | P08 — Implantação | Configuração e roteiro de deploy/rollback ensaiados, preservando dados e exclusões autorizadas |

### Entregas ainda abertas do escopo

- [ ] **P03 — Desenvolver controle financeiro de produção.** Definir preços e
  orçamento do piloto; implementar reserva antes da chamada e liquidação pelo
  uso conhecido, coordenadas no banco entre instâncias. Contabilizar resumo,
  ferramentas, análise e geração; manter reserva de consumo indeterminado.
  Bloquear chamadas que excedam o saldo e apresentar o motivo no produto.
  Verificar concorrência e ausência de cobrança duplicada. Limites técnicos por
  execução e o proxy da avaliação já existem, mas não concluem esta entrega.
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

P03/P05/P06 exigem desenvolvimento adicional; P04 combina instrumentação e
operação; P07 exige validação e eventuais correções; P08 exige configuração e
ensaio. Permanecem condições para o piloto, não apenas melhorias opcionais.

## Melhorias evidenciadas na avaliação reduzida

Origem: [rodada real com GPT-6.1 Terra](evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).
Estas melhorias não substituem as entregas P01–P08 ainda pendentes.

- [ ] **M01 — Investigar e reduzir a latência da consulta após aprovação.**
  Prioridade no [TODO geral do projeto](../../../TODO.md): **medium** (PROJ-001).
  CONV-001/3 levou **63,941 s** para responder apenas o nome da contratante e o
  valor já aprovados. Ainda não foi identificada a causa. Separar as medições de
  fila, montagem do contexto, contagem de tokens, chamada ao provedor e publicação;
  registrar somente metadados técnicos, sem conteúdo jurídico ou credenciais.
  Corrigir esperas desnecessárias que forem comprovadas e verificar com testes
  locais se aprovação, contexto e durabilidade continuam corretos. Comparar os
  tempos antes/depois; novas medições pagas precisam de orçamento definido.
  Conclusão: causa documentada, correção ou encaminhamento justificado e evidência
  de verificação. Não há SLA de latência aprovado. Relaciona-se a P04/P07.
- [x] **M02 — Corrigir a preparação do cenário de isolamento entre usuários.**
  O avaliador fazia upload com o segundo titular fictício antes de confirmar seu
  aviso de IA e recebia HTTP 428 (`AI_TRANSPARENCY_ACK_REQUIRED`). O setup passou a
  consultar/confirmar o aviso com a sessão desse titular. Correção verificada com
  API/PostgreSQL sem IA, 20 testes offline e execução real de CONV-007 aprovada.
  A proteção do produto foi preservada; a falha estava na preparação do teste.

Na leitura parcial, os alertas estruturados estavam presentes e os checks
passaram. Conferir sua apresentação no frontend continua no fechamento de P07;
esta rodada de API não demonstrou um defeito visual nem homologou essa experiência.

## Em espera — OCR

- [ ] **P09 — Calibração do OCR**, pausada por orientação do responsável pelo produto.

O [registro de OCR em espera](../../standBy/calibracao-ocr/README.md)
preserva os critérios de retomada. O pipeline existe e permanece desativado;
imagens e regiões digitalizadas continuam sinalizadas como não lidas. Leitura
nativa de PDF/DOCX/TXT segue no escopo ativo.
