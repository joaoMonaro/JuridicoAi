CREATE TABLE casos (
    id uuid PRIMARY KEY,
    titulo varchar(200) NOT NULL,
    descricao varchar(4000),
    status varchar(20) NOT NULL,
    data_criacao timestamptz NOT NULL,
    data_atualizacao timestamptz NOT NULL,
    CONSTRAINT ck_casos_titulo CHECK (length(btrim(titulo)) > 0),
    CONSTRAINT ck_casos_status CHECK (status IN ('Ativo', 'Arquivado'))
);

CREATE INDEX ix_casos_status ON casos (status);

CREATE TABLE envolvidos_caso (
    id uuid PRIMARY KEY,
    caso_id uuid NOT NULL REFERENCES casos (id) ON DELETE RESTRICT,
    nome varchar(200) NOT NULL,
    tipo varchar(40) NOT NULL,
    documento varchar(50),
    dados_adicionais jsonb,
    data_criacao timestamptz NOT NULL,
    data_atualizacao timestamptz NOT NULL,
    CONSTRAINT ck_envolvidos_caso_nome CHECK (length(btrim(nome)) > 0),
    CONSTRAINT ck_envolvidos_caso_tipo CHECK (tipo IN (
        'Cliente', 'ParteContraria', 'Advogado', 'Testemunha',
        'TerceiroInteressado', 'Outro'))
);

CREATE INDEX ix_envolvidos_caso_caso_id ON envolvidos_caso (caso_id);

CREATE TABLE contextos_caso (
    id uuid PRIMARY KEY,
    caso_id uuid NOT NULL REFERENCES casos (id) ON DELETE RESTRICT,
    tipo varchar(40) NOT NULL,
    titulo varchar(200),
    conteudo varchar(50000) NOT NULL,
    data_criacao timestamptz NOT NULL,
    data_atualizacao timestamptz NOT NULL,
    CONSTRAINT ck_contextos_caso_conteudo CHECK (length(btrim(conteudo)) > 0),
    CONSTRAINT ck_contextos_caso_tipo CHECK (tipo IN (
        'RelatoInicial', 'ResumoDosFatos', 'Cronologia', 'Observacao',
        'Estrategia', 'Outro'))
);

CREATE INDEX ix_contextos_caso_caso_id ON contextos_caso (caso_id);

CREATE TABLE documentos_caso (
    id uuid PRIMARY KEY,
    caso_id uuid NOT NULL REFERENCES casos (id) ON DELETE RESTRICT,
    nome_original varchar(255) NOT NULL,
    nome_armazenado varchar(255) NOT NULL,
    tipo varchar(100) NOT NULL,
    mime_type varchar(150) NOT NULL,
    tamanho_bytes bigint NOT NULL,
    storage_id varchar(500) NOT NULL,
    data_upload timestamptz NOT NULL,
    CONSTRAINT uq_documentos_caso_storage_id UNIQUE (storage_id),
    CONSTRAINT ck_documentos_caso_tamanho CHECK (tamanho_bytes > 0)
);

CREATE INDEX ix_documentos_caso_caso_id ON documentos_caso (caso_id);
