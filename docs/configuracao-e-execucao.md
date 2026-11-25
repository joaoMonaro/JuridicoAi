# Configuração e execução

## Pré-requisitos

- SDK do .NET 10;
- PostgreSQL 17 ou compatível.

Para exportar PDF, configure também o conversor local conforme
[Configuração da exportação Word e PDF](operacao/exportacao-word-pdf.md).

## Restaurar e compilar

Na raiz do repositório:

```bash
dotnet restore
dotnet build
```

## PostgreSQL com Docker Compose

O Compose cria o PostgreSQL e mantém os dados no volume `postgres_data`.

Crie o arquivo local `.env` a partir do modelo versionado:

```bash
cp .env.example .env
```

Edite o `.env` e defina:

```dotenv
COMPOSE_PROJECT_NAME=juridicoai
POSTGRES_DB=juridicoai
POSTGRES_USER=juridico
POSTGRES_PASSWORD=defina-uma-senha-local
```

Se `COMPOSE_PROJECT_NAME` estiver exportado globalmente no terminal, ele tem
prioridade sobre o valor do `.env`. Remova-o antes de iniciar o projeto:

```bash
unset COMPOSE_PROJECT_NAME
```

Em seguida, inicie o banco:

```bash
docker compose up -d
```

O nome `COMPOSE_PROJECT_NAME=juridicoai` isola os containers, redes e volumes
deste repositório dos demais projetos Compose da máquina.

Evite `docker compose down -v` quando quiser preservar os dados. A opção `-v`
remove também o volume do PostgreSQL. A extração do scraper não exige
remover containers ou volumes MongoDB existentes.

## Configurações da API

As chaves podem ser definidas em `appsettings*.json`, variáveis de ambiente ou
user-secrets. Em variáveis de ambiente, use `__` no lugar de `:`.

Para aplicar migrations em uma etapa controlada, configure a conexão e execute
`dotnet run --project src/JuridicoAI.Api -- migrate-database`. O comando encerra
sem iniciar o servidor. No runtime publicado, use `Postgres__RunMigrations=false`.
O [ensaio isolado de publicação](../ops/rehearsal/README.md) exercita esse fluxo
com PostgreSQL e storage exclusivos, sem alterar o Compose de desenvolvimento.

