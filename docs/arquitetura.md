# Arquitetura e fluxos

## Componentes

```text
Cliente HTTP
    |
    v
JuridicoAI.Api
    |---- ingestão ------> JuridicoAI.Scrapper ------> e-SAJ/TJSP
    |                          |
    |                          +--> Playwright (sessão e reCAPTCHA)
    |                          +--> HttpClient (busca e paginação)
    |
    |---- jurisprudências ---------------------------> MongoDB
    |---- casos, peças e templates (EF Core) ---------> PostgreSQL
    |
    +---- geração de petição ------------------------> OpenAI Responses API
                         ^
                         |
                 ementas do MongoDB
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
│       └── Infrastructure/    EF Core/PostgreSQL, MongoDB, OpenAI e configurações
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

## Fluxo de geração de petição

```text
POST /peticoes/gerar
    -> busca as 30 ementas de maior confiança
    -> combina fatos e ementas em um prompt
    -> chama POST /v1/responses na OpenAI
    -> devolve output_text como texto
```

O prompt instrui o modelo a não inventar fatos ou jurisprudência. Essa instrução
reduz o risco de conteúdo indevido, mas não substitui revisão jurídica humana.

## Ciclos de vida e dependências

A API registra serviços de aplicação e repositórios como `scoped`. O gateway da
OpenAI usa um `HttpClient` gerenciado por `IHttpClientFactory`. O scraper cria
uma sessão Playwright e um `HttpClient` próprios para cada pesquisa.

Os índices do MongoDB são criados sob demanda no início das operações de
ingestão e reprocessamento, não durante a inicialização da API.

A fundação do caso usa um `DbContext` scoped e repositórios implementados com
Entity Framework Core. O modelo relacional é configurado explicitamente pela
Fluent API. Na inicialização, o serviço de migrations cria um escopo próprio e
executa `Database.MigrateAsync()` quando `Postgres:RunMigrations` está ativo.

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
