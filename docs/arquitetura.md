# Arquitetura e fluxos

## Componentes

```text
Cliente HTTP
    |
    |---- JWT/AuthContext ----> Session, User, Membership, Organization
    v
JuridicoAI.Api
    |---- assistente IA --> IAssistenteIaProvider ----> OpenAI Responses API
    |
    |---- identidade, casos e peças (EF Core) --------> PostgreSQL
```

O scraper do TJSP foi extraído para o projeto independente
`../JuridicoAI.Scrapper`, relativo à raiz do repositório. A API não possui
referência ao scraper, rotas de jurisprudências ou dependência de MongoDB.

## Organização da solução

```text
JuridicoAI.sln
src/
└── JuridicoAI.Api/
    ├── Program.cs
    └── src/
        ├── Application/       DTOs e casos de uso
        ├── Controllers/       endpoints HTTP
        ├── Domain/            entidades persistidas
        └── Infrastructure/    EF Core/PostgreSQL, storage e configurações
```

## Ciclos de vida e dependências

A API registra serviços de aplicação e repositórios como `scoped`.

A fundação do caso usa um `DbContext` scoped e repositórios implementados com
Entity Framework Core. O modelo relacional é configurado explicitamente pela
Fluent API. Na inicialização, o serviço de migrations cria um escopo próprio e
executa `Database.MigrateAsync()` quando `Postgres:RunMigrations` está ativo.

Casos, Peças e Templates são dados privados filtrados por `organizationId`.
O middleware JWT valida a Session antes de construir o `AuthContext` usado
pelos serviços e repositórios.

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

## Assistente de IA — fluxo legado e evolução conversacional

O endpoint legado conserva o fluxo abaixo:

```text
POST /pecas/{pecaId}/assistente/solicitacoes
    -> autentica User e Organization
    -> valida configuração, kill switch e feature da Organization
    -> exige confirmação da versão atual do aviso de transparência
    -> valida Peça, modo, seleção, operação e limites
    -> autoriza contextos no mesmo Caso
    -> monta contexto explicitamente selecionado
    -> solicita saída estruturada ao provider com store=false
    -> valida operação, tamanhos, alertas e achados
    -> transmite eventos SSE sequenciados ao cliente
    -> registra somente metadados técnicos e de consumo
```

Esse fluxo não chama atualização de Peça. A aplicação de uma sugestão é uma
ação explícita do frontend no TipTap, condicionada à âncora válida e ao
autosave em `SALVO`, e é persistida posteriormente pelo `PATCH /pecas/{pecaId}`
já existente. O `OrganizationId` nunca é aceito do corpo da requisição como
fonte de autoridade.

O chat v2 acrescenta conversa privada persistida, contexto/memória, documentos
autorizados e propostas tipadas. Com a flag durável, persiste pedido/job, confirma
o enfileiramento via SSE e usa workers PostgreSQL com leases/checkpoints. O
frontend acompanha por polling e recupera resultados no histórico. Aprovar uma
proposta chama a transação de Peça/decisões/recibo, sem PATCH duplicado de autosave.
Histórico pessoal e exclusão usam sessão separada e worker de expurgo autorizado.

Consulte [Assistente de IA](backend/assistente-ia.md),
[contratos implementados](../implementacoes/features/assistente-conversacional/especificacoes/07-contratos-implementados.md)
e [estado atual](../implementacoes/features/assistente-conversacional/estado-atual.md).
O dossiê anterior e o plano de geração de minutas preservam contexto histórico;
não são inventário de pendências técnicas atuais.