| Chave | Padrão | Finalidade |
|---|---:|---|
| `Postgres:ConnectionString` | vazio | conexão obrigatória para operações de casos |
| `Postgres:RunMigrations` | `true` | aplica migrations pendentes do EF Core ao iniciar |
| `Auth:JwtSecret` | vazio | segredo JWT obrigatório, com ao menos 32 caracteres |
| `Auth:RegistrationEnabled` | `false` (`true` em Development) | habilita cadastro público; manter `false` no piloto e provisionar o participante por CLI |
| `Auth:Issuer` | `JuridicoAI` | emissor dos access tokens |
| `Auth:Audience` | `JuridicoAI.Web` | audiência dos access tokens |
| `Auth:AccessTokenTtlMinutes` | `15` | duração do access token |
| `Auth:RefreshTokenTtlDays` | `30` | duração máxima do refresh token |
| `Auth:SecureRefreshCookie` | `false` (`true` em Production) | cookie de refresh enviado somente por HTTPS; desligar em Production impede a inicialização |
| `ReverseProxy:KnownProxies` | lista vazia | IPs específicos dos proxies confiáveis; nenhuma confiança automática em loopback |
| `ReverseProxy:KnownNetworks` | lista vazia | redes CIDR confiáveis, IPv4 ou IPv6; redes universais `/0` são rejeitadas |
| `ReverseProxy:ForwardLimit` | `1` | máximo de saltos processados da direita para a esquerda; deve ser maior que zero |
| `DocumentStorage:RootPath` | `data/documentos` | diretório do storage local |
| `DocumentStorage:MaxFileSizeBytes` | `20971520` | limite por upload |
| `DocumentStorage:EncryptionKeyBase64` | vazio | chave AES-256-GCM em Base64 (32 bytes) para criptografia em repouso; **obrigatória em Production** |
| `DataRetention:Enabled` | `true` | ativa o expurgo automático periódico (sessões, soft-deletes e auditoria) |
| `DataRetention:IntervalHours` | `6` | intervalo em horas entre execuções do worker de retenção (`1` a `168`) |
| `DataRetention:SoftDeleteRetentionDays` | `30` | prazo em dias antes do expurgo físico de Peças e Templates excluídos |
| `DataRetention:ExpiredSessionRetentionDays` | `30` | prazo em dias antes do expurgo de sessões expiradas/revogadas |
| `DataRetention:AuditLogRetentionDays` | `365` | prazo em dias de retenção dos registros em `data_access_audit_logs` |
| `AssistenteIa:Enabled` | `false` | trava de configuração superior do assistente |
| `AssistenteIa:ReviewV2Enabled` | `false` | revisão persistida e aprovação transacional |
| `AssistenteIa:ConversationV2Enabled` | `false` | histórico persistido no contexto |
| `AssistenteIa:DurableGenerationEnabled` | `false` | geração do chat v2 em worker com checkpoints |
| `AssistenteIa:HistoryMemoryEnabled` | `false` | memória versionada e recuperação do histórico |
| `AssistenteDocumentos:Enabled` | `false` | extração nativa e contexto documental |
| `AssistenteDocumentos:RetrievalEnabled` | `false` | ferramentas documentais com cobertura |
| `AssistenteDocumentos:AnalysisEnabled` | `false` | análise extensa em segundo plano |
| `AssistenteDocumentos:OcrEnabled` | `false` | calibração em espera; manter desativado |
| `AssistenteIa:Provider` | `OpenAI` | provider selecionado |
| `AssistenteIa:Model` | vazio | modelo enviado ao provider |
| `AssistenteIa:ApiKey` | vazio | chave sensível do provider |
| `AssistenteIa:MaxInputTokens` | `60000` | teto de entrada após montagem do contexto |
| `AssistenteIa:MaxRequestBodyBytes` | `524288` | limite do corpo HTTP do assistente |
| `AssistenteIa:RequestsPerUserPerMinute` | `10` | limite por usuário |
| `AssistenteIa:ConcurrentRequestsPerUser` | `2` | concorrência por usuário |
| `AssistenteIa:RequestsPerOrganizationPerMinute` | `30` | limite agregado por Organization |
| `AssistenteIa:ConcurrentRequestsPerOrganization` | `6` | concorrência por Organization |
| `AssistenteIa:DefaultMonthlyLimitUsd` | `5.0` | limite financeiro mensal inicial por organização; mês calendário UTC |
| `AssistenteIa:InputCostPerMillionTokensUsd` | `0` | tarifa de entrada sem cache; informar preço real do modelo |
| `AssistenteIa:CachedInputCostPerMillionTokensUsd` | `0` | tarifa de leitura do cache por milhão de tokens |
| `AssistenteIa:CacheWriteCostPerMillionTokensUsd` | `0` | tarifa de gravação no cache de 30 minutos por milhão de tokens, obrigatória para o `gpt-6.1-terra` |
| `AssistenteIa:OutputCostPerMillionTokensUsd` | `0` | tarifa de saída; informar preço real do modelo |
| `AssistenteIa:PricingVersion` | `unconfigured` | identificação da tabela de preços usada nas reservas |
| `AssistenteIa:AllowSimulatedWithoutFinancialControl` | `false` | opt-out explícito para testes simulados; proibido em produção |
| `AssistenteIa:PrivacyPolicyUrl` | vazio | URL absoluta da Política de Privacidade exibida no aviso de transparência; **obrigatória em Production** quando `AssistenteIa:Enabled=true` |
| `AssistenteIa:PiiPseudonymizationEnabled` | `true` | ativa pseudonimização reversível de CPF, CNPJ, RG, e-mail, telefone e CEP antes do envio à IA |
| `Cors:AllowedOrigins` | lista vazia | origens exatas do frontend, compartilhadas com a sessão pessoal; produção exige lista não vazia, HTTPS e host não local |

### Configuração mínima LGPD em Produção (VPS)

Em `ASPNETCORE_ENVIRONMENT=Production`, a API bloqueia a inicialização se as duas variáveis abaixo não estiverem definidas:

