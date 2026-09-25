# Autenticação e multitenancy

A API usa banco e schema compartilhados, com isolamento lógico por
`organizationId`. No domínio, `Organization` representa um escritório ou
espaço de trabalho, `User` representa a identidade autenticável e `Membership`
vincula os dois com role `Owner` ou `Member`.

## Modelo

O PostgreSQL contém:

- `organizations`: nome, status `Active` ou `Inactive` e timestamps;
- `users`: nome, e-mail globalmente único, hash de senha, status e timestamps;
- `memberships`: vínculo único por Organization e User, com a role;
- `sessions`: hash do refresh token, expiração, revogação e último uso.

Uma Organization inativa ou um User inativo não pode iniciar nem renovar uma
sessão. O e-mail é normalizado com `Trim()` e letras minúsculas.

## Tokens e senha

Senhas são armazenadas com Argon2id, salt aleatório de 16 bytes, memória de
64 MiB, três iterações, paralelismo 2 e saída de 32 bytes. Somente o hash
codificado é persistido.

O access token é um JWT assinado exclusivamente com HMAC-SHA256. O middleware
valida assinatura, algoritmo, emissor, audiência, expiração e a Session
referenciada. Seus claims incluem:

```json
{
  "sub": "user-id",
  "organizationId": "organization-id",
  "sessionId": "session-id",
  "role": "Owner"
}
```

O access token dura 15 minutos por padrão. O refresh token é um valor opaco de
64 bytes aleatórios, dura 30 dias e é enviado somente em cookie HttpOnly. O
banco armazena seu SHA-256 hexadecimal, nunca o token original.

Em produção, configure `Auth:SecureRefreshCookie=true`. O cookie usa
`SameSite=Strict` e caminho `/auth`.

## Configuração

```bash
export Auth__JwtSecret="um-segredo-aleatorio-com-pelo-menos-32-caracteres"
export Auth__Issuer="JuridicoAI"
export Auth__Audience="JuridicoAI.Web"
export Auth__AccessTokenTtlMinutes=15
export Auth__RefreshTokenTtlDays=30
export Auth__SecureRefreshCookie=true
```

O segredo é obrigatório, não possui default funcional e é validado na
inicialização. Login e refresh têm rate limit de dez requisições por minuto por
instância da aplicação.

## Endpoints

| Método | Rota | Autenticação |
|---|---|---|
| `POST` | `/auth/register` | pública |
| `POST` | `/auth/login` | pública |
| `POST` | `/auth/refresh` | cookie de refresh |
| `POST` | `/auth/logout` | cookie de refresh, idempotente |
| `GET` | `/auth/me` | Bearer JWT |

### Registro

```json
{
  "organizationName": "Escritório Exemplo",
  "name": "João da Silva",
  "email": "joao@example.com",
  "password": "senha-segura-com-12-ou-mais"
}
```

`POST /auth/register` cria atomicamente uma Organization ativa, seu primeiro
User ativo, a Membership `Owner` e a Session inicial. O e-mail é normalizado e
deve ser globalmente único. Em caso de duplicidade, inclusive sob registros
concorrentes, a API retorna 409 com o código `EMAIL_ALREADY_EXISTS`.

A resposta tem o mesmo contrato público do login e retorna HTTP 201. O access
token fica no JSON; o refresh token é enviado somente no cookie HttpOnly. O
registro não seleciona uma Organization existente nem cria usuários `Member`.

### Login

```json
{
  "email": "usuario@escritorio.com",
  "senha": "senha informada pelo usuário",
  "organizationId": "opcional-quando-houver-mais-de-uma-membership"
}
```

Com uma única Membership, a Organization é selecionada automaticamente. Com
mais de uma e sem `organizationId`, a API retorna 409 com o código
`ORGANIZATION_SELECTION_REQUIRED`. Uma Organization informada só é aceita se
pertencer ao User autenticado pela senha.

E-mail inexistente, senha incorreta, User inativo, Organization inativa e
Membership inválida retornam a mesma mensagem `Credenciais invalidas.` para
evitar enumeração de usuários.

Resposta:

```json
{
  "accessToken": "...",
  "expiresIn": 900,
  "user": {
    "id": "...",
    "nome": "João",
    "email": "joao@example.com"
  },
  "organization": {
    "id": "...",
    "nome": "Escritório"
  },
  "role": "Owner"
}
```

O refresh token não aparece no JSON; ele é definido no cookie
`juridicoai_refresh`.

### Refresh e logout

`POST /auth/refresh` não exige access token. A operação valida Session, User,
Organization e Membership e troca atomicamente o hash armazenado. O token
anterior deixa de funcionar imediatamente.

`POST /auth/logout` revoga a Session indicada pelo cookie e remove o cookie. Repetir
a operação é seguro. Como o middleware consulta a Session em cada requisição
autenticada, um access token de uma Session revogada também deixa de ser aceito.

### Usuário atual

`GET /auth/me` retorna User, Organization e role do contexto autenticado. Hash
de senha e tokens nunca fazem parte da resposta.

## Provisionamento administrativo

Além do registro público, o primeiro OWNER também pode ser criado por comando
administrativo. Esse caminho permanece disponível para bootstrap e recuperação,
usando variáveis temporárias para não expor a senha nos argumentos ou logs:

```bash
export PROVISION_ORGANIZATION_NAME="Escritório Exemplo"
export PROVISION_OWNER_NAME="João da Silva"
export PROVISION_OWNER_EMAIL="joao@example.com"
export PROVISION_OWNER_PASSWORD="senha-segura-com-12-ou-mais"

dotnet run --project src/JuridicoAI.Api -- provision-organization
```

O caso de uso cria Organization ativa, User ativo e Membership `Owner` em uma
transação, mas não cria uma Session. Falha em qualquer etapa causa rollback.
`createOrganization` existe separadamente no serviço de aplicação para uso
futuro sem duplicar User.

Convites, criação de Members, confirmação de e-mail e recuperação de senha
ainda não fazem parte da API.

## Isolamento dos dados privados

Casos, Peças e Templates possuem `organizationId`, `createdById` e
`updatedById`. O cliente não envia esses campos como autoridade: eles vêm dos
claims validados pelo `AuthContext`.

Repositories sempre combinam o identificador do recurso com a Organization.
Acesso a outra Organization retorna 404, sem revelar a existência do dado.
Peças validam que Caso e Template pertencem à Organization atual e preservam o
update atômico por `id + organizationId + versao`.

Envolvidos, contextos e documentos são isolados por meio do Caso pai. A base de
jurisprudências no MongoDB permanece global porque representa conteúdo público
compartilhado, não dados privados de um cliente.

## Migração de dados anteriores

A migration `AddOrganizationsAndAuth` cria uma Organization legada e um User
legado inativo com credencial deliberadamente inutilizável. Casos, Peças e
Templates existentes são associados a esses registros antes da criação das
FKs obrigatórias; nenhum dado é apagado. Novos clientes devem ser criados pelo
comando de provisionamento.
