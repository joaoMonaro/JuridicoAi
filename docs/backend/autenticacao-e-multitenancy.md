# Autenticação e multitenancy

A API usa banco e schema compartilhados, com isolamento lógico por
`organizationId`. No domínio, `Organization` representa um escritório ou
espaço de trabalho, `User` representa a identidade autenticável e `Membership`
vincula os dois com role `Owner` ou `Member`.

A descrição organizacional abaixo não se aplica à sessão pessoal de histórico,
que tem esquema e audiência separados, descritos ao final.

## Modelo

O PostgreSQL contém:

- `organizations`: nome, status `Active` ou `Inactive` e timestamps;
- `users`: nome, e-mail globalmente único, hash de senha, status e timestamps;
- `memberships`: vínculo único por Organization e User, com a role;
- `sessions`: hashes do refresh token completo e da credencial estável de
  revogação, expiração, revogação e último uso.

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

O access token dura 15 minutos por padrão. O refresh token, opaco para o cliente,
contém duas partes de 64 bytes aleatórios codificados em Base64, separadas por
ponto: credencial estável de revogação e parte rotativa. Dura 30 dias por padrão
e é enviado somente em cookie HttpOnly. O banco armazena os SHA-256 hexadecimais
do token completo e da parte estável, nunca seus valores originais.

O perfil Production define `Auth:SecureRefreshCookie=true`; sobrescrever para
`false` impede a inicialização. O cookie usa `HttpOnly`, `SameSite=Strict` e
caminho `/auth`, inclusive nas operações de renovação e logout. Em Development,
o padrão continua `false` para permitir HTTP local.

Em Production, a API exige HTTPS em todas as rotas. Requisições HTTP retornam
400 com código `HTTPS_REQUIRED`, antes da execução dos endpoints e sem emitir
cookies ou redirecionar a requisição. Cabeçalhos de origens não autorizadas não
contornam a proteção. O middleware padrão de encaminhamento é executado antes
de HTTPS e do rate limit: `ReverseProxy:KnownProxies`/`KnownNetworks` definem
quem pode encaminhar o IP e o protocolo originais. As duas listas vazias mantêm
o encaminhamento desativado; `ForwardLimit` limita os saltos e vale 1 por padrão.
As contagens de `X-Forwarded-For` e `X-Forwarded-Proto` devem coincidir.
O certificado, o domínio e os endereços reais dos proxies serão configurados
na publicação. A cota de autenticação permanece 10 requisições/minuto por IP
original, por instância.

## Configuração

O cadastro público fica fechado por padrão (`Auth:RegistrationEnabled=false`).
O perfil `Development` habilita o cadastro para desenvolvimento; variáveis de
ambiente e user-secrets podem sobrescrever essa opção. No piloto, use
`Auth__RegistrationEnabled=false` e crie o participante pelo comando administrativo.
Essa opção não bloqueia login, refresh ou provisionamento.

