# Monitoramento P04

Para iniciar e acessar o painel, siga o [guia rápido](../../README.md#abrir-o-painel-de-monitoramento).

Configuração, métricas, limiares e resposta:
[procedimento operacional](../../docs/operacao/monitoramento-assistente.md).

Os serviços usam uma rede privada e publicam administração/OTLP somente em loopback.
Definir `MONITORING_GRAFANA_PASSWORD` e executar com nome de projeto explícito:

```bash
docker compose -p juridicoai-monitoring -f ops/monitoring/compose.yaml up -d
```

Ajustar a origem da probe em `prometheus.yaml` e habilitar `AssistenteMonitoring`
na API. O receiver padrão permite revisão local, sem enviar notificações externas.
Validar canal real, responsável, acesso e retenção antes do piloto.
