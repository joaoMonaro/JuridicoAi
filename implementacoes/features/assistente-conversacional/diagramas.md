# Diagramas do detalhamento técnico

> Proposta, 2026-11-11. Representações em Mermaid, mantidas no repositório.
> Complementam as especificações; não representam serviços já implementados.

## Componentes e fronteira de escrita

```mermaid
flowchart TD
    UI["Frontend: chat e editor TipTap"] --> API["API autenticada"]
    API --> Conversa["Conversa privada por usuario e Peca"]
    Conversa --> DB[("PostgreSQL: historico e trabalhos")]
    DB --> Worker["Worker com lease e checkpoints"]
    Worker --> Contexto["Contexto autorizado e memoria"]
    Contexto --> Fontes["Anexos privados e documentos autorizados do Caso"]
    Fontes --> Extracao["Extracao e OCR local"]
    Extracao --> Qualidade["Somente trechos de leitura confiavel"]
    Qualidade --> Contexto
    Contexto --> IA["Modelo e ferramentas de consulta ou proposta"]
    IA --> Proposta["Compilacao e validacao da proposta"]
    Proposta --> Preview["Comparacao no documento"]
    Preview --> Aprovacao["Aprovacao explicita do usuario"]
    Aprovacao --> Aplicacao["Servico deterministico de aplicacao"]
    Aplicacao --> Commit["Transacao: Peca, decisoes e recibo"]
    Commit --> DB
    DB --> Eventos["Eventos persistidos e SSE"]
    Eventos --> UI
```

O modelo não tem ligação de escrita com a Peça. Consulta histórica pessoal usa
rota própria, sem passar pelo worker, modelo ou consulta ao Caso atual.

## Aprovação e conflito

```mermaid
sequenceDiagram
    actor Usuario
    participant Front as Frontend
    participant Auto as Autosave
    participant API as API de aplicacao
    participant DB as PostgreSQL

    Usuario->>Front: Aprovar grupos exibidos
    Front->>Auto: Bloquear mutacoes e ensurePersisted
    Auto-->>Front: Versao sincronizada
    Front->>API: RequestId, revisoes, versao e hashes
    API->>DB: BEGIN e locks curtos
    API->>DB: Conferir titularidade, recibo e proposta
    alt RequestId ja confirmado
        DB-->>API: Recibo persistido
        API->>DB: Encerrar transacao
        API-->>Front: Mesmo recibo e indicacao de atualidade
    else Nova aplicacao com base compativel
        API->>DB: Atualizar Peca e registrar grupos e recibo
        API->>DB: COMMIT
        API-->>Front: Documento canonico e nova versao
        Front->>Auto: Aplicar transacao e adotar commit externo
        Auto-->>Front: Salvo, sem PATCH duplicado
        Front-->>Usuario: Mudancas aplicadas e desfazer disponivel
    else Versao ou revisao divergente
        API->>DB: ROLLBACK
        API-->>Front: Conflito, sem alteracao parcial
        Front-->>Usuario: Preservar edicao e atualizar revisao
    end
```

Se a resposta se perder, o frontend consulta o recibo com o mesmo requestId.
Um recibo antigo não autoriza sobrescrever uma Peça que já avançou de versão.

## Relações centrais de conteúdo

```mermaid
erDiagram
    USER ||--o{ AI_CONVERSATION : owns
    PECA ||--o{ AI_CONVERSATION : references
    AI_CONVERSATION ||--o{ AI_MESSAGE : contains
    AI_CONVERSATION ||--o{ AI_ATTACHMENT : isolates
    AI_MESSAGE ||--o{ AI_MESSAGE_ATTACHMENT : links
    AI_ATTACHMENT ||--o{ AI_MESSAGE_ATTACHMENT : links
    AI_CONVERSATION ||--o{ AI_EXECUTION : runs
    AI_EXECUTION ||--o{ AI_SNAPSHOT : captures
    AI_EXECUTION ||--o{ AI_PROPOSAL : produces
    AI_PROPOSAL ||--o{ AI_CHANGE_GROUP : groups
    AI_PROPOSAL ||--o{ AI_APPLICATION_RECEIPT : confirms
    AI_CONVERSATION ||--o{ AI_MEMORY : summarizes
    AI_ATTACHMENT ||--o{ AI_DOCUMENT_EXTRACTION : extracts
    DOCUMENTO_CASO ||--o{ AI_DOCUMENT_EXTRACTION : extracts
    AI_DOCUMENT_EXTRACTION ||--o{ AI_DOCUMENT_CHUNK : partitions
```

A extração tem origem exclusiva: anexo privado **ou** DocumentoCaso, nunca ambos.
A FK da Peça para conversas não implica exclusão em cascata. Organização e
titularidade continuam nos filtros e constraints descritos na especificação de
dados; este diagrama omite colunas e tabelas operacionais para facilitar leitura.
