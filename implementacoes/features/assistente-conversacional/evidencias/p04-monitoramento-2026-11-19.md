# P04 — Monitoramento operacional (2026-11-19)

Branch local `feature/p04-monitoramento-assistente`, criada a partir de
`homologacao` atualizada no commit `9e85020`. Implementação técnica e ensaio
local; não representam deploy, homologação jurídica ou liberação do piloto.

## Entrega

- Telemetria técnica com `Meter`, interface `IAssistenteTelemetry` e pacotes
  estáveis OpenTelemetry 1.21.0, com exportação OTLP/HTTP protobuf configurável.
- Integração das medições de latência existentes, histogramas em segundos,
  chamadas físicas separadas de contagem remota de entrada, falhas/cancelamentos,
  tokens conhecidos e ausência de usage. Cache não é somado novamente à entrada.
- Falhas dos loops dos quatro workers, perdas de lease de geração/análise e
  ocorrências controladas de cotas, orçamento e processamento, sem texto livre
  como dimensão. Limite/ledger do P03 continuam obrigatórios e independentes.
- Coletor a cada 60 s por padrão, uma consulta agregada PostgreSQL com timeout,
  sem carregar conteúdo/IDs jurídicos: filas por tipo/estado, idade, ausência de
  progresso, prazos, leases, exclusões, reservas pendentes de todos os períodos,
  consumo/reserva do mês UTC e respostas criadas nas últimas 24 h por estado atual.
- Snapshot em memória; falha conserva valores/data anteriores e incrementa
  contador. Antes do primeiro sucesso não há zeros fictícios de consumo/filas.
  Ao terminar a última pendência, grupos conhecidos publicam zero.
- Migration aditiva `20261119113211_AddAssistantProgressMonitoring`, com marcador
  nullable de último progresso em geração, análise, extração e exclusão.
  Contexto/lotes/seções/trechos são avanços; claim, mudanças de fase e reagendamento
  por cota não atualizam o marcador. Legado usa criação como fallback.
- Console JSON com scopes quando habilitado; `GET /health/live` separado de
  prontidão. Coleta inicia depois do serviço de migrations.
- Compose independente: Collector, Prometheus, Grafana, Alertmanager e Blackbox.
  Ports administrativos/OTLP publicados somente em loopback; Grafana autenticado.
  Painel provisionado com 16 visualizações e 14 regras de alerta com procedimentos.

## Verificações executadas

| Verificação | Resultado |
|---|---|
| Build Debug e Release | sem erros/avisos |
| Suíte completa com PostgreSQL descartável | **333 aprovados, 1 ignorado (OCR), 0 falhas** |
| Testes P04 incluídos na suíte | 6 de telemetria/exportador/snapshot e 3 de integração PostgreSQL |
| EF `has-pending-model-changes` | nenhuma diferença pendente |
| Promtool: configuração e regras | válidas |
| Promtool: simulações de alertas | 10 cenários aprovados |
| Amtool: configuração do Alertmanager | válida |
| Compose | configuração validada e serviços iniciados localmente |
| Grafana API | painel provisionado, 16 visualizações |
| NuGet, dependências diretas/transitivas vulneráveis | nenhuma reportada na consulta desta rodada |
| `git diff --check` | sem erros |

Os nove testes novos verificam: dimensões limitadas e cache; listener que lança
exceção sem interromper negócio; snapshot preservado em falha; chamadas físicas
separadas de contagem, com usage conhecido mesmo em saída inválida; cancelamento
antes do envio sem chamada; payload OTLP sem marcadores privados e exportador em
falha; consulta agregada real e zeros após resolução; progresso recente em trabalho
antigo; reagendamento por cota preservando o último checkpoint.

As dez simulações Promtool verificam disparo/resolução de fila, exclusão, reserva,
orçamento e API; coleta completamente ausente; falhas do provedor com/sem sucessos;
falhas repetidas do worker e prazos excedidos. Réplicas do mesmo banco não são somadas.

## Ensaio de integração local

Usados exclusivamente PostgreSQL 17 descartável, dados fictícios e IA desativada.
A API iniciou em `Testing`, com diretório temporário próprio, migrations em banco
vazio e intervalo de coleta de 10 s para o ensaio. Não houve chamada real ao provedor,
contagem remota paga ou configuração de chaves de IA nesta entrega.

1. API → OTLP → Collector → Prometheus verificado por consultas de timestamp,
   disponibilidade do snapshot e agregados. Reinício em banco vazio sem evento de
   falha de coleta na inicialização após corrigir a ordem dos hosted services.
2. Blackbox fora do processo da API retornou `probe_success=1` para `/health/live`.
3. Conta fictícia com 4,75/5,00 e autorização fictícia antiga produziram alertas
   `BudgetCritical` e `DeletionOverdue` recebidos pelo Alertmanager. A faixa inferior
   `BudgetWarning` ficou inibida pela crítica. Nenhuma notificação externa foi enviada.
4. Marcadores privados nos nomes fictícios não apareceram nas séries exportadas.
5. PostgreSQL descartável pausado: contador de falha de coleta exportado, exclusão
   pendente anterior preservada e `/health/live` continuou HTTP 200. Após retomada,
   timestamp de coleta bem-sucedida avançou e idade voltou a poucos segundos.
6. Resolução: exclusão fictícia liberada para o worker existente e conta fictícia
   zerada: agregados voltaram a zero e nenhum alerta permaneceu disparando no Prometheus.

O ensaio de banco indisponível foi curto; o disparo por coleta ausente/desatualizada
foi comprovado separadamente com Promtool, sem alegar espera pelo prazo de alerta
na interrupção real. A API do Grafana comprovou provisionamento, sem afirmar
homologação visual pelo advogado.

## Limites e pendências

- Monitoramento fica desligado por padrão, ativável por ambiente. Não houve deploy.
- Receiver padrão é revisão pela interface local do Alertmanager. Definir canal
  real, atribuir responsável, calibrar limiares e testar notificações/resolução
  no ambiente de implantação antes de concluir P04 operacionalmente.
- Prometheus local retém 15 dias. Logs da API continuam no stdout; destino,
  rotação, retenção e acesso devem ser configurados no supervisor da implantação.
- Liveness não verifica banco/storage/migrations: readiness completa segue PROD-005.
- Estado persistido de uma interação não equivale a evento de produto; não há
  telemetria de aceitação jurídica ou medição de TTFT no navegador nesta entrega.
- Alertas observam; não liberam reservas, repetem chamadas incertas nem alteram
  trabalhos/exclusões automaticamente. Restauração continua em P05.
- Probes de API local precisam de origem alcançável a partir dos containers;
  rede do Linux nativo e configuração futura de nuvem exigem validação própria.

Detalhes de configuração, métricas e resposta no
[procedimento operacional](../../../../docs/operacao/monitoramento-assistente.md).
