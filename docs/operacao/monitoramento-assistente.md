# Monitoramento do assistente — P04

Implementação local em 2026-11-19. Métricas operacionais não contêm texto jurídico,
nomes de documentos, instruções, respostas, credenciais ou identificadores de usuários,
organizações e Peças como dimensões. O controle financeiro continua no P03.

## Ativação

Por padrão `AssistenteMonitoring:Enabled=false`. A ativação habilita coleta periódica,
exportação de métricas OTLP/HTTP e console JSON com scopes. Não habilita IA.

| Configuração | Padrão | Uso |
|---|---|---|
| `Enabled` | `false` | habilitar no ambiente acompanhado |
| `OtlpEndpoint` | `http://localhost:4318/v1/metrics` | destino privado OTLP/HTTP protobuf |
| `CollectionIntervalSeconds` | `60` | intervalo entre coletas; mínimo 10 s |
| `QueryTimeoutSeconds` | `5` | timeout do comando de consulta |
| `StalledJobSeconds` | `600` | tempo sem checkpoint para sinalizar trabalho parado |
| `PendingDeletionSeconds` | `900` | idade de uma autorização ainda não concluída |
| `OldReservationSeconds` | `900` | idade de reserva pendente para revisão |

Exemplo, preservando a configuração independente de autenticação/banco/storage:

```bash
AssistenteMonitoring__Enabled=true \
AssistenteMonitoring__OtlpEndpoint=http://localhost:4318/v1/metrics \
dotnet run --project src/JuridicoAI.Api
```

O exportador usa pacotes estáveis OpenTelemetry, exporta a cada 15 s e tem timeout
próprio de 5 s. O Collector recebe e fornece métricas ao Prometheus; a API não
publica endpoint administrativo de métricas. A consulta ao banco começa depois
do serviço de migrations e possui timeout; não carrega conteúdo de documentos.
Desativar o monitoramento não desativa ledger, limites financeiros ou workers.

## Serviços locais

O Compose é separado do banco/aplicação. Todos os ports publicados estão em
`127.0.0.1`; Prometheus, Alertmanager e OTLP dependem dessa restrição de rede.
Grafana também exige login. Não publicar esses ports diretamente na Internet.

Definir uma senha local própria, sem versioná-la:

```bash
export MONITORING_GRAFANA_PASSWORD='senha-local-propria'
docker compose -p juridicoai-monitoring -f ops/monitoring/compose.yaml up -d
```

Usar `-p` explicitamente evita herdar `COMPOSE_PROJECT_NAME` de outro projeto.
A senha provisiona o administrador `admin` na primeira inicialização do volume;
rotacionar a senha de uma instalação existente pelo Grafana.

- Grafana: `http://127.0.0.1:3001`, painel **JuridicoAI — Assistente / Operação**.
- Prometheus: `http://127.0.0.1:9090`.
- Alertmanager: `http://127.0.0.1:9093`.
- OTLP: `http://127.0.0.1:4318/v1/metrics`.

O Blackbox consulta `http://host.docker.internal:5299/health/live` de fora do
processo da API. Ajustar `prometheus.yaml` para a origem real do ambiente.
Docker Desktop permite alcançar a API do host; em Docker Linux nativo uma API
ligada somente ao loopback pode não ser alcançável pelo gateway do container.
Nesse caso usar uma rede privada compartilhada com a API ou endereço de escuta
acessível apenas nessa rede; conferir com a probe antes de liberar o ambiente.
Não abrir a API ao público para satisfazer uma verificação local.

`GET /health/live` confirma somente que o processo HTTP responde. Não atesta
banco, storage, migrations ou prontidão para tráfego: readiness completa continua
em PROD-005. O monitoramento acusa falha da coleta do banco separadamente.

Para encerrar sem excluir dados do monitoramento:

```bash
docker compose -p juridicoai-monitoring -f ops/monitoring/compose.yaml down
```

Prometheus mantém 15 dias. Os volumes de Grafana/Alertmanager são persistentes.
Logs JSON da API vão ao stdout: o supervisor da implantação deve restringir acesso,
rotacionar e configurar retenção. O Compose de monitoramento não coleta logs da API.
Destino centralizado e política final de logs continuam como configuração operacional.

## Semântica dos dados

- Histogramas `ai.stage.duration` e `ai.provider.call.duration` usam segundos e
  relógio monotônico. O exportador Prometheus normaliza nomes para underscores e
  acrescenta `_seconds_bucket`, `_seconds_count` e `_seconds_sum`.
