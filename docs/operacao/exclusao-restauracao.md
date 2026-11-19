# Exclusões após restauração — P05

Cada exclusão confirmada grava um registro independente com requestId, userId,
conversaId e data. O arquivo é assinado, gravado de forma atômica e não contém
mensagens, nomes de anexos ou caminhos. Falha nessa gravação retorna 503; a conversa
já autorizada fica bloqueada e a gravação pendente pode ser retomada. A consulta
do protocolo informa `PendenteDeRetomada`/`AI_DELETION_JOURNAL_PENDING` enquanto
o registro independente não estiver confirmado.

## Configurar uma vez por ambiente

Provisione um diretório privado em volume **independente dos backups de banco e
documentos**. Um caminho diferente no mesmo disco não garante independência física.
Ele deve permitir escrita, renomeação atômica e flush durável. O ensaio foi em Linux.

Configure `DeletionProtection` por variáveis/secret manager em produção ou
user-secrets em desenvolvimento:

| Opção | Valor |
|---|---|
| `JournalDirectory` | caminho absoluto do volume, fora da aplicação/documentos, sem links simbólicos |
| `IntegrityKeyBase64` | chave Base64 de pelo menos 32 bytes, gerada uma vez com `openssl rand -base64 32` |
| `ScopeId` | UUID exclusivo e estável do ambiente |
| `MaintenanceMode` | `false` normalmente; `true` durante recuperação |

Guarde a chave e o escopo fora dos backups de conteúdo. Não regenere a chave ao
reiniciar. Produção exige essa configuração; localmente, sem ela, as outras funções
continuam disponíveis, mas exclusões não recebem confirmação protegida.

Na primeira instalação/upgrade, com banco configurado:

```bash
AI_CONFIRM_NEW_JOURNAL=true dotnet run --project src/JuridicoAI.Api -- initialize-deletion-journal
```

O comando também exporta protocolos antigos, inclusive concluídos. Para repetir
somente essa exportação em um ambiente íntegro:

```bash
dotnet run --project src/JuridicoAI.Api -- export-deletion-authorizations
```

**Nunca inicialize um registro novo ou exporte o banco antigo durante uma
restauração.** Use o registro independente atual. A assinatura detecta adulteração;
a proteção do volume e um hash obtido de fonte atual confiável impedem usar uma
cópia antiga assinada como se fosse a mais recente.

## Restaurar

1. Isole o tráfego e encerre/drene todas as instâncias e workers. Restaure banco e
   arquivos com o procedimento de backup do ambiente, sem sobrescrever o registro
   independente nem sua chave.
2. Confira o registro atual no volume independente:

```bash
dotnet run --project src/JuridicoAI.Api -- inspect-deletion-journal
```

3. Use o SHA256 exibido, um UUID para a execução e uma evidência de isolamento:

```bash
DeletionProtection__MaintenanceMode=true \
AI_RESTORE_RUN_ID='UUID_DA_EXECUCAO' \
AI_RESTORE_JOURNAL_SHA256='SHA256_CONFERIDO' \
AI_CONFIRM_RESTORE_ISOLATED=true \
AI_CONFIRM_WORKERS_DRAINED=true \
AI_RESTORE_EVIDENCE='Ambiente isolado e processos anteriores encerrados' \
dotnet run --project src/JuridicoAI.Api -- restore-assistant-deletions
```

O comando aplica migrations, revoga sessões organizacionais/pessoais restauradas,
reconstrói as exclusões pelos arquivos/metadados restaurados e executa o expurgo.
Conserva os requestIds; repetir a mesma execução é seguro. Falha deixa um bloqueio
persistente: corrija a causa e repita com o mesmo UUID. Não altere o bloqueio por SQL.

4. Confira o resultado antes de reabrir:

```bash
dotnet run --project src/JuridicoAI.Api -- verify-restored-deletions
```

5. Reinicie com `MaintenanceMode=false`. A inicialização confere o registro, o
   banco e os arquivos antes de liberar tráfego e workers comuns. Conteúdo
   ressuscitado, registro ausente/inválido ou recuperação incompleta mantêm HTTP
   503; `/health/live` continua disponível. Reiniciar é necessário após recuperar.

Esses comandos são administrativos e não iniciam servidor HTTP ou chamadas à IA.
Em um deploy publicado, use o binário e as configurações desse ambiente.

## Preservação e retenção

- Peças aprovadas, fontes compartilhadas do Caso e outros chats permanecem intactos.
  Divergências de titularidade ou arquivo compartilhado interrompem a recuperação.
- Arquivos privados órfãos também são conferidos pelo namespace do conversaId,
  mesmo quando o backup não contém mais os metadados ou o usuário.
- O registro não expira automaticamente. Mantenha-o durante toda a existência de
  backups que possam conter o conteúdo; não remova entradas manualmente. Limite
  inicial: 100 mil autorizações/32 MiB; ao atingir o limite, a confirmação falha.
- Definir retenção dos backups, destino independente, perda de dados e tempo de
  recuperação no deploy. Este código não provisiona storage ou backup em nuvem.
- Downgrade que apagaria proteção/recuperação existente é bloqueado. Rollback da
  aplicação deve preservar a verificação P05.
- A conclusão informa apenas `RemovidaDoAmbienteAtivo`; não afirma que as cópias
  históricas foram apagadas. Retenção/expurgo das cópias exigem verificação própria.

O ensaio automatizado usa PostgreSQL descartável, `pg_dump`/`pg_restore`, arquivos
fictícios e nenhum provedor de IA. Para executá-lo junto da suíte, configure
`JURIDICOAI_REVIEW_TEST_POSTGRES` e `JURIDICOAI_REVIEW_POSTGRES_CONTAINER` apontando
para o mesmo container descartável PostgreSQL, com usuário de testes `postgres`.
A homologação com o advogado será feita com o MVP online, em acesso restrito.
