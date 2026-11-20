# Configurar a IA

## Comece aqui: ativação completa em desenvolvimento

Este roteiro habilita conversa, edição e geração de minutas, histórico/memória,
anexos, busca nos documentos e análise em segundo plano. Execute na raiz do
repositório. Você precisa de **.NET 10, Docker e chave da OpenAI com acesso a um
modelo compatível com Responses, Structured Outputs e ferramentas**.

OCR (leitura de digitalizações) depende de calibração e tem uma etapa própria
abaixo. Monitoramento também é configurado separadamente.

### 1. Prepare o banco e a leitura de PDFs

Se ainda não tem `.env`, copie `.env.example` para `.env`. Preencha nele:

```dotenv
POSTGRES_DB=juridicoai
POSTGRES_USER=juridico
POSTGRES_PASSWORD=PREENCHA_SENHA_DO_BANCO
POSTGRES_PORT=5432
```

Suba o banco e restaure as dependências:

```bash
docker compose -p juridicoai up -d --wait
dotnet restore
```

Para leitura de PDFs, instale Poppler. Em Ubuntu/Debian:

```bash
sudo apt-get install poppler-utils
```

### 2. Salve a configuração da API

**Substitua todos os campos `PREENCHA_...` antes de executar.** Use a mesma senha
do banco acima. O segredo JWT deve ter ao menos 32 caracteres; pode ser gerado
com `openssl rand -base64 48`. Informe os três preços reais do modelo, em USD
por milhão de tokens, usando ponto decimal e valores positivos.

Este comando salva os valores nos user-secrets do projeto, fora do Git:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set <<'JSON'
{
  "Postgres": {
    "ConnectionString": "Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=PREENCHA_SENHA_DO_BANCO",
    "RunMigrations": true
  },
  "Auth": {
    "JwtSecret": "PREENCHA_SEGREDO_ALEATORIO_COM_PELO_MENOS_32_CARACTERES"
  },
  "AssistenteIa": {
    "Enabled": true,
    "Provider": "OpenAI",
    "BaseUrl": "https://api.openai.com/v1/",
    "ApiKey": "PREENCHA_CHAVE_OPENAI",
    "Model": "PREENCHA_ID_DO_MODELO",
    "InputCostPerMillionTokensUsd": "PREENCHA_PRECO_ENTRADA",
    "CachedInputCostPerMillionTokensUsd": "PREENCHA_PRECO_ENTRADA_CACHE",
    "OutputCostPerMillionTokensUsd": "PREENCHA_PRECO_SAIDA",
    "PricingVersion": "tabela-2026-11-19",
    "DefaultMonthlyLimitUsd": 5.00,
    "AllowSimulatedWithoutFinancialControl": false,
    "ReviewV2Enabled": true,
    "ConversationV2Enabled": true,
    "DurableGenerationEnabled": true,
    "HistoryMemoryEnabled": true
  },
  "AssistenteDocumentos": {
    "Enabled": true,
    "RetrievalEnabled": true,
    "AnalysisEnabled": true,
    "OcrEnabled": false
  },
  "DocumentStorage": {
    "RootPath": "data/documentos"
  }
}
JSON
```

**A API não carrega `.env` nem `OPENAI_API_KEY` automaticamente.** O `.env` acima
serve ao Compose; a configuração da API é a deste passo. A pasta de documentos
precisa permitir leitura, escrita e exclusão pelo processo.

### 3. Libere a organização

Se a organização ainda não existe, crie-a e anote os IDs exibidos:

```bash
PROVISION_ORGANIZATION_NAME="Escritorio de teste" \
PROVISION_OWNER_NAME="Administrador" \
PROVISION_OWNER_EMAIL="admin@example.com" \
PROVISION_OWNER_PASSWORD="PREENCHA_SENHA_DO_USUARIO" \
dotnet run --project src/JuridicoAI.Api --launch-profile http -- provision-organization
```

Se já existe, use seus IDs. O usuário informado precisa ser um **Owner ativo**
da organização. Preencha as variáveis e execute:

```bash
export IA_ORGANIZACAO_ID="PREENCHA_UUID_DA_ORGANIZACAO"
export IA_OWNER_ID="PREENCHA_UUID_DO_OWNER"

