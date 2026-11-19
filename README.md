# JuridicoAI

Backend do JuridicoAI: uma API ASP.NET Core para autenticação e multitenancy,
organização de Casos, edição persistente de Peças e assistência jurídica por IA.

## Componentes principais

```text
Frontend -> API .NET -> PostgreSQL
                   `-> OpenAI Responses API
```

- PostgreSQL armazena identidade, organizações, Casos, Peças e governança da IA;
- o assistente gera respostas em SSE e nunca altera uma Peça sem aplicação
  explícita no frontend.

O scraper do TJSP foi separado em um projeto independente em
`../JuridicoAI.Scrapper`. Ele não integra a solução nem é necessário para
compilar ou executar esta API.

## Início rápido

Configure `.env` para o PostgreSQL, suba o serviço e defina os segredos locais:

```bash
cp .env.example .env
docker compose up -d

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Postgres:ConnectionString" \
  "Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=..."

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Auth:JwtSecret" \
  "um-segredo-aleatorio-com-pelo-menos-32-caracteres"

dotnet run --project src/JuridicoAI.Api
```

A chave e o modelo da OpenAI são necessários somente para executar o
assistente. Não versione segredos; consulte [User Secrets](docs/user-secrets.md).

As migrations do Entity Framework Core são aplicadas na inicialização quando
`Postgres:RunMigrations=true`.

## Abrir o painel de monitoramento

Com Docker em execução, rode na raiz do projeto:

```bash
export MONITORING_GRAFANA_PASSWORD='sua-senha'
docker compose -p juridicoai-monitoring -f ops/monitoring/compose.yaml up -d
```

Em outro terminal, inicie ou reinicie a API já configurada com:

```bash
AssistenteMonitoring__Enabled=true dotnet run --project src/JuridicoAI.Api
```

Abra [o painel Grafana](http://localhost:3001/d/juridicoai-assistente).
Usuário: **admin**. Senha: a definida acima na primeira inicialização.
Os primeiros dados aparecem em aproximadamente um minuto.

Para parar o painel, preservando seus dados:

```bash
docker compose -p juridicoai-monitoring -f ops/monitoring/compose.yaml down
```

## Documentação

- [Versionamento — produção, homologação e fluxo de mudanças](docs/versionamento.md)
- [TODO geral do projeto](TODO.md)
- [Decisões do MVP](docs/decisoes-mvp.md)
- [Preparação para produção e testes locais — planejamento](implementacoes/features/preparacao-producao/README.md)
- [Índice da documentação atual](docs/README.md)
- [Configuração e execução](docs/configuracao-e-execucao.md)
- [Monitoramento operacional do assistente](docs/operacao/monitoramento-assistente.md)
- [API HTTP](docs/backend/api.md)
- [Assistente de IA](docs/backend/assistente-ia.md)
- [Estado atual do assistente conversacional](implementacoes/features/assistente-conversacional/estado-atual.md)
- [TODO da feature](implementacoes/features/assistente-conversacional/TODO.md)
- [Autenticação e multitenancy](docs/backend/autenticacao-e-multitenancy.md)
- [Peças e templates](docs/backend/pecas.md)
- [Implementações em andamento](implementacoes/README.md)
- [Geração de minutas — plano anterior incorporado](implementacoes/features/geracao-minutas-ia/README.md)

A documentação de `docs/` descreve o comportamento atual. Especificações,
decisões e pendências ficam em `implementacoes/`, separadas entre a feature
ativa em `features/`, entregas registradas em `alreadyDone/` e trabalho em espera
em `standBy/`. As suítes de avaliação da IA ficam em `avaliacoes/`.
