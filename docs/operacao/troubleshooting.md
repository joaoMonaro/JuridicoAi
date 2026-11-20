# Solução de problemas e limitações

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

## Limitações de segurança e produção

A API já exige autenticação JWT nas rotas privadas e aplica isolamento por
Organization aos dados jurídicos. Isso não torna a configuração de
desenvolvimento pronta para produção. Antes de expor a aplicação:

- defina `Auth:JwtSecret` e demais segredos em um secret manager apropriado;
- restrinja CORS às origens reais e configure HTTPS/reverse proxy;
- mantenha arquivos e rotas administrativas sob controle de acesso;
- aplique a política de retenção e exclusão de dados e backups;
- valide que logs e telemetria não recebem conteúdo jurídico ou credenciais;
- configure preços, orçamento, métricas, dashboards e alertas da IA;
- mantenha o cadastro fechado no piloto e preserve o kill switch testado;
- conclua as revisões jurídicas de LGPD, contratos, suboperadores e
  transferência internacional.

O roteiro detalhado do assistente está em
[Segurança operacional](../../implementacoes/standBy/assistente-ia/evidencias/seguranca-operacional-producao.md).

## O assistente não aparece ou informa indisponibilidade

Verifique, nesta ordem:

1. `AssistenteIa:Enabled=true`, com `ApiKey` e `Model` válidos;
2. kill switch persistido `AI_ENABLED=true`;
3. Organization ativa e sem bloqueio administrativo em `AI_ASSISTANT`;
4. ausência de bloqueio administrativo na permissão da operação (`AI_EDIT`,
   `AI_REVIEW` ou `AI_DOCUMENT_GENERATION`), quando aplicável;
5. confirmação da versão atual do aviso de transparência pelo usuário.

As quatro permissões atuais são habilitadas por padrão para Organizations ativas;
registros explícitos `false` bloqueiam o recurso. Os comandos administrativos estão
em [User Secrets](../user-secrets.md). Não envie a saída de `user-secrets list`
para logs ou suporte, pois ela contém valores completos.

## Diagnóstico recomendado

1. Execute `dotnet build`.
2. Confirme que o PostgreSQL está disponível e a connection string está configurada.
3. Inicie a API e confira os logs de inicialização e migrations.
4. Valide `GET http://localhost:5299/`.
5. Autentique-se e confira o acesso aos próprios Casos e Peças.
6. Para problemas no assistente, confira a configuração e as permissões descritas acima.
