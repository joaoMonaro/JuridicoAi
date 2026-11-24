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
assistente. Não versione segredos; consulte [Configuração e execução](docs/configuracao-e-execucao.md).

As migrations do Entity Framework Core são aplicadas na inicialização quando
`Postgres:RunMigrations=true`.

Para aplicar migrations separadamente, use `dotnet run --project
src/JuridicoAI.Api -- migrate-database` com a conexão do ambiente configurada.
O comando aplica as migrations e encerra sem iniciar o servidor.

O [ensaio local de publicação](ops/rehearsal/README.md) compila e executa frontend,
API e dependências em containers isolados, com dados fictícios e IA simulada,
e verifica persistência, backup e restauração antes da publicação em nuvem.

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

- [Índice da documentação](docs/README.md) — visão geral do que está implementado
- [Escopo e critérios do MVP](MVP.md)
- [TODO geral do projeto](TODO.md)
- [Arquitetura e fluxos](docs/arquitetura.md)
- [Configuração, segredos e execução](docs/configuracao-e-execucao.md)
- [API HTTP](docs/backend/api.md)
- [Autenticação e multitenancy](docs/backend/autenticacao-e-multitenancy.md)
- [Peças, templates e exportação](docs/backend/pecas.md)
- [Assistente de IA](docs/backend/assistente-ia.md)
- [Configurar a IA](docs/operacao/configuracao-ia.md)
- [Monitoramento do assistente](docs/operacao/monitoramento-assistente.md)
- [Versionamento e branches](docs/versionamento.md)
- [Preparação para produção e pendências abertas](implementacoes/producao-e-pendencias.md)

`docs/` descreve o comportamento atual; `implementacoes/` reúne pendências de
produção e checklists manuais; `avaliacoes/` guarda as suítes de avaliação da IA;
`ops/` contém monitoramento local e ensaio de publicação.
