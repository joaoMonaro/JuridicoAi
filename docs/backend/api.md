# API HTTP

Base local padrão: `http://localhost:5299`.

A API usa JWT nas rotas privadas. Login e ciclo de sessão são descritos em
[Autenticação e multitenancy](autenticacao-e-multitenancy.md). Em ambiente de
desenvolvimento, a interface Swagger está disponível em
`http://localhost:5299/swagger` e o documento OpenAPI em
`http://localhost:5299/swagger/v1/swagger.json`.

## Resumo dos endpoints

| Método | Rota | Função |
|---|---|---|
| `GET` | `/` | verificação básica da aplicação |
| `POST` | `/auth/register` | cria Organization, Owner e Session inicial |
| `POST` | `/auth/login` | autentica e cria uma Session |
| `POST` | `/auth/refresh` | rotaciona o refresh token |
| `POST` | `/auth/logout` | revoga a Session atual |
| `GET` | `/auth/me` | retorna User, Organization e role atuais |
| `POST` | `/casos` | cria um Caso |
| `GET` | `/casos` | lista Casos, com filtro opcional de status |
| `GET` | `/casos/{id}` | obtém um Caso |
| `PUT` | `/casos/{id}` | atualiza um Caso |
| `DELETE` | `/casos/{id}` | arquiva um Caso |
| `POST` | `/casos/{casoId}/envolvidos` | adiciona envolvido ao Caso |
| `GET` | `/casos/{casoId}/envolvidos` | lista envolvidos do Caso |
| `GET` | `/casos/{casoId}/envolvidos/{id}` | obtém um envolvido |
| `PUT` | `/casos/{casoId}/envolvidos/{id}` | atualiza um envolvido |
| `DELETE` | `/casos/{casoId}/envolvidos/{id}` | remove um envolvido |
| `POST` | `/api/jurisprudencias/ingest` | coleta e processa resultados do TJSP |
| `GET` | `/api/jurisprudencias` | consulta jurisprudências processadas |
| `GET` | `/api/jurisprudencias/raw` | consulta dados brutos |
| `POST` | `/api/jurisprudencias/reprocess` | reprocessa dados brutos existentes |
| `POST` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos` | adiciona endereço ao envolvido |
| `GET` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos` | lista endereços do envolvido |
| `GET` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos/{id}` | obtém um endereço |
| `PATCH` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos/{id}` | substitui os dados de um endereço |
| `DELETE` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos/{id}` | remove um endereço |
| `POST` | `/casos/{casoId}/contextos` | cria um contexto textual |
| `GET` | `/casos/{casoId}/contextos` | lista contextos |
| `GET` | `/casos/{casoId}/contextos/{id}` | obtém um contexto |
| `PUT` | `/casos/{casoId}/contextos/{id}` | atualiza um contexto |
| `DELETE` | `/casos/{casoId}/contextos/{id}` | remove um contexto |
| `POST` | `/casos/{casoId}/documentos` | envia um documento |
| `GET` | `/casos/{casoId}/documentos` | lista metadados de documentos |
| `GET` | `/casos/{casoId}/documentos/{id}` | obtém metadados de um documento |
| `GET` | `/casos/{casoId}/documentos/{id}/arquivo` | baixa o arquivo |
| `DELETE` | `/casos/{casoId}/documentos/{id}` | remove documento e arquivo |
| `GET` | `/casos/{id}/contexto-completo` | agrega Caso, envolvidos, contextos e metadados |
| `POST` | `/casos/{casoId}/pecas` | cria uma Peça jurídica |
| `GET` | `/casos/{casoId}/pecas` | lista Peças sem os documentos completos |
| `GET` | `/pecas/{pecaId}` | obtém uma Peça completa |
| `PATCH` | `/pecas/{pecaId}` | atualiza uma Peça com concorrência otimista |
| `DELETE` | `/pecas/{pecaId}?versao={versao}` | exclui logicamente uma Peça |
| `POST` | `/pecas/{pecaId}/exportar` | baixa Word ou PDF da versão solicitada |
| `GET` | `/assistente/capacidades` | informa disponibilidade, modos e limites do assistente |
| `GET` | `/assistente/transparencia` | retorna aviso atual e estado de confirmação |
| `POST` | `/assistente/transparencia/confirmacoes` | confirma a versão atual do aviso |
| `POST` | `/pecas/{pecaId}/assistente/solicitacoes` | gera sugestão por IA em stream SSE, sem alterar a Peça |
| `POST` | `/templates-peca` | cria um template de Peça |
| `GET` | `/templates-peca` | lista templates de Peça |
| `GET` | `/templates-peca/{id}` | obtém um template de Peça |
| `PATCH` | `/templates-peca/{id}` | atualiza um template de Peça |
| `DELETE` | `/templates-peca/{id}` | exclui logicamente um template de Peça |

Os contratos, exemplos, regras de cópia de template e controle de concorrência
estão documentados em [Peças e templates](pecas.md).
O contrato do assistente e seus eventos estão documentados em
[Assistente de IA](../../implementacoes/standBy/assistente-ia/especificacoes/contrato-api.md).
Para uma visão operacional do comportamento implementado, consulte também
[Assistente de IA](assistente-ia.md).

## Registrar uma conta

```http
POST /auth/register
Content-Type: application/json