- Cada chamada HTTP física de geração produz uma observação, inclusive em erro.
  Contagens remotas de entrada usam `operation=input_tokens`; não são gerações.
  Uma interação pode gerar várias chamadas. Reenvio idempotente não é inferido
  como interação nova a partir de uma chamada física.
- `ai_interactions_24h` conta respostas persistidas **criadas nas últimas 24 h**,
  agrupadas pelo estado atual; é gauge, não contador de eventos terminais.
  Rejeitar uma resposta concluída preserva a classificação de geração bem-sucedida.
  Histórico excluído deixa de participar desse agregado.
- Tokens são os informados pelo provedor, inclusive em saída inválida com usage
  conhecido. Cache é subconjunto da entrada: não somar `cached_input` ao total de input.
  Usage ausente incrementa `ai.provider.usage.unknown`; não significa custo zero.
- Gastos e reservas mensais são gauges lidos das contas do P03 no mês calendário UTC.
  Não recalcular custos por tokens no monitoramento nem somar gauges ao longo do tempo.
- Faixas de orçamento são acumuladas: uma conta em 95% entra em 70% e 90%.
  O percentual considera **consumido + reservado**. Nenhum valor por organização é exportado.
- Reservas pendentes incluem meses anteriores. Reserva antiga é candidata à revisão,
  sem afirmar que está órfã, não enviada ou livre de cobrança.
- Trabalhos ativos são agrupados por tipo/estado. Idade é desde a criação;
  ausência de progresso usa `UltimoProgressoEm`, com criação como fallback legado.
  Claim, mudanças de fase e reagendamento por cota não contam como avanço; checkpoints
  de contexto, evidências, lotes e seções contam. Esperas por documentos/análise ficam
  visíveis pelo estado; investigar a dependência antes de tratar como travamento.
- Coleta falha preserva último snapshot e sua data. Antes do primeiro sucesso não
  há gauges de filas/consumo: observar disponibilidade e idade da coleta. Ao concluir
  a última pendência, a coleta publica zero para os grupos conhecidos.
- Várias instâncias observam o mesmo banco: agregar gauges persistidos com `max`
  por tipo/estado, **nunca somar réplicas**. Contadores de chamadas/falhas são por processo
  e devem ser agregados com `sum(rate(...))` ou `sum(increase(...))`.
- Logs existentes usam `CorrelationId`, `ClientRequestId` e IDs técnicos para
  investigação restrita. Esses IDs não viram labels. Não há coleta automática
  de bodies HTTP, prompts, respostas ou mensagens de exceção pela nova telemetria.

O painel distingue chamadas físicas dos estados persistidos das interações.
Não mede TTFT usando o primeiro fragmento de JSON do provedor: esse fragmento não
é conteúdo renderizável para o advogado. Tempos fim a fim no navegador e prontidão
completa não são demonstrados apenas pelos histogramas das etapas do backend.

## Alertas e resposta

Os limiares abaixo são valores iniciais **para ensaio**, não SLAs aprovados.
Alterar os tempos no app exige revisar as descrições/regras e repetir seus testes.
Todos usam o responsável lógico `juridicoai-operator`; atribuir uma pessoa antes do piloto.

| Alerta | Condição inicial / janela | Severidade | Procedimento |
|---|---|---|---|
| ApiUnavailable | probe/Blackbox falhando por 1 min | crítica | API indisponível |
| MonitoringStale | coleta sem sucesso há 180 s/ausente, sustentado 2 min | crítica | Coleta indisponível |
| JobsStalled | checkpoint ausente há 600 s, sustentado 2 min | aviso | Trabalhos sem progresso |
| JobDeadlineExceeded | prazo excedido por 1 min | aviso | Trabalhos sem progresso |
| WorkerFailures | ao menos 3 falhas do loop em 5 min, sustentado 1 min | crítica | Trabalhos sem progresso |
| LeaseExpired | lease expirado sem retomada por 3 min | aviso | Trabalhos sem progresso |
| DeletionOverdue | autorização pendente há 900 s, sustentado 2 min | crítica | Exclusões pendentes |
| ReservationNeedsReview | reserva pendente há 900 s, sustentado 2 min | aviso | Reservas pendentes |
| BudgetWarning/Critical/Exhausted | conta em 70%/90%/100%, sustentado 1 min | aviso/aviso/crítica | Consumo mensal |
| ProviderFailures | pelo menos 3 falhas e nenhum sucesso em 5 min, sustentado 1 min | crítica | Falhas do provedor |
| ExecutionTimeouts | ao menos 3 prazos excedidos em 5 min, sustentado 1 min | crítica | Falhas do provedor |
| ProviderSlow | p95 acima de 90 s, ao menos 20 chamadas em 15 min, sustentado 5 min | aviso | Falhas do provedor |

