# API HTTP

Base local padrão: `http://localhost:5299`.

A API usa JWT nas rotas privadas (detalhes em
[Autenticação e multitenancy](autenticacao-e-multitenancy.md)). Em `Development`,
o Swagger fica em `/swagger` e o OpenAPI em `/swagger/v1/swagger.json`.

## Infraestrutura e autenticação

| Método | Rota | Função |
| :--- | :--- | :--- |
| `GET` | `/` | verificação básica da aplicação |
| `GET` | `/health/live` | liveness HTTP (processo ativo) |
| `POST` | `/auth/register` | cria Organization, Owner e Session inicial (desabilitado quando `Auth:RegistrationEnabled=false`) |
| `POST` | `/auth/login` | autentica e cria uma Session |
| `POST` | `/auth/refresh` | rotaciona o refresh token (`juridicoai_refresh`) |
| `POST` | `/auth/logout` | revoga a Session atual |
| `GET` | `/auth/me` | retorna User, Organization e role atuais |
| `POST` | `/auth/pessoal/login` | autentica sessão pessoal isolada (histórico do assistente) |
| `POST` | `/auth/pessoal/refresh` | rotaciona refresh token da sessão pessoal |
| `POST` | `/auth/pessoal/logout` | revoga a sessão pessoal |

## Casos, Peças e Templates

