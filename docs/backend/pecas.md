# Peças e templates

Uma Peça é um documento jurídico pertencente obrigatoriamente a um Caso. O
backend armazena o documento contínuo e suas configurações, mas não renderiza
páginas, HTML, PDF ou DOCX. O JSON TipTap/ProseMirror é a fonte canônica para o
futuro editor.

Todos os endpoints de Peças e Templates exigem `Authorization: Bearer` e usam a
Organization do token como fonte de autoridade.

## Modelo e persistência

Peças e templates são persistidos no PostgreSQL por meio do Entity Framework
Core. A migration `AddPecas` cria as tabelas `pecas` e `templates_peca`.

Uma Peça contém:

- vínculo obrigatório por `casoId`;
- título e status;
- `conteudoJson` com o documento TipTap;
- referência opcional `templateId`;
- `pageConfig`, `headerJson`, `footerJson` e `styles` separados do conteúdo;
- `versao` para concorrência otimista;
- datas de criação, atualização e exclusão lógica.

Os campos JSON são colunas PostgreSQL `jsonb`. O backend não converte o
conteúdo para string, HTML ou Markdown e preserva a estrutura recebida. Não há
entidade de página: paginação e quebra de páginas permanecem responsabilidades
da apresentação.

Os status serializados pela API são `Rascunho` e `Finalizada`. Toda Peça começa
como `Rascunho`, com `versao` igual a 1.

## Endpoints de Peças

| Método | Rota | Resultado |
|---|---|---|
| `POST` | `/casos/{casoId}/pecas` | cria uma Peça no Caso |
| `GET` | `/casos/{casoId}/pecas` | lista metadados das Peças do Caso |
| `GET` | `/pecas/{pecaId}` | obtém o documento completo |
| `PATCH` | `/pecas/{pecaId}` | atualiza parcialmente com controle de versão |
| `DELETE` | `/pecas/{pecaId}?versao={versao}` | exclui logicamente com controle de versão |

### Criação sem template

```http
POST /casos/11111111-1111-1111-1111-111111111111/pecas
Content-Type: application/json

{
  "titulo": "Petição inicial"
}
```

Sem template, a aplicação usa defaults centralizados: documento TipTap vazio,
papel A4, margens em milímetros e estilos jurídicos básicos com Times New Roman.

### Criação a partir de template

```json
{
  "titulo": "Contestação",
  "templateId": "22222222-2222-2222-2222-222222222222"
}
```

Na criação, `conteudoJson`, `pageConfig`, `headerJson`, `footerJson` e `styles`
são copiados do template. A Peça conserva o `templateId` como rastreabilidade,
mas os JSONs passam a pertencer à Peça. Alterar ou excluir logicamente o
template posteriormente não modifica Peças existentes.

Caso ou template inexistente retorna 404.

### Listagem

`GET /casos/{casoId}/pecas` retorna apenas:

```json
[
  {
    "id": "33333333-3333-3333-3333-333333333333",
    "titulo": "Petição inicial",
    "status": "Rascunho",
    "versao": 4,
    "templateId": null,
    "dataAtualizacao": "2026-08-14T02:00:00Z"
  }
]
```

A projeção é feita no banco e não carrega `conteudoJson`, cabeçalho, rodapé,
configuração de página ou estilos. A leitura completa está disponível somente
em `GET /pecas/{pecaId}`.

### Atualização parcial e concorrência

Toda atualização exige a versão conhecida pelo cliente:

```http
PATCH /pecas/33333333-3333-3333-3333-333333333333
Content-Type: application/json

{
  "titulo": "Petição inicial revisada",
  "conteudoJson": {
    "type": "doc",
    "content": []
  },
  "versao": 4
}
```

Também podem ser atualizados `pageConfig`, `headerJson`, `footerJson`, `styles`
e `status`. Ao menos um campo além de `versao` precisa ser enviado.

O repositório executa um update condicional por `id`, `versao` e ausência de
exclusão lógica. O incremento ocorre na mesma instrução SQL. Se a versão 4 for
aceita, a resposta contém a versão 5:

```json
{
  "id": "33333333-3333-3333-3333-333333333333",
  "versao": 5,
  "dataAtualizacao": "2026-08-14T02:03:00Z"
}
```

Quando nenhuma linha corresponde à versão esperada, a API retorna 409 em
`application/problem+json`:

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "A peca foi alterada por outra requisicao.",
  "code": "PECA_VERSION_CONFLICT"
}
```

O campo `versao` não representa histórico, snapshots ou restauração de
versões. Ele existe exclusivamente para impedir sobrescrita concorrente.

### Validação dos JSONs

- `conteudoJson` precisa ser um objeto com `type` igual a `doc`;
- os demais campos JSON, quando enviados, precisam ser objetos;
- `pageConfig.format` precisa ser `A4`;
- as quatro margens precisam ser números não negativos em milímetros;
- o backend não valida profundamente todos os nós TipTap.

## Endpoints de templates

| Método | Rota | Resultado |
|---|---|---|
| `POST` | `/templates-peca` | cria um template |
| `GET` | `/templates-peca` | lista templates ativos |
| `GET` | `/templates-peca/{id}` | obtém um template |
| `PATCH` | `/templates-peca/{id}` | atualiza parcialmente um template |
| `DELETE` | `/templates-peca/{id}` | exclui logicamente um template |

Exemplo de criação:

```json
{
  "nome": "Petição inicial padrão",
  "conteudoJson": {
    "type": "doc",
    "content": []
  },
  "pageConfig": {
    "format": "A4",
    "marginTop": 30,
    "marginBottom": 20,
    "marginLeft": 30,
    "marginRight": 20
  },
  "styles": {
    "fontFamily": "Times New Roman",
    "fontSize": 12,
    "lineHeight": 1.5,
    "textAlign": "justify"
  }
}
```

Campos JSON omitidos recebem os mesmos defaults usados na criação de Peças.
Templates também usam soft delete. A chave estrangeira permanece preservada
para que uma Peça continue indicando seu template de origem.

## Exclusão lógica e consultas

`DELETE` define `dataExclusao`; os registros não aparecem em listagens nem em
consultas normais. A exclusão de Peça também usa a versão esperada e incrementa
a versão atomicamente. Não existem restauração ou lixeira nesta etapa.

Os índices de listagem combinam Caso, exclusão e atualização para Peças, e
exclusão e nome para templates. Há também índice para `templateId`.

## Segurança e escopo atual

Peças e Templates possuem `organizationId`, `createdById` e `updatedById`.
Consultas e updates incluem a Organization autenticada; Caso e Template de
outra Organization não podem ser usados para criar uma Peça. O locking
otimista inclui `organizationId` além de identificador e versão.

Não fazem parte dos endpoints de Peças editor visual, paginação, autosave no
cliente, imagens, uploads, PDF, DOCX, histórico ou colaboração em tempo real.
A geração de sugestões por IA existe em endpoints separados, consulta a Peça
somente para autorização e **não** atualiza `conteudoJson`. Veja o
[Assistente de IA](../../implementacoes/features/assistente-ia/README.md).