As notificações são agrupadas por alerta/tipo, com espera inicial de 30 s e
repetição de 4 h. Alertas de orçamento superior inibem os inferiores; API fora do
ar inibe a notificação redundante da coleta. A condição deixa de disparar quando
a série volta ao normal; a próxima coleta/exportação/scrape acrescenta atraso.

**Canal padrão: somente a interface local do Alertmanager.** Antes do piloto,
configurar um receiver real, testar entrega e resolução, atribuir responsável e
confirmar retenção/acesso. Exemplo de receiver para ensaio com um servidor HTTP
local (ajustar a rota e rede; nenhuma notificação externa é enviada pela suíte):

```yaml
receivers:
  - name: local-review
    webhook_configs:
      - url: http://receptor-privado:9095/alerts
        send_resolved: true
```

### API indisponível

Conferir probe, conectividade, processo e logs do supervisor. Conferir `/health/live`
sem enviar pedidos à IA. Restabelecer o serviço seguindo o procedimento de deploy;
não apagar banco, arquivos ou filas. Resolver após probe voltar a 1.

### Coleta indisponível

Conferir API, Collector e scrape do Prometheus, endpoint OTLP, banco e migrations.
O evento `AI_MONITORING_COLLECTION_FAILED` não inclui a exceção. Não interpretar
os dados antigos como estado atual. Resolver após timestamp de sucesso avançar.

### Trabalhos sem progresso

Conferir tipo/estado, dependências documentais, flags, cotas, prazo e leases.
Usar logs correlacionados restritos para localizar o trabalho. Retomada somente
pelos fluxos existentes; uma chamada de resultado incerto não pode ser repetida
automaticamente. Não alterar leases ou estados manualmente para silenciar o alerta.

### Exclusões pendentes

Conferir worker de exclusão e acesso ao storage, permissões e tentativas. Exclusão
já autorizada deve continuar mesmo com IA desligada. Não remover o protocolo nem
reativar a conversa. Resolver quando o expurgo ativo terminar; backups seguem P05.

### Reservas pendentes

Conferir estado, período de origem, execução e registros do provedor. Usar os
comandos auditados do P03 em [Configurar a IA](configuracao-ia.md).
Reserva `Reserved` antiga não deve ser liberada só pela idade; recuperação de órfã
exige workers drenados e evidência. Resolver após reconciliação comprovada.

### Consumo mensal

Conferir consumo versus reservas e tarifas congeladas no P03. Identificar a conta
por consulta operacional autorizada, sem criar label por tenant. Investigar antes
de alterar limite. O bloqueio financeiro existente permanece responsável por impedir
novas chamadas; edição e exportação continuam disponíveis. Resolver quando a conta
sair da faixa ou na virada UTC do mês, preservando reservas antigas para revisão.

### Falhas do provedor

Separar falha, cancelamento, espera por cota e erro de formato. Conferir modelo,
configuração, códigos dos logs e disponibilidade do provedor. Não fazer chamadas
pagas de diagnóstico nem aumentar cotas automaticamente. Resolver após recuperação
na janela observada; p95 só aciona com amostra mínima.

## Verificação sem IA real

```bash
dotnet build JuridicoAI.sln
# Usar exclusivamente PostgreSQL descartável; os provedores da suíte são simulados.
dotnet test JuridicoAI.sln

docker run --rm --network none \
  -v "$PWD/ops/monitoring:/work:ro" -w /work \
  --entrypoint /bin/promtool prom/prometheus:v3.16.0 test rules alerts.test.yaml
```

Para habilitar os testes de integração existentes definir
`JURIDICOAI_REVIEW_TEST_POSTGRES` com um servidor descartável que permita criar e
apagar os bancos exclusivos da suíte. Nenhum teste P04 chama o provedor real.

Conferir também o caminho API → OTLP → Collector → Prometheus → Grafana e
Prometheus → Alertmanager. Ensaiar indisponibilidade usando apenas instâncias
fictícias; registrar alerta ativo, resolução e comportamento com banco/Collector
indisponível. O ensaio local não aprova a infraestrutura futura de nuvem.