for ia_feature in AI_ASSISTANT AI_EDIT AI_REVIEW AI_DOCUMENT_GENERATION; do
  AI_FEATURE_ORGANIZATION_ID="$IA_ORGANIZACAO_ID" \
  AI_FEATURE_NAME="$ia_feature" \
  AI_FEATURE_ENABLED=true \
  AI_OPERATION_UPDATED_BY_ID="$IA_OWNER_ID" \
  AI_OPERATION_REASON="Ativacao do ambiente de desenvolvimento" \
  dotnet run --project src/JuridicoAI.Api --launch-profile http -- set-ai-feature
done

AI_ENABLED=true \
AI_OPERATION_UPDATED_BY_ID="$IA_OWNER_ID" \
AI_OPERATION_REASON="Ativacao do ambiente de desenvolvimento" \
dotnet run --project src/JuridicoAI.Api --launch-profile http -- set-ai-enabled
```

Esses comandos aplicam as migrations e salvam as permissões no banco.
O limite inicial é **US$ 5 por organização/mês UTC**. Se essa organização já tem
um limite específico no banco, ele prevalece; ajuste-o na seção seguinte.

### 4. Inicie e confira

```bash
dotnet run --project src/JuridicoAI.Api --launch-profile http
```

A API atende em `http://localhost:5299`, com Swagger em `/swagger`.
No frontend conectado a essa API:

1. Faça login na organização liberada e abra uma Peça de um Caso.
2. Abra o assistente e aceite o aviso de uso de IA.
3. Envie uma pergunta curta; depois peça uma minuta e aprove uma alteração.
4. Anexe um PDF com texto e peça uma análise para conferir o fluxo documental.

Para conferir pela API, use JWT organizacional nos endpoints abaixo:

| Endpoint | Resultado esperado |
|---|---|
| `GET /assistente/capacidades` | Assistente e capacidades v2 habilitados |
| `GET /assistente/limite-mensal` | Saldo disponível, sem estado de indisponibilidade |
| `GET /assistente/transparencia` | Aviso atual confirmado pelo usuário |

O frontend é um projeto separado e deve apontar para essa API. Em localhost,
o CORS já permite a conexão. A geração de teste usa a conta paga do provedor.

## Configurações adicionais

Os padrões abaixo já vêm do projeto. Ajuste conforme necessidade e reinicie a
API. Para salvar uma opção local, use:

```bash
dotnet user-secrets --project src/JuridicoAI.Api set "AssistenteIa:TimeoutSeconds" "120"
```

Em variáveis de ambiente, troque `:` por `__`, por exemplo
`AssistenteIa__TimeoutSeconds=120`. Variáveis do processo prevalecem sobre
user-secrets, que são carregados em `Development`. Em produção, injete a
configuração pelo deploy; user-secrets são para desenvolvimento.

### Custos e permissões

Os três preços configurados no início precisam acompanhar o modelo escolhido.
Atualize `AssistenteIa:PricingVersion` ao mudar a tabela. O projeto não consulta
preços automaticamente. Mantenha `AllowSimulatedWithoutFinancialControl=false`;
a exceção é exclusiva de testes simulados e é proibida em produção.

Para alterar o limite mensal de uma organização:

```bash
AI_LIMIT_ORGANIZATION_ID="$IA_ORGANIZACAO_ID" \
AI_LIMIT_USD=5.00 \
AI_OPERATION_UPDATED_BY_ID="$IA_OWNER_ID" \
AI_OPERATION_REASON="Ajuste do limite mensal" \
dotnet run --project src/JuridicoAI.Api --launch-profile http -- set-ai-monthly-limit
```

Cada chamada reserva saldo antes do envio. Saldo positivo pode ser insuficiente
para a reserva; consumo incerto mantém a reserva até conferência operacional.

| Permissão no banco | Libera |
|---|---|
| `AI_ASSISTANT` | Uso do assistente |
| `AI_EDIT` | Redação, propostas e aplicação de mudanças |
| `AI_DOCUMENT_GENERATION` | Minuta inicial e substituição integral; também exige `AI_EDIT` |
| `AI_REVIEW` | Modo legado de revisão |

`AssistenteIa:Enabled=false` desliga a IA pela configuração. O comando
`set-ai-enabled` controla a chave global no banco, sem reiniciar. Uma permissão
ausente bloqueia a organização; a chave global ausente é considerada ligada.

