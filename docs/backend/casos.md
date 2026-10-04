# Fundação do caso

Todos os endpoints deste documento exigem `Authorization: Bearer`. O Caso
pertence à Organization do token e registra o User que criou e atualizou o
registro. Identificadores de outra Organization são tratados como inexistentes.

O agregado `Caso` organiza dados básicos, envolvidos, contextos textuais,
metadados de documentos e Peças jurídicas. A fundação não processa o conteúdo
dos arquivos com IA. Peças e seus templates são descritos separadamente em
[Peças e templates](pecas.md).

## Persistência e inicialização

Os dados são persistidos no PostgreSQL por meio do Entity Framework Core e do
provedor `Npgsql.EntityFrameworkCore.PostgreSQL`. O `JuridicoAiDbContext`
expõe os conjuntos de identidade, casos, envolvidos, contextos, documentos,
Peças, templates e governança da IA. Nomes de tabelas e colunas, tamanhos, conversões de enums,
índices, relacionamentos e checks são definidos explicitamente pela Fluent API
em `Infrastructure/Database/EntityConfigurations.cs`.

Na inicialização, a API executa `Database.MigrateAsync()` e registra as
migrations aplicadas na tabela padrão `__EFMigrationsHistory`. Se
`Postgres:ConnectionString` estiver vazio, a API continua disponível para os
fluxos que não dependem de PostgreSQL, registra um aviso e não aplica
migrations. Os endpoints de casos continuam exigindo uma conexão configurada.

Configuração por variável de ambiente:

```bash
export Postgres__ConnectionString="Host=localhost;Port=5432;Database=juridicoai;Username=juridico;Password=..."
```

`Postgres:RunMigrations` permite desabilitar a execução automática. A migration
inicial do EF Core cria `casos`, `envolvidos_caso`, `contextos_caso` e
`documentos_caso`. A migration `AddPecas` acrescenta `pecas` e
`templates_peca`, com chaves estrangeiras, checks e índices de listagem.
Migrations posteriores acrescentam identidade, sessões, consumo, feature flags,
confirmações de transparência e auditoria operacional da IA; o diretório de
migrations e `__EFMigrationsHistory` são a referência para a sequência completa.

### Compatibilidade com o banco anterior

Se o banco já tiver sido criado pela antiga `001_fundacao_caso.sql` e essa
versão estiver registrada em `schema_migrations`, a inicialização importa esse
estado para `__EFMigrationsHistory`. A migration inicial do EF é então marcada
como aplicada sem recriar tabelas ou apagar dados. A tabela antiga pode
permanecer no banco como registro histórico, mas migrations novas passam a ser
controladas exclusivamente pelo EF Core.

### Criar uma nova migration

Instale a ferramenta `dotnet-ef` em versão compatível com o EF Core do projeto.
Depois de alterar as entidades ou configurações Fluent, execute na raiz:

```bash
dotnet ef migrations add NomeDaAlteracao \
  --project src/JuridicoAI.Api/JuridicoAI.Api.csproj \
  --startup-project src/JuridicoAI.Api/JuridicoAI.Api.csproj \
  --output-dir src/Infrastructure/Database/Migrations \
  --namespace JuridicoAI.Api.Infrastructure.Database.Migrations
```

Revise os métodos `Up` e `Down` gerados antes de aplicar a migration,
principalmente em alterações que removem ou transformam dados. Em
desenvolvimento, ela será aplicada na próxima inicialização quando
`Postgres:RunMigrations=true`. Também pode ser aplicada explicitamente:

```bash
dotnet ef database update \
  --project src/JuridicoAI.Api/JuridicoAI.Api.csproj \
  --startup-project src/JuridicoAI.Api/JuridicoAI.Api.csproj
```

Em produção, prefira executar a atualização como etapa única do deploy e
desabilitar a execução concorrente pelas réplicas da aplicação.

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

Criação de pessoa física:

```json
{
  "nome": "João da Silva",
  "tipo": "Cliente",
  "natureza": "PessoaFisica",
  "cpfCnpj": "529.982.247-25",
  "rg": "12345678",
  "rgOrgaoEmissor": "SSP",
  "rgUf": "SP",
  "nacionalidade": "Brasileira",
  "estadoCivil": "Casado",
  "profissao": "Motorista",
  "dataNascimento": "1985-04-10",
  "email": "joao@example.com",
  "telefone": "+55 11 99999-9999"
}
```

Criação de pessoa jurídica:

```json
{
  "nome": "Empresa Exemplo Ltda.",
  "nomeFantasia": "Empresa Exemplo",
  "tipo": "ParteContraria",
  "natureza": "PessoaJuridica",
  "cpfCnpj": "11.222.333/0001-81",
  "inscricaoEstadual": "123456789",
  "email": "juridico@empresa.example"
}
```

Tipos: `Cliente`, `ParteContraria`, `Advogado`, `Testemunha`,
`TerceiroInteressado` e `Outro`. Múltiplos envolvidos do mesmo tipo são
permitidos. Naturezas aceitas: `NaoInformada`, `PessoaFisica`,
`PessoaJuridica` e `OrgaoPublico`.

CPF, CNPJ, CEP, UF, país, e-mail e telefone são normalizados pelo backend.
CPF/CNPJ fornecido precisa possuir dígitos verificadores válidos e ser
compatível com a natureza. A listagem mascara o documento; a consulta
individual autenticada retorna a identidade completa. `Documento` e
`DadosAdicionais` permanecem apenas como colunas legadas e não fazem parte dos
novos contratos.

## Endereços dos envolvidos

| Método | Rota |
|---|---|
| `POST` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos` |
| `GET` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos` |
| `GET` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos/{id}` |
| `PATCH` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos/{id}` |
| `DELETE` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos/{id}` |

Endereço residencial:

```json
{
  "tipo": "Residencial",
  "principal": true,
  "cep": "01001-000",
  "logradouro": "Praça da Sé",
  "numero": "100-A",
  "complemento": "Apto 20",
  "bairro": "Sé",
  "cidade": "São Paulo",
  "uf": "SP",
  "pais": "BR"
}
```

Sede empresarial usa o mesmo contrato com `tipo` igual a `Sede`. Os demais
tipos são `Comercial`, `Correspondencia`, `Citacao` e `Outro`. Pode existir
somente um endereço principal de cada tipo por envolvido; marcar outro como
principal desmarca o anterior na mesma transação. O `PATCH` substitui os campos
do endereço pelo contrato enviado.

```json
{
  "tipo": "Sede",
  "principal": true,
  "cep": "20040-020",
  "logradouro": "Rua da Assembleia",
  "numero": "10",
  "bairro": "Centro",
  "cidade": "Rio de Janeiro",
  "uf": "RJ",
  "pais": "BR"
}
```

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

`GET /casos/{id}/contexto-completo` retorna o caso, a identidade completa dos
envolvidos com seus endereços, contextos e metadados dos documentos. Os
endereços são carregados em uma consulta única e o binário dos documentos não
faz parte da resposta.

Todos os acessos a filhos usam simultaneamente `casoId` e `id`: um identificador
válido não pode ser consultado, alterado ou removido pela rota de outro caso.
Recursos inexistentes retornam 404 no formato `application/problem+json`.