```bash
# 1. Gere a chave AES-256-GCM (32 bytes em Base64) e guarde fora da pasta de backup de data/documentos
openssl rand -base64 32

# 2. Exporte no serviço/ambiente da VPS
export DocumentStorage__EncryptionKeyBase64="<SAIDA_DO_OPENSSL>"
export AssistenteIa__PrivacyPolicyUrl="https://seu-dominio.com.br/privacidade"
```

- `AssistenteIa__PiiPseudonymizationEnabled=true` e `DataRetention__Enabled=true` já vêm ativos por padrão.
- Aplique as migrations (`migrate-database`) para criar a tabela `data_access_audit_logs`.
- Em `Development`, `DocumentStorage:EncryptionKeyBase64` e `AssistenteIa:PrivacyPolicyUrl` são opcionais.

Não versione chaves, senhas ou connection strings reais. Para desenvolvimento,
o projeto da API possui `UserSecretsId` e carrega automaticamente o Secret
Manager do .NET quando o ambiente é `Development`.

O guia completo, incluindo chave da OpenAI, flags por Organization e kill
switch, está em [User Secrets no desenvolvimento](user-secrets.md).

Chamadas reais à IA exigem tarifas válidas e versão de preços configuradas,
além de modelo, chave e permissões. Os zeros versionados indicam configuração
pendente e não habilitam chamadas gratuitas. O limite de US$ 5,00 é a configuração
inicial do piloto; a interface apresenta somente percentuais. As migrations
financeiras precisam ser aplicadas junto às demais migrations da aplicação.
A alteração do limite por organização e a reconciliação de reservas usam
[operações CLI auditadas](../implementacoes/features/assistente-conversacional/evidencias/p03-limite-mensal-2026-11-18.md#operação-reconciliação-e-recuperação-de-reservas-órfãs).

Configure a connection string com o mesmo usuário e senha definidos no `.env`:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "Postgres:ConnectionString" \
  "Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=defina-uma-senha-local"

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Auth:JwtSecret" \
  "um-segredo-aleatorio-com-pelo-menos-32-caracteres"
```

Para listar o que está configurado:

```bash
dotnet user-secrets --project src/JuridicoAI.Api list
```

Esse comando mostra os valores completos. Não compartilhe sua saída. Os
user-secrets ficam fora do repositório, mas não são criptografados e devem ser
usados somente em desenvolvimento.

Variáveis exportadas no terminal têm prioridade sobre user-secrets. A conversão
de nomes usa dois sublinhados:

```text
Postgres:ConnectionString -> Postgres__ConnectionString
```

## Executar a API

```bash
dotnet run --project src/JuridicoAI.Api/JuridicoAI.Api.csproj
```

O perfil HTTP de desenvolvimento usa `http://localhost:5299`. O endpoint raiz
`GET /` pode ser usado como verificação básica e retorna `Hello World!`.

Em Production, a API recusa HTTP em todas as rotas com status 400 e código
`HTTPS_REQUIRED`. O certificado/domínio será configurado na publicação. Se o
HTTPS terminar em proxy, configure os IPs ou redes confiáveis em `ReverseProxy`.
O middleware de encaminhamento já é executado antes da validação de HTTPS,
autenticação e rate limit.
Segredos próprios do ambiente também serão definidos e injetados na publicação.

O encaminhamento fica desativado quando `KnownProxies` e `KnownNetworks` estão
vazios. Com origem confiável, somente `X-Forwarded-For` e `X-Forwarded-Proto`
são processados; devem ter a mesma quantidade de entradas. Cabeçalhos de
origens desconhecidas são ignorados. `X-Forwarded-Host` não altera o host da API.
Configurações com IP/rede inválidos, rede `/0` ou limite menor que 1 impedem a
inicialização. Exemplo ilustrativo para um único proxy:

```bash
export ReverseProxy__KnownProxies__0="10.0.0.10"
export ReverseProxy__ForwardLimit=1
```

O IP acima deve ser substituído pelo endereço do proxy que se conecta à API.
Se a hospedagem usar uma rede de proxies dinâmica, configure o CIDR informado
por ela em `ReverseProxy__KnownNetworks__0`. Para uma cadeia com mais de um proxy,
declare os intermediários confiáveis e ajuste `ForwardLimit` à topologia real.
O proxy deve controlar os cabeçalhos que encaminha; restrinja o acesso ao
backend conforme a arquitetura publicada. A autenticação mantém a cota de 10
requisições/minuto por IP original, por instância. Os endereços reais e a
verificação no ambiente publicado pertencem ao deploy já previsto.

Em Development, origens HTTP/HTTPS de loopback (`localhost`, `127.0.0.1` e
`[::1]`) continuam permitidas em qualquer porta. Em Production, somente a lista
explícita `Cors:AllowedOrigins` é aceita; lista vazia, HTTP, localhost, curingas,
caminho, barra final, credenciais, query ou fragmento impedem a inicialização.
A origem deve incluir esquema, host e porta quando necessária, no formato
canônico enviado pelo navegador. Exemplo de configuração no deploy:

```bash
export Cors__AllowedOrigins__0="https://app.seu-dominio.com.br"
```

Uma única lista governa o CORS e a verificação de Origin da sessão pessoal.
A antiga chave `Auth:PersonalHistoryAllowedOrigins` foi substituída por
`Cors:AllowedOrigins`. Os cookies `SameSite=Strict` continuam exigindo que
frontend e API pertençam ao mesmo site.

## Configuração mínima por funcionalidade

| Funcionalidade | Requisitos |
|---|---|
| Fundação do caso, autenticação e Peças | PostgreSQL, segredo JWT e diretório de storage gravável |
| Assistente de IA | requisitos de Peças, chave/modelo do provider, configuração habilitada, kill switch e flags da Organization |

## Sequência recomendada

1. Execute `cp .env.example .env` e altere a senha.
2. Remova um eventual projeto global com `unset COMPOSE_PROJECT_NAME`.
3. Inicie o PostgreSQL com `docker compose up -d`.
4. Configure `Postgres:ConnectionString` e `Auth:JwtSecret` nos user-secrets.
5. Execute `dotnet restore` e `dotnet build`.
6. Inicie a API; as migrations do EF Core serão aplicadas antes de a aplicação
   começar a atender requisições.
7. Valide `GET http://localhost:5299/`.

Para usar a IA, configure separadamente o provider e libere apenas a
Organization desejada conforme [User Secrets](user-secrets.md). A configuração
local não substitui o aviso de transparência apresentado no primeiro uso.


## Ativação do assistente conversacional

Siga [Configurar a IA](operacao/configuracao-ia.md) para ativar o fluxo completo
em desenvolvimento. O guia começa pelos comandos de configuração e termina
com os ajustes opcionais.

Configurar chave/modelo e a flag geral não ativa todos os recortes v2. Capacidades
combinam flags técnicas, kill switch e permissões da organização. Dependências,
limites atuais e rotas estão em
[contratos implementados](../implementacoes/features/assistente-conversacional/especificacoes/07-contratos-implementados.md).
Histórico pessoal e exclusões aceitas continuam disponíveis sem geração habilitada;
exigem as migrations correspondentes e conta/sessão pessoal válidas.

GPT-6.1 Terra foi usado na avaliação reduzida, sem configurar um modelo default no
produto. A chave de avaliação foi temporária e não foi persistida. OCR continua em
espera. Não confundir ativação local com a liberação do piloto, cujas pendências
financeiras, de acesso, backup e operação estão no
[TODO da feature](../implementacoes/features/assistente-conversacional/TODO.md).

## Monitoramento do assistente

`AssistenteMonitoring:Enabled=false` por padrão. Habilitar coleta periódica e
exportação OTLP/HTTP com `AssistenteMonitoring__Enabled=true`; endpoint padrão
`http://localhost:4318/v1/metrics`. O monitoramento não habilita IA nem altera os
limites do P03. O [procedimento P04](operacao/monitoramento-assistente.md) documenta
as opções, os serviços locais privados, o painel, os alertas e as pendências de deploy.
`GET /health/live` verifica somente o processo HTTP; readiness completa segue pendente.