### Tamanho das respostas, prazo e volume de uso

Todas as chaves desta tabela usam o prefixo `AssistenteIa:`.

| Chave | Padrão | Controla |
|---|---:|---|
| `MaxOutputTokens` | 6000 | Tokens de saída por chamada |
| `MaxOutputCharacters` | 20000 | Caracteres de saída |
| `TimeoutSeconds` | 90 | Prazo de execução, em segundos |
| `MaxInputTokens` | 60000 | Tokens de entrada por chamada |
| `MaxTotalContextCharacters` | 80000 | Contexto total em caracteres |
| `MaxDocumentContextCharacters` | 60000 | Contexto da Peça |
| `MaxAdditionalContextCharacters` | 8000 | Contexto adicional |
| `MaxInstructionCharacters` | 4000 | Instrução do usuário |
| `MaxRequestBodyBytes` | 524288 | Corpo HTTP, exceto anexos |
| `RequestsPerUserPerMinute` | 10 | Pedidos por minuto/usuário |
| `ConcurrentRequestsPerUser` | 2 | Pedidos simultâneos/usuário |
| `RequestsPerOrganizationPerMinute` | 30 | Pedidos por minuto/organização |
| `ConcurrentRequestsPerOrganization` | 6 | Pedidos simultâneos/organização |

Todos devem ser positivos. A entrada também precisa caber no modelo escolhido.
As cotas de frequência e simultaneidade são por instância da API.

### Documentos e OCR

O roteiro inicial habilita anexos e análise com `AssistenteDocumentos:Enabled`,
`RetrievalEnabled` e `AnalysisEnabled`. Para desativar documentos, desligue as
três opções. Formatos aceitos: PDF, DOCX, TXT UTF-8, PNG e JPEG; `.doc` não é aceito.

Prefixo das opções abaixo: `AssistenteDocumentos:`.

| Chave | Padrão | Controla |
|---|---:|---|
| `MaxFileBytes` | 20971520 | Até 20 MiB por arquivo; não pode exceder esse teto |
| `MaxAttachments` | 50 | Anexos por conversa |
| `MaxConversationBytes` | 524288000 | 500 MiB por conversa |
| `MaxPages` | 100 | Páginas de PDF |
| `MaxExtractedCharacters` | 2000000 | Texto extraído |
| `MaxPixels` | 25000000 | Pixels da imagem |
| `ProcessTimeoutSeconds` | 60 | Prazo dos processos locais |
| `LeaseSeconds` | 240 | Tempo reservado ao processamento; maior que 3 × prazo local |
| `MaxAttempts` | 3 | Tentativas de processamento documental |
| `MaxRetrievalRounds` | 8 | Rodadas de busca/leitura; 1–12 |
| `MaxRetrievalTotalTokens` | 180000 | Orçamento de tokens da recuperação |
| `MaxRetrievalResultCharacters` | 12000 | Caracteres por resultado; 4000–16000 |
| `MaxAnalysisCalls` | 12 | Chamadas da análise; 3–30 |
| `MaxAnalysisTotalTokens` | 180000 | Orçamento de tokens da análise |
| `AnalysisBatchCharacters` | 24000 | Caracteres por lote; 8000–32000 |
| `AnalysisSummaryCharacters` | 8000 | Caracteres por resumo; 2000–12000 |
| `AnalysisOutputTokens` | 2000 | Saída por etapa; 500–6000 |
| `AnalysisLeaseSeconds` | 180 | Maior que `AssistenteIa:TimeoutSeconds + 30` |
| `AnalysisDeadlineSeconds` | 600 | Prazo total; pelo menos o lease |

Para ler digitalizações/imagens, instale Tesseract e os modelos português e
inglês. Depois da calibração, configure:

| Chave | Padrão | Preencher com |
|---|---|---|
| `OcrEnabled` | `false` | `true` após validar a leitura no ambiente |
| `OcrCalibrationVersion` | vazio | Identificador da calibração realizada |
| `TessdataPath` | vazio | Caminho absoluto com `por.traineddata` e `eng.traineddata` |
| `TesseractPath` | `tesseract` | Executável OCR |
| `PdfRenderPath` | `pdftoppm` | Executável Poppler para OCR |
| `PdfInfoPath` | `pdfinfo` | Executável Poppler para inspeção |
| `PdfTextPath` | `pdftotext` | Executável Poppler para extração |
| `PdfImagesPath` | `pdfimages` | Executável Poppler para detectar imagens |

