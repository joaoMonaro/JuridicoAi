# Solução de problemas e limitações

## A API inicia, mas uma rota do MongoDB falha

Sintoma comum:

```text
Configure Mongo:ConnectionString ou a variavel Mongo__ConnectionString.
```

Defina a conexão antes de usar ingestão, consulta ou reprocessamento:

```bash
export Mongo__ConnectionString="mongodb://usuario:senha@localhost:27017/?authSource=admin"
```

Confirme também se o contêiner está ativo e se usuário, senha, porta e
`authSource` correspondem ao Compose.

## Timeout de seleção do MongoDB

O padrão de `Mongo:ServerSelectionTimeoutSeconds` é 10 segundos. Verifique:

- disponibilidade do servidor;
- resolução de nome e porta;
- firewall/rede do contêiner;
- credenciais;
- replica set e parâmetros adicionais da connection string.

Aumentar o timeout pode ajudar em redes lentas, mas não corrige conexão
inválida.

## Falha ao aplicar migrations do PostgreSQL

As migrations da fundação do caso são gerenciadas pelo Entity Framework Core
e aplicadas na inicialização quando `Postgres:RunMigrations=true`. Verifique:

- se `Postgres:ConnectionString` aponta para o banco e a porta corretos;
- credenciais e disponibilidade do PostgreSQL;
- permissão do usuário para criar ou alterar tabelas e índices;
- os logs de `DatabaseMigrationService` e do EF Core;
- o conteúdo de `__EFMigrationsHistory`.

Em um banco criado pelo mecanismo antigo, confirme que `schema_migrations`
contém `001_fundacao_caso.sql`. Essa entrada permite que a API reconheça o
schema existente e registre a migration inicial no histórico do EF sem recriar
as tabelas.

Não apague entradas de `__EFMigrationsHistory` nem execute novamente a
migration inicial manualmente em um banco com dados. Antes de corrigir uma
divergência de schema, faça backup e compare a estrutura real com a migration
em `Infrastructure/Database/Migrations`.

## Chromium ou Playwright não encontrado

O scraper tenta localizar instalações conhecidas do Chromium/Chrome. Quando
nenhuma existe, instale o browser do Playwright após compilar:

```bash
dotnet build
pwsh src/JuridicoAI.Scrapper/bin/Debug/net10.0/playwright.ps1 install chromium
```

Em Linux, erros sobre bibliotecas compartilhadas indicam dependências de
sistema ausentes.

## Timeout ou falha ao abrir o TJSP

A criação da sessão possui timeout interno de 30 segundos. Quando o scraper é
chamado pela API, toda a coleta também é limitada por
`Scraper:TimeoutSeconds`, padrão 120.

Verifique:

- conectividade com `https://esaj.tjsp.jus.br`;
- indisponibilidade ou lentidão do portal;
- bloqueio automatizado/captcha;
- compatibilidade da versão do Chromium;
- seletores e token do reCAPTCHA documentados em
  [Integração com o TJSP](../scraper/integracao-tjsp.md).

Na API, o estouro do timeout total retorna HTTP 504. Na CLI, a falha é escrita
em `stderr` e o processo termina com código 1.

## TJSP informa resultados, mas o scraper retorna zero

Isso normalmente indica alteração no HTML. O parser depende de
`totalResultadoAba-*`, `fundocinza1`, `downloadEmenta` e outros padrões.

Use uma coleta com `--raw-html` para preservar a primeira página e compare sua
estrutura com `TjspResultadoParser`. Lembre-se de que uma página de bloqueio
pode responder HTTP 200 e ainda assim não conter os elementos esperados.

## Campos retornam vazios

O parser tolera dados ausentes. Número de processo, ementa e link podem virar
string vazia; vários metadados podem virar `null`. Alterações de rótulos ou
marcação HTML são a causa mais provável quando isso ocorre em massa.

Datas inválidas não interrompem a ingestão: na collection processada elas ficam
como `null`.

## Menos itens do que o solicitado

Possíveis causas:

- o TJSP possui menos resultados;
- itens sem processo e sem ementa são descartados;
- alguma página retornou estrutura inesperada;
- o teto de 200 foi aplicado;
- houve cancelamento ou erro antes de concluir a paginação.

## Itens aparecem como duplicados

A chave de deduplicação é `fonte + numeroProcesso + link`. Duplicados não são
normalizados ou classificados novamente durante ingestão. Use a rota de
reprocessamento para aplicar regras atuais ao raw já salvo.

## Falha ao criar índices

Os índices únicos podem falhar se o banco já contiver duplicatas. Faça backup,
identifique documentos com a mesma chave e saneie-os antes de repetir a
ingestão. O projeto não possui migração automática para esse cenário.

## Limitações de segurança e produção

A API já exige autenticação JWT nas rotas privadas e aplica isolamento por
Organization aos dados jurídicos. Isso não torna a configuração de
desenvolvimento pronta para produção. Antes de expor a aplicação:

- defina `Auth:JwtSecret` e demais segredos em um secret manager apropriado;
- restrinja CORS às origens reais e configure HTTPS/reverse proxy;
- mantenha dados brutos, arquivos e rotas administrativas sob controle de acesso;
- aplique a política de retenção e exclusão de dados e backups;
- valide que logs e telemetria não recebem conteúdo jurídico ou credenciais;
- configure preços, orçamento, métricas, dashboards e alertas da IA;
- execute o rollout por Organization e preserve o kill switch testado;
- conclua as revisões jurídicas de LGPD, contratos, suboperadores e
  transferência internacional;
- revise termos de uso e políticas do portal consultado antes de reativar o
  scraper em um fluxo de produto.

O roteiro detalhado do assistente está em
[Segurança operacional](../../implementacoes/features/assistente-ia/evidencias/seguranca-operacional-producao.md).

## O assistente não aparece ou informa indisponibilidade

Verifique, nesta ordem:

1. `AssistenteIa:Enabled=true`, com `ApiKey` e `Model` válidos;
2. kill switch persistido `AI_ENABLED=true`;
3. flag `AI_ASSISTANT` habilitada para a Organization;
4. flag específica da operação (`AI_EDIT`, `AI_REVIEW` ou
   `AI_DOCUMENT_GENERATION`), quando aplicável;
5. confirmação da versão atual do aviso de transparência pelo usuário.

Ausência de uma flag equivale a desabilitado. Os comandos administrativos estão
em [User Secrets](../user-secrets.md). Não envie a saída de `user-secrets list`
para logs ou suporte, pois ela contém valores completos.

## Diagnóstico recomendado

1. Execute `dotnet build`.
2. Teste o scraper pela CLI com `--max 1`.
3. Confirme o JSON antes de envolver MongoDB.
4. Teste a conexão do MongoDB.
5. Faça uma ingestão com um limite pequeno.
6. Consulte raw e processado.
