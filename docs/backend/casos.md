# Fundação do caso

O agregado `Caso` organiza dados básicos, envolvidos, contextos textuais e
metadados de documentos. Esta etapa não integra o caso à geração de petições
nem processa o conteúdo dos arquivos com IA.

## Persistência e inicialização

Os dados são persistidos no PostgreSQL. Na inicialização, a API aplica em uma
transação as migrations SQL embutidas ainda ausentes de `schema_migrations`.
Se `Postgres:ConnectionString` estiver vazio, a API continua disponível para
os fluxos antigos, registra um aviso e não aplica migrations; os endpoints de
casos dependem de uma conexão configurada.

Configuração por variável de ambiente:

```bash
export Postgres__ConnectionString="Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=..."
```

`Postgres:RunMigrations` permite desabilitar a execução automática. A migration
`001_fundacao_caso.sql` cria `casos`, `envolvidos_caso`, `contextos_caso` e
`documentos_caso`, com chaves estrangeiras, checks e índices por `caso_id`.

## Casos

| Método | Rota | Resultado |
|---|---|---|
| `POST` | `/casos` | cria um caso ativo |
| `GET` | `/casos?status=Ativo` | lista casos; o filtro é opcional |
| `GET` | `/casos/{id}` | obtém um caso |
| `PUT` | `/casos/{id}` | substitui título, descrição e status |
| `DELETE` | `/casos/{id}` | arquiva logicamente o caso |

Criação:

```json
{
  "titulo": "Negativação indevida - João da Silva",
  "descricao": "Cliente relata dívida desconhecida."
}
```

Atualização:

```json
{
  "titulo": "Negativação indevida - João da Silva",
  "descricao": "Relato revisado.",
  "status": "Ativo"
}
```

Os status aceitos são `Ativo` e `Arquivado`. O arquivamento não remove os
vínculos ou arquivos do caso.

## Envolvidos

| Método | Rota |
|---|---|
| `POST` | `/casos/{casoId}/envolvidos` |
| `GET` | `/casos/{casoId}/envolvidos` |
| `GET` | `/casos/{casoId}/envolvidos/{id}` |
| `PUT` | `/casos/{casoId}/envolvidos/{id}` |
| `DELETE` | `/casos/{casoId}/envolvidos/{id}` |

```json
{
  "nome": "João da Silva",
  "tipo": "Cliente",
  "documento": "000.000.000-00",
  "dadosAdicionais": {
    "email": "joao@example.com"
  }
}
```

Tipos: `Cliente`, `ParteContraria`, `Advogado`, `Testemunha`,
`TerceiroInteressado` e `Outro`. Múltiplos envolvidos do mesmo tipo são
permitidos.

## Contextos

| Método | Rota |
|---|---|
| `POST` | `/casos/{casoId}/contextos` |
| `GET` | `/casos/{casoId}/contextos` |
| `GET` | `/casos/{casoId}/contextos/{id}` |
| `PUT` | `/casos/{casoId}/contextos/{id}` |
| `DELETE` | `/casos/{casoId}/contextos/{id}` |

```json
{
  "tipo": "RelatoInicial",
  "titulo": "Relato do cliente",
  "conteudo": "Cliente afirma que nunca contratou com a empresa."
}
```

Tipos: `RelatoInicial`, `ResumoDosFatos`, `Cronologia`, `Observacao`,
`Estrategia` e `Outro`.

## Documentos

| Método | Rota | Resultado |
|---|---|---|
| `POST` | `/casos/{casoId}/documentos` | upload `multipart/form-data` |
| `GET` | `/casos/{casoId}/documentos` | lista metadados |
| `GET` | `/casos/{casoId}/documentos/{id}` | obtém metadados |
| `GET` | `/casos/{casoId}/documentos/{id}/arquivo` | baixa o binário |
| `DELETE` | `/casos/{casoId}/documentos/{id}` | remove metadados e arquivo |

O formulário usa o campo obrigatório `arquivo` e o campo textual opcional
`tipo`. O nome recebido nunca é usado como caminho; o servidor gera o
`StorageId`. Se a persistência dos metadados falhar após salvar o arquivo, o
serviço tenta removê-lo imediatamente.

Configurações de `DocumentStorage`:

| Chave | Padrão |
|---|---|
| `RootPath` | `data/documentos` relativo à API |
| `MaxFileSizeBytes` | `20971520` (20 MiB) |
| `AllowedExtensions` | PDF, DOC, DOCX, TXT, PNG, JPG/JPEG |
| `AllowedMimeTypes` | tipos MIME correspondentes |

Extensão e MIME precisam estar simultaneamente permitidos. Arquivo vazio
retorna 400, tamanho excedido retorna 413 e formato não suportado retorna 415.

## Contexto completo

`GET /casos/{id}/contexto-completo` retorna o caso, todos os envolvidos,
contextos e metadados dos documentos. O binário dos documentos não faz parte
da resposta.

Todos os acessos a filhos usam simultaneamente `casoId` e `id`: um identificador
válido não pode ser consultado, alterado ou removido pela rota de outro caso.
Recursos inexistentes retornam 404 no formato `application/problem+json`.