A calibração OCR segue pendente no projeto. Até concluí-la, mantenha desligado:
PDFs com texto continuam funcionando, mas imagens e regiões digitalizadas ficam
sinalizadas como sem leitura confiável. Apenas preencher a versão não calibra OCR.

### Geração em segundo plano e memória

O roteiro inicial liga `AssistenteIa:ReviewV2Enabled`, `ConversationV2Enabled`,
`DurableGenerationEnabled` e `HistoryMemoryEnabled`. Revisão e conversa são
necessárias para o painel atual; geração durável e memória dependem das duas.
Os processos em segundo plano rodam dentro da API, que precisa permanecer ativa.

Prefixo das opções abaixo: `AssistenteIa:`.

| Chave | Padrão | Controla/regra |
|---|---:|---|
| `GenerationMaxSections` | 8 | Seções da minuta; 1–8 |
| `GenerationMaxCalls` | 12 | Chamadas por geração; 3–30 |
| `GenerationMaxTotalTokens` | 180000 | Tokens por geração |
| `GenerationMaxDocumentCharacters` | 120000 | Corpo montado em JSON; 20000–120000 caracteres |
| `GenerationSectionCharacters` | 20000 | JSON por seção; 2000–24000 caracteres |
| `GenerationLeaseSeconds` | 180 | Maior que `TimeoutSeconds + 30` |
| `GenerationDeadlineSeconds` | 1800 | Prazo total; pelo menos o lease |
| `HistoryCompactionMessages` | 64 | Limiar de resumo; entre `HistoryRecentMessages` e 200 |
| `HistoryRecentMessages` | 16 | Mensagens recentes; 2–100 |
| `HistoryRecentCharacters` | 16000 | Texto recente; 4000–24000 caracteres |
| `HistoryBatchCharacters` | 24000 | Lote de resumo; 4000–32000 caracteres |
| `HistoryMemoryCharacters` | 12000 | Memória; 4000–16000 caracteres |
| `HistoryToolRounds` | 8 | Rodadas de consulta do histórico; 2–12 |
| `HistoryTotalTokens` | 180000 | Tokens para memória/histórico |

Ao aumentar `TimeoutSeconds`, revise os leases de geração e análise. Por exemplo,
timeout de 150 s exige leases **maiores** que 180 s. Resumos, análise e geração
por seções usam várias chamadas e compartilham o limite financeiro.

### Aviso de transparência

| Chave | Padrão | Uso |
|---|---|---|
| `AssistenteIa:TransparencyNoticeVersion` | `ai-transparency-v1` | Versão não vazia do aviso |
| `AssistenteIa:PrivacyPolicyUrl` | `null` | URL absoluta da política de privacidade, se informada |

Conversa e documentos acrescentam sufixos à versão. Quando o aviso muda, o usuário
precisa confirmar novamente. A API retorna a versão atual em
`GET /assistente/transparencia`; o aceite usa
`POST /assistente/transparencia/confirmacoes` com `{"versao":"VERSAO_RETORNADA"}`.

### Monitoramento

É opcional para geração. Usa o prefixo `AssistenteMonitoring:` e precisa de um
collector OTLP HTTP/protobuf. Requer a entrega P04 de monitoramento, presente
nas alterações locais do workspace e ainda não incluída neste commit de
documentação.

| Chave | Padrão | Controla/regra |
|---|---|---|
| `Enabled` | `false` | Coleta e exportação de métricas |
| `OtlpEndpoint` | `http://localhost:4318/v1/metrics` | Destino HTTP/HTTPS, sem credenciais ou query na URL |
| `CollectionIntervalSeconds` | `60` | Intervalo de coleta; 10–3600 s |
| `QueryTimeoutSeconds` | `5` | Prazo da consulta; 1–30 s |
| `StalledJobSeconds` | `600` | Tempo sem progresso; mínimo 60 s |
| `PendingDeletionSeconds` | `900` | Idade de exclusão pendente; mínimo 60 s |
| `OldReservationSeconds` | `900` | Idade de reserva pendente; mínimo 60 s |

### Deploy e opções da API