| Método | Rota | Função |
| :--- | :--- | :--- |
| `POST/GET` | `/casos` | cria ou lista Casos (`?status=Ativo`) |
| `GET/PUT/DELETE` | `/casos/{id}` | obtém, atualiza ou arquiva um Caso |
| `POST/GET` | `/casos/{casoId}/envolvidos` | adiciona ou lista envolvidos |
| `GET/PUT/DELETE` | `/casos/{casoId}/envolvidos/{id}` | obtém, atualiza ou remove envolvido |
| `POST/GET` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos` | adiciona ou lista endereços |
| `GET/PATCH/DELETE` | `/casos/{casoId}/envolvidos/{envolvidoId}/enderecos/{id}` | obtém, substitui ou remove endereço |
| `POST/GET` | `/casos/{casoId}/contextos` | cria ou lista contextos textuais |
| `GET/PUT/DELETE` | `/casos/{casoId}/contextos/{id}` | obtém, atualiza ou remove contexto |
| `POST/GET` | `/casos/{casoId}/documentos` | upload (`multipart/form-data`) ou lista metadados |
| `GET/DELETE` | `/casos/{casoId}/documentos/{id}` | obtém metadados ou remove documento/arquivo |
| `GET` | `/casos/{casoId}/documentos/{id}/arquivo` | baixa o arquivo binário |
| `GET` | `/casos/{id}/contexto-completo` | agrega Caso, envolvidos, contextos e metadados |
| `POST/GET` | `/casos/{casoId}/pecas` | cria ou lista metadados das Peças do Caso |
| `GET/PATCH/DELETE` | `/pecas/{pecaId}` | obtém, atualiza ou exclui logicamente (`?versao=`) com concorrência otimista |
| `POST` | `/pecas/{pecaId}/exportar` | gera e baixa `.docx` ou `.pdf` da versão salva |
| `POST/GET` | `/templates-peca` | cria ou lista templates de Peça |
| `GET/PATCH/DELETE` | `/templates-peca/{id}` | obtém, atualiza ou exclui logicamente template |

Detalhes de contratos e validação em [Casos](casos.md) e [Peças e templates](pecas.md).

## Assistente de IA

### Rotas compartilhadas e legado

| Método | Rota | Função |
| :--- | :--- | :--- |
| `GET` | `/assistente/capacidades` | disponibilidade, flags e limites visíveis ao usuário |
| `GET` | `/assistente/limite-mensal` | período UTC e percentuais usado/reservado/disponível da organização |
| `GET` | `/assistente/transparencia` | aviso versionado atual e estado de aceite |
| `POST` | `/assistente/transparencia/confirmacoes` | registra aceite da versão atual do aviso |
| `POST` | `/pecas/{pecaId}/assistente/solicitacoes` | fluxo legado v1 em SSE (sem alterar a Peça) |

### Conversa, propostas, jobs e anexos (`/assistente/v2`, JWT organizacional)

| Método | Rota | Função |
| :--- | :--- | :--- |
| `POST` | `/assistente/v2/pecas/{pecaId}/conversa` | cria ou recupera a conversa privada do usuário na Peça |
| `GET` | `/assistente/v2/conversas/{id}/mensagens?antes={seq}&limite=50` | lista mensagens com cursor exclusivo |
| `POST` | `/assistente/v2/conversas/{id}/interacoes` | envia mensagem livre (SSE 200; com `DurableGenerationEnabled`, enfileira job) |
| `POST` | `/assistente/v2/conversas/{id}/mensagens` | adaptador legado com modo/contexto explícitos (SSE 200) |
| `POST` | `/assistente/v2/conversas/{id}/mensagens/{mensagemId}/rejeicao` | rejeita resposta (204) |
| `GET/POST` | `/assistente/v2/conversas/{id}/propostas` | lista propostas ou registra ponte legada |
| `GET` | `/assistente/v2/propostas/{id}` | obtém proposta completa e documento-base para revisão |
| `POST` | `/assistente/v2/propostas/{id}/aplicacoes` | aplica grupos aprovados em transação idempotente e retorna recibo |
| `GET` | `/assistente/v2/conversas/{id}/aplicacoes/{requestId}` | reconciliação de recibo por `requestId` |
| `POST` | `/assistente/v2/propostas/{id}/rejeicoes` | rejeita grupos da proposta |
| `GET` | `/assistente/v2/conversas/{id}/geracoes/{geracaoId}` | acompanha estado/seções do job de geração durável |
| `POST` | `/assistente/v2/conversas/{id}/geracoes/{geracaoId}/cancelamento` | cancela geração (204) |
| `POST` | `/assistente/v2/conversas/{id}/geracoes/{geracaoId}/retomada` | solicita retomada explícita (202) |
| `GET` | `/assistente/v2/conversas/{id}/analises/{analiseId}` | acompanha estado/lotes/cobertura da análise documental |
| `POST` | `/assistente/v2/conversas/{id}/analises/{analiseId}/cancelamento` | cancela análise (204) |
| `POST` | `/assistente/v2/conversas/{id}/analises/{analiseId}/retomada` | solicita retomada da análise (202) |
| `POST/GET` | `/assistente/v2/conversas/{id}/anexos` | envia anexo privado (`202`) ou lista estados/qualidade |
| `GET` | `/assistente/v2/conversas/{id}/anexos/{anexoId}` | estado, progresso e problemas de leitura do anexo |
| `GET` | `/assistente/v2/conversas/{id}/anexos/{anexoId}/arquivo` | download privado autenticado |
| `POST` | `/assistente/v2/conversas/{id}/anexos/{anexoId}/reprocessar` | solicita reprocessamento autorizado (`202`) |
| `GET` | `/assistente/v2/conversas/{id}/fontes/busca?consulta=...&limite=12` | busca filtrada nos trechos elegíveis |

### Histórico pessoal e exclusão (`/assistente/v2/pessoal`, sessão `PersonalHistory`)

| Método | Rota | Função |
| :--- | :--- | :--- |
| `GET` | `/assistente/v2/pessoal/conversas?antes=...&limite=30` | lista conversas do titular |
| `GET` | `/assistente/v2/pessoal/conversas/{id}` | detalhes da conversa |
| `GET` | `/assistente/v2/pessoal/conversas/{id}/mensagens?antes=...&limite=50` | mensagens da conversa |
| `GET` | `/assistente/v2/pessoal/conversas/{id}/anexos` | anexos privados da conversa |
| `GET` | `/assistente/v2/pessoal/conversas/{id}/anexos/{anexoId}/arquivo` | download privado do anexo |
| `GET` | `/assistente/v2/pessoal/propostas/{id}` | consulta proposta histórica |
| `DELETE` | `/assistente/v2/pessoal/conversas/{id}` | solicita expurgo (`requestId` + `confirmarExclusao:true`, retorna `202`) |
| `GET` | `/assistente/v2/pessoal/exclusoes` e `/exclusoes/{requestId}` | acompanha protocolos de exclusão |

Detalhes de regras e comportamento em [Assistente de IA](assistente-ia.md).

## Exemplo de registro (`POST /auth/register`)

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

Cria atomicamente Organization, User (`Owner`) e Session inicial (`201`), definindo
o cookie `juridicoai_refresh`. E-mail duplicado retorna `409` (`EMAIL_ALREADY_EXISTS`).

Exemplos executáveis também estão em `src/JuridicoAI.Api/JuridicoAI.http`.