{
  "organizationName": "Escritório Exemplo",
  "name": "João da Silva",
  "email": "joao@example.com",
  "password": "senha-segura-com-12-ou-mais"
}
```

O registro cria atomicamente a Organization, seu primeiro User com role
`Owner` e a Session autenticada. Retorna HTTP 201 com o mesmo corpo público do
login e define o refresh token no cookie `juridicoai_refresh`. O token de
refresh não é incluído no JSON. E-mail duplicado retorna HTTP 409 com o código
`EMAIL_ALREADY_EXISTS`.

## Ingerir jurisprudências

```http
POST /api/jurisprudencias/ingest
Content-Type: application/json

{
  "termo": "negativação indevida dano moral",
  "maxResultados": 30
}
```

Validações:

- `termo` é obrigatório e deve ter pelo menos 3 caracteres;
- `maxResultados` é opcional e deve estar entre 1 e 200;
- se omitido, usa `Scraper:MaxResults`.

Resposta 200:

```json
{
  "termoBuscado": "negativação indevida dano moral",
  "coletados": 30,
  "novosRaw": 25,
  "duplicados": 5,
  "classificados": 24,
  "falhas": 1
}
```

Um timeout interno do scraper retorna 504 com `{ "erro": "..." }`. Falhas de
validação retornam 400 no formato padrão do ASP.NET Core.

## Consultar jurisprudências processadas

```http
GET /api/jurisprudencias?tema=negativacao_indevida&subtema=debito_inexistente&resultado=provavelmente_favoravel_consumidor&danoMoral=true&fonte=TJSP&limite=50
```

Todos os filtros são opcionais e combinados com `AND`.

| Parâmetro | Tipo | Comportamento |
|---|---|---|
| `tema` | string | igualdade exata em `classificacao.tema` |
| `subtema` | string | igualdade exata em `classificacao.subtema` |
| `resultado` | string | igualdade exata em `classificacao.resultado` |
| `danoMoral` | boolean | igualdade em `classificacao.danoMoral` |
| `fonte` | string | convertida para maiúsculas antes da consulta |
| `limite` | inteiro | padrão 50; limitado internamente a 1–200 |

Os itens são ordenados por `dataJulgamento` decrescente. Sem filtros, a rota
retorna os itens mais recentes segundo essa ordenação.

## Consultar dados brutos

```http
GET /api/jurisprudencias/raw?limite=20
```

O limite padrão é 50 e é ajustado internamente para 1–200. Os registros são
ordenados por `dataColeta` decrescente. A resposta inclui `htmlBruto`, que pode
ser volumoso.

## Reprocessar dados brutos

```http
POST /api/jurisprudencias/reprocess?termo=negativação%20indevida&limite=500
```

- `termo` é opcional e compara por igualdade exata com `termoBuscado`;
- `limite` tem padrão 500 e é ajustado para 1–1000;
- o reprocessamento não acessa o TJSP;
- todos os raws encontrados são normalizados, classificados e gravados
  novamente, independentemente do status atual.

Resposta 200:

```json
{
  "encontrados": 100,
  "reprocessados": 98,
  "falhas": 2
}
```

## Exemplos executáveis

O arquivo `src/JuridicoAI.Api/JuridicoAI.http` contém exemplos para clientes
compatíveis com arquivos `.http`.