| Configuração | Padrão/ação |
|---|---|
| Provedor | `AssistenteIa:Provider=OpenAI` é o único implementado |
| Endpoint | `AssistenteIa:BaseUrl=https://api.openai.com/v1/`; precisa suportar `responses`, `responses/input_tokens`, JSON Schema estrito, SSE e ferramentas |
| Banco | `Postgres:RunMigrations=true` aplica migrations; se desligado, aplicar por outro processo |
| Arquivos | `DocumentStorage:RootPath=data/documentos`; relativo à pasta da API, deve ser persistente e gravável |
| Upload geral | `DocumentStorage:MaxFileSizeBytes=20971520`; conferir também limites do proxy e do assistente |
| Formatos do Caso | `DocumentStorage:AllowedExtensions` e `AllowedMimeTypes`; padrões em `appsettings.json` |
| JWT | `Auth:Issuer=JuridicoAI`, `Audience=JuridicoAI.Web`, `AccessTokenTtlMinutes=15`, `RefreshTokenTtlDays=30` |
| Cookie organizacional | `Auth:SecureRefreshCookie=true` no perfil Production; `false` impede a inicialização nesse ambiente. Padrão local continua `false` |
| Origem do frontend | `Cors:AllowedOrigins`: lista de origens HTTPS exatas obrigatória em Production, compartilhada com a sessão pessoal; localhost automático apenas em Development |
| Proxy | `ReverseProxy:KnownProxies`/`KnownNetworks`: IPs ou CIDRs reais da hospedagem; listas vazias desativam o encaminhamento. `ForwardLimit=1` por padrão |
| Histórico pessoal | Cookie de refresh sempre `Secure` e `SameSite=Strict`; exige HTTPS/mesmo site no deploy |

Aplique todas as migrations. Em várias instâncias, compartilhe o acesso aos
arquivos dos jobs; os temporários usam `Path.GetTempPath()/juridicoai-extract`.
O proxy precisa permitir SSE. Acesso externo ao PostgreSQL e saída HTTPS para a
OpenAI devem estar disponíveis. A janela de contexto e o acesso ao modelo são
condições da conta do provedor.

Prompts, schemas, intervalos dos workers e exportação de métricas são definidos
no código. Não há opção de temperatura, modelo alternativo, pesquisa externa ou
retry pago automático. O fluxo atual não exige embeddings nem banco vetorial.

### Se não funcionar

| Sintoma | Confira primeiro |
|---|---|
| API não inicia | JWT, placeholders não substituídos, limites e dependências das flags |
| Painel sem envio | `Enabled`, revisão/conversa v2 e `AI_ASSISTANT` |
| `AI_FEATURE_NOT_ENABLED` | Permissões da organização no passo 3 |
| `AI_LIMIT_CONFIGURATION_INVALID` | Três tarifas positivas, modelo e versão de preços |
| `AI_MONTHLY_LIMIT_REACHED` / `AI_MONTHLY_LIMIT_INSUFFICIENT` | Limite da organização, consumo e reservas |
| `AI_TRANSPARENCY_ACK_REQUIRED` | Aceite do aviso atual |
| `AI_PROVIDER_ERROR` / `AI_UNAVAILABLE` | Credencial, acesso ao modelo, contrato do endpoint, rede e chave global |
| Anexo parado/sem texto | Poppler, permissões da pasta, processo da API e OCR |

`GET /health/live` confirma apenas que a API responde. A confirmação do fluxo
completo é a interação e a análise documental do passo 4.

Para reservas incertas, use `reconcile-ai-reservation` com `AI_RECONCILE_ACTION`.
Para órfãs, use `recover-orphan-ai-reservation` com **`AI_ORPHAN_ACTION`** e
confirmação de workers drenados. O [procedimento financeiro](../../implementacoes/features/assistente-conversacional/evidencias/p03-limite-mensal-2026-11-18.md)
detalha a reconciliação; os exemplos antigos de órfãs usam o nome incorreto da
variável. Alterações administrativas exigem operador e justificativa auditados.

Referência: [validações e comandos](../../src/JuridicoAI.Api/Program.cs),
[configuração base](../../src/JuridicoAI.Api/appsettings.json) e
[contratos HTTP](../../implementacoes/features/assistente-conversacional/especificacoes/07-contratos-implementados.md).
Levantamento do código local em 19/11/2026; valores efetivos de segredos/banco e
chamadas ao provedor não foram verificados.
