# User Secrets no desenvolvimento

O Secret Manager do .NET permite armazenar configurações sensíveis usadas em
desenvolvimento fora do repositório. Ele é adequado para segredos JWT, chaves
de API e senhas de bancos locais.

Os valores persistem após fechar o terminal e podem ser carregados tanto pelo
`dotnet run` quanto pelo debug do Rider. Diferentemente de um `export`, eles não
ficam limitados à sessão atual do shell.

## Como o projeto está habilitado

O projeto `src/JuridicoAI.Api/JuridicoAI.Api.csproj` contém um
`UserSecretsId`. Esse identificador associa o projeto ao armazenamento local do
Secret Manager:

```xml
<UserSecretsId>3eda566e-4d29-4fb5-84e9-02e8c96a7e01</UserSecretsId>
```

O ASP.NET Core carrega esses valores automaticamente quando a aplicação é
executada no ambiente `Development`. Os perfis `http` e `https` do projeto já
definem esse ambiente no `launchSettings.json`.

## Configurar o segredo JWT

Execute na raiz do repositório:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "Auth:JwtSecret" \
  "um-segredo-aleatorio-com-pelo-menos-32-caracteres"
```

Para gerar um valor aleatório em vez de escrever um manualmente:

```bash
openssl rand -base64 48
```

Copie o resultado e use-o como valor de `Auth:JwtSecret`. Não coloque o segredo
real em arquivos versionados, exemplos, commits ou mensagens.

Depois da configuração, inicie normalmente:

```bash
dotnet run --project src/JuridicoAI.Api
```

No Rider, use o perfil de execução `JuridicoAI.Api: http` ou
`JuridicoAI.Api: https`. Como o valor está persistido no Secret Manager, não é
necessário adicionar a variável manualmente à configuração de debug.

## Outras configurações

Chaves hierárquicas usam `:` no comando do Secret Manager:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "Postgres:ConnectionString" \
  "Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=senha-local"

dotnet user-secrets --project src/JuridicoAI.Api set \
  "Mongo:ConnectionString" \
  "mongodb://juridico:senha-local@localhost:27017/?authSource=admin"
```

### Assistente de IA

O assistente permanece desabilitado por padrão. Para usar o provider OpenAI no
ambiente de desenvolvimento, configure a chave fora do repositório, escolha um
modelo compatível com Responses e Structured Outputs e só então habilite a
feature:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set \
  "AssistenteIa:ApiKey" \
  "sua-chave-da-api"

dotnet user-secrets --project src/JuridicoAI.Api set \
  "AssistenteIa:Model" \
  "modelo-escolhido"

dotnet user-secrets --project src/JuridicoAI.Api set \
  "AssistenteIa:Enabled" \
  "true"
```

A inicialização falha de forma explícita se o assistente estiver habilitado sem
chave, modelo ou com limites inválidos. A chave nunca deve ser colocada em
`appsettings*.json`, `.env`, commits, logs ou exemplos compartilhados.

O kill switch global não libera um tenant sozinho. Para habilitar uma flag em
uma Organization, execute o comando operacional com acesso à mesma configuração
de PostgreSQL da API:

```bash
AI_FEATURE_ORGANIZATION_ID="uuid-da-organizacao" \
AI_FEATURE_NAME="AI_ASSISTANT" \
AI_FEATURE_ENABLED="true" \
AI_OPERATION_UPDATED_BY_ID="uuid-do-owner" \
AI_OPERATION_REASON="Liberação para tenant interno" \
dotnet run --project src/JuridicoAI.Api -- set-ai-feature
```

Repita explicitamente para `AI_EDIT`, `AI_REVIEW` ou
`AI_DOCUMENT_GENERATION`, conforme o estágio do rollout. Ausência de registro
equivale a desabilitado. Esse comando é administrativo e não recebe conteúdo
jurídico; até existir uma interface autenticada, sua execução deve ficar
restrita ao ambiente operacional controlado. O operador informado precisa ser
um `Owner` ativo da Organization e a justificativa deve ter entre 1 e 500 caracteres.
Cada alteração gera um registro imutável em `ai_feature_change_audit`.

Com `AssistenteIa:Enabled=true`, o kill switch persistido pode bloquear e
restaurar novas operações sem reiniciar a API:

```bash
AI_ENABLED="false" \
AI_OPERATION_UPDATED_BY_ID="uuid-do-operador" \
AI_OPERATION_REASON="Interrupção preventiva durante investigação" \
dotnet run --project src/JuridicoAI.Api -- set-ai-enabled
```

Para o rollback, execute o mesmo comando com `AI_ENABLED="true"` e uma nova
justificativa. O operador global precisa ser um usuário ativo. A justificativa
é dado técnico de auditoria: não inclua nomes de partes, conteúdo de Peças ou
outros dados do Caso.

`AssistenteIa:Enabled=false` continua sendo a trava superior de configuração e
não pode ser revertida pelo banco. Para que o comando operacional possa
habilitar a IA, essa configuração de bootstrap precisa estar em `true`, com
provider, modelo e chave válidos.

## Consultar, remover e limpar

Listar todos os valores configurados:

```bash
dotnet user-secrets --project src/JuridicoAI.Api list
```

Esse comando exibe os valores completos. Evite compartilhar ou registrar sua
saída em logs.

Remover apenas uma configuração:

```bash
dotnet user-secrets --project src/JuridicoAI.Api remove "Auth:JwtSecret"
```

Remover todas as configurações locais associadas ao projeto:

```bash
dotnet user-secrets --project src/JuridicoAI.Api clear
```

## Precedência e variáveis de ambiente

Uma variável de ambiente definida no processo sobrescreve o valor equivalente
dos user-secrets. Em variáveis de ambiente, substitua `:` por `__`:

```text
Auth:JwtSecret             -> Auth__JwtSecret
Postgres:ConnectionString  -> Postgres__ConnectionString
AssistenteIa:ApiKey        -> AssistenteIa__ApiKey
```

Por exemplo:

```bash
export Auth__JwtSecret="valor-temporario-com-pelo-menos-32-caracteres"
dotnet run --project src/JuridicoAI.Api
```

Nesse caso, `valor-temporario...` é usado no lugar do valor persistido. O
`export` existe somente no shell atual e nos processos iniciados por ele. Um
Rider aberto pelo menu do sistema não herda essa variável.

## Limitações de segurança

User-secrets são voltados exclusivamente ao desenvolvimento:

- ficam fora do repositório e não são enviados ao Git;
- são persistentes na conta local do sistema operacional;
- não são necessariamente criptografados;
- não substituem um gerenciador de segredos de produção.

Em produção, use o mecanismo de configuração segura da infraestrutura, como um
cofre de segredos ou variáveis de ambiente fornecidas durante o deploy.