```bash
export Auth__RegistrationEnabled=false
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
| `POST` | `/auth/register` | pública, somente com cadastro habilitado |
| `POST` | `/auth/login` | pública |
| `POST` | `/auth/refresh` | cookie de refresh |
| `POST` | `/auth/logout` | cookie de refresh, idempotente |
| `GET` | `/auth/me` | Bearer JWT |

### Registro

Com `Auth:RegistrationEnabled=false`, a API retorna HTTP 403 com o código
`REGISTRATION_DISABLED`, sem criar dados ou emitir tokens/cookie.

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
anterior deixa de renovar acesso imediatamente. A parte estável é preservada;
renovar exige o token completo atual. A atualização exige também sessão não
revogada e não expirada, permitindo somente uma rotação concorrente por token.

`POST /auth/logout` identifica a Session pelo hash da parte estável do cookie e
a revoga com um único UPDATE, removendo o cookie na resposta. Um cookie anterior
à última rotação continua podendo encerrar a mesma sessão. Repetir a operação
é seguro; ela afeta somente a sessão identificada, preservando outras sessões
do mesmo usuário e de outros usuários.

Se o refresh atualizar primeiro, o logout ainda encontra a sessão pela credencial
estável. Se o logout revogar primeiro, a atualização condicional do refresh falha
com HTTP 401. Uma resposta de refresh atrasada pode entregar um cookie, mas suas
credenciais já não concedem acesso. A validação JWT de produção, centralizada em
`OrganizationAuthentication`, consulta a sessão no banco em cada requisição;
validações posteriores ao logout recusam também os access tokens já emitidos.

Tokens vazios, malformados ou maiores que 512 caracteres não renovam acesso;
logout com token ausente ou inválido continua idempotente.

### Compatibilidade e migration da sessão principal

A migration `20261120155327_AddMainSessionRevocationSecret` adiciona
`revocation_token_hash` e seu índice único. Para sessões existentes, copia o hash
do refresh atual para esse campo. O token legado de uma parte continua funcionando
e, na primeira renovação, passa a ser a parte estável do formato composto. Depois
dessa rotação, o token antigo serve para logout, mas não renova acesso.

Atualizar com a instância antiga parada/drenada, aplicar a migration e iniciar
a nova versão, evitando processos antigos gravando sessões sem o novo campo.
O downgrade é bloqueado enquanto houver sessões ativas, não expiradas, com
tokens compostos, para preservar sua credencial de revogação. A migration foi
verificada somente em PostgreSQL descartável; nenhum banco existente foi alterado.

Ver [evidência de concorrência, revogação e isolamento](../../implementacoes/features/preparacao-producao/evidencias/sessao-principal-2026-11-20.md).

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

## Acesso ao agente no piloto

O participante provisionado já tem acesso ao agente na sua Organization ativa,
sem lista de participantes ou gate adicional de v2. Isso também vale para contas
existentes: as quatro permissões atuais de IA ficam habilitadas por padrão;
bloqueios administrativos explícitos continuam sendo respeitados por organização.
As flags operacionais do ambiente, o kill switch, o aviso de transparência e o
controle financeiro continuam valendo. Fechar o cadastro não revoga contas já
criadas; antes do piloto, conferir as contas ativas no banco do ambiente publicado.

## Isolamento dos dados privados

Casos, Peças e Templates possuem `organizationId`, `createdById` e
`updatedById`. O cliente não envia esses campos como autoridade: eles vêm dos
claims validados pelo `AuthContext`.

Repositories sempre combinam o identificador do recurso com a Organization.
Acesso a outra Organization retorna 404, sem revelar a existência do dado.
Peças validam que Caso e Template pertencem à Organization atual e preservam o
update atômico por `id + organizationId + versao`.

Envolvidos, contextos e documentos são isolados por meio do Caso pai.

## Migração de dados anteriores

A migration `AddOrganizationsAndAuth` cria uma Organization legada e um User
legado inativo com credencial deliberadamente inutilizável. Casos, Peças e
Templates existentes são associados a esses registros antes da criação das
FKs obrigatórias; nenhum dado é apagado. Novos clientes devem ser criados pelo
comando de provisionamento.


## Sessão pessoal para histórico do assistente

Implementada separadamente da sessão organizacional. Não remover a exigência de
Membership dos endpoints do workspace para permitir leitura histórica.

- `POST /auth/pessoal/login`: e-mail/senha, conta ativa; não exige organização.
- `POST /auth/pessoal/refresh`: rotação do refresh da sessão pessoal.
- `POST /auth/pessoal/logout`: revogação, inclusive proteção contra refresh concorrente.
- JWT de 15 minutos com audiência `JuridicoAI.History`, `sub` e `sessionId`, sem
  organização/role; validade da sessão e conta conferida no banco.
- Refresh de 7 dias, hashes no banco e cookie `juridicoai_history_refresh`,
  Secure/HttpOnly/Strict, caminho `/auth/pessoal`. Endpoints verificam Origin e
  usam o rate limit de autenticação. A verificação de origem usa a mesma lista
  do CORS em `Cors:AllowedOrigins`: produção exige origens HTTPS exatas, sem
  localhost ou curingas. O acesso local automático é exclusivo de Development.
  Frontend/API precisam manter o mesmo site para os cookies Strict.

O token pessoal é aceito somente nas rotas `/assistente/v2/pessoal`, que leem ou
excluem o histórico privado do próprio titular. Não acessa Peça/Caso atuais,
fontes compartilhadas, geração ou aplicação. Token organizacional não substitui
a sessão pessoal. O usuário pode perder Membership e continuar lendo o histórico,
mas conta desabilitada ou sessão revogada continuam sem acesso.

Ver [contratos efetivos](../../implementacoes/features/assistente-conversacional/especificacoes/07-contratos-implementados.md)
e [evidência de autorização e exclusão](../../implementacoes/features/assistente-conversacional/evidencias/e1-historico-pessoal-exclusao-2026-11-12.md).
