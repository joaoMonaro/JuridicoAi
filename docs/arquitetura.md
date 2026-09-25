# Arquitetura e fluxos

## Componentes

```text
Cliente HTTP
    |
    |---- JWT/AuthContext ----> Session, User, Membership, Organization
    v
JuridicoAI.Api
    |---- ingestão ------> JuridicoAI.Scrapper ------> e-SAJ/TJSP
    |                          |
    |                          +--> Playwright (sessão e reCAPTCHA)
    |                          +--> HttpClient (busca e paginação)
    |
    |---- jurisprudências ---------------------------> MongoDB
    |---- identidade, casos e peças (EF Core) --------> PostgreSQL
```

O projeto `JuridicoAI.Api` referencia `JuridicoAI.Scrapper` diretamente. Assim,
o mesmo `TjspScraperService` é usado tanto pelo executável de linha de comando
quanto pelo endpoint de ingestão.

## Organização da solução

```text
JuridicoAI.sln
src/
├── JuridicoAI.Api/
│   ├── Program.cs
│   └── src/
│       ├── Application/       DTOs e casos de uso
│       ├── Controllers/       endpoints HTTP
│       ├── Domain/            entidades persistidas
│       └── Infrastructure/    EF Core/PostgreSQL, MongoDB e configurações
└── JuridicoAI.Scrapper/
    ├── Program.cs
    └── src/
        ├── Cli/               argumentos, execução e saída JSON
        ├── Models/            contratos da busca
        ├── Parsing/           leitura do HTML do TJSP
        └── Services/          sessão, HTTP, payload e orquestração
```

## Fluxo de ingestão

```text
POST /api/jurisprudencias/ingest
    -> coleta no TJSP
    -> gravação idempotente em jurisprudencias_raw
    -> normalização de texto e datas
    -> classificação local por palavras-chave
    -> upsert em jurisprudencias
    -> atualização do raw para status "processado"
```

Cada item é tratado isoladamente. Uma falha de normalização, classificação ou
persistência incrementa o contador `falhas`, mas não interrompe os demais itens.
Uma falha na coleta, anterior ao laço, encerra a requisição. Timeout específico
do scraper é convertido pela API em HTTP 504.

## Ciclos de vida e dependências

A API registra serviços de aplicação e repositórios como `scoped`. O scraper
cria uma sessão Playwright e um `HttpClient` próprios para cada pesquisa.

Os índices do MongoDB são criados sob demanda no início das operações de
ingestão e reprocessamento, não durante a inicialização da API.

A fundação do caso usa um `DbContext` scoped e repositórios implementados com
Entity Framework Core. O modelo relacional é configurado explicitamente pela
Fluent API. Na inicialização, o serviço de migrations cria um escopo próprio e
executa `Database.MigrateAsync()` quando `Postgres:RunMigrations` está ativo.

Casos, Peças e Templates são dados privados filtrados por `organizationId`.
Jurisprudências permanecem globais e compartilhadas. O middleware JWT valida a
Session antes de construir o `AuthContext` usado pelos serviços e repositórios.

## Fluxo de Peças

```text
POST /casos/{casoId}/pecas
    -> valida Caso e template opcional
    -> copia conteúdo e configurações do template ou aplica defaults
    -> grava documento contínuo em JSONB com status Rascunho e versão 1

PATCH /pecas/{pecaId}
    -> valida JSONs e versão recebida
    -> UPDATE condicionado por id + versão + ausência de soft delete
    -> incrementa versão atomicamente ou retorna HTTP 409
```

Templates são origens copiáveis, não vínculos dinâmicos. Alterações posteriores
no template não propagam para Peças existentes. A listagem por Caso projeta
somente metadados para não carregar documentos TipTap potencialmente grandes.
