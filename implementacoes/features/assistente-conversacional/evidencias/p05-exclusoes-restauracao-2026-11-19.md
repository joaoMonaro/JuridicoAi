# P05 — Exclusões após restauração (2026-11-19)

Branch `feature/p05-exclusoes-restauracao`, criada de `origin/homologacao` em
`8cac6db` (P04 integrado). Entrega técnica local; volume/backup em nuvem e
homologação com o advogado no MVP online continuam pendentes.

## Implementado

- Registro independente assinado HMAC-SHA256, com escopo exclusivo do ambiente,
  substituição atômica, flush do arquivo/diretório em Linux e bloqueio entre
  escritores. Somente requestId, userId, conversaId e data da autorização;
  nenhum conteúdo, nome ou caminho de anexo. Permissões locais 0700/0600.
- Configuração `DeletionProtection`: diretório independente fora da aplicação e
  dos documentos, chave Base64 de pelo menos 32 bytes, ScopeId e MaintenanceMode.
  Produção exige configuração. Registro ausente/inválido nunca é recriado
  automaticamente; a inicialização é um comando explícito.
- Autorização local bloqueia a conversa/cancela execuções; confirmação HTTP ocorre
  após a gravação externa, fora da transação do banco. Falha mantém protocolo e
  conteúdo bloqueados, com estado público `PendenteDeRetomada` e código
  `AI_DELETION_JOURNAL_PENDING`. Reenvio e worker retomam a gravação sem duplicar.
- `journaled_at` distingue a gravação protegida. O expurgo exige confirmação no
  registro assinado e valida titularidade/namespace/referências compartilhadas
  antes de apagar arquivos. Remoção local rejeita links simbólicos.
- Comandos de inicialização/exportação de todos os protocolos (inclusive antigos
  concluídos), inspeção do hash, reaplicação idempotente e verificação final.
- Recuperação exige hash do registro atual, UUID da execução, manutenção,
  confirmação de isolamento/drenagem e evidência. Revoga sessões restauradas,
  reconstrói filas pelos metadados e arquivos privados, preserva requestIds e
  confere ausência final de conversa/derivados/arquivos antes de concluir.
- `ai_restore_state` mantém o bloqueio através de falhas/reinícios. Inicialização
  verifica o registro e conteúdo restaurado; middleware e workers comuns também
  respeitam o bloqueio persistente. `/health/live` permanece disponível.
  O exportador de pendências não importa protocolos de um ambiente não verificado.
- Arquivos órfãos e backup de arquivos mais antigo que protocolo concluído são
  reaplicados; Peças, fontes compartilhadas do Caso e outros chats são preservados.
- Migration aditiva `20261119124332_AddAssistantDeletionRecovery`. Downgrade que
  apagaria proteção ou estado de recuperação existente é bloqueado.

## Validação

Suíte final: **351 testes aprovados, 1 de OCR ignorado, 0 falhas** (352 no total),
com PostgreSQL descartável e o ensaio de backup habilitado. A rodada final foi
sequencial; os 18 testes novos estão incluídos nesse total.

- Seis testes do registro: conteúdo mínimo/idempotência, concorrência de instâncias,
  adulteração/omissão/chave/escopo, ausência sem reinicialização, diretórios/links
  inválidos e catálogo/remover arquivos sem atravessar links ao material compartilhado.
- Onze testes PostgreSQL: falha externa antes da confirmação, reaplicação e
  sessões, arquivo restaurado sobre protocolo concluído, usuário ausente no backup,
  titular divergente, arquivo compartilhado, confirmações/hash, registro desatualizado,
  fence HTTP/workers, falha/retomada do expurgo, verificação de inicialização e
  exportação de protocolo legado concluído (cenários agrupados em onze testes).
- Ensaio automatizado com `pg_dump -Fc`/`pg_restore` e cópia dos arquivos reais da
  fixture: backup anterior à autorização, exclusão no ambiente ativo e restauração
  conjunta. A abertura é recusada antes da reaplicação. Peça aprovada, fonte do Caso
  e outro chat permanecem iguais; sessões são revogadas e o requestId é mantido.
  Repetição e restauração apenas dos arquivos privados também passaram.
- Ensaio CLI/HTTP com API em `Testing`, PostgreSQL descartável e IA desativada:
  inicializar → inspecionar → verificar → reaplicar → reiniciar; manutenção exigida;
  registro removido provoca 503 nas rotas comuns e liveness 200, sem recriar arquivo.
- Build Release sem erros/avisos; EF sem diferenças pendentes de modelo/migration.
- Uma rodada intermediária encontrou timeout de conexão Npgsql em um teste
  preexistente; a conferência final usa execução sequencial no servidor descartável.

Nenhuma chamada real à IA foi feita. O teste de backup exige os dois parâmetros de
PostgreSQL/container descartáveis e não é executado contra um ambiente de produto.

## Condições operacionais restantes

Provisionar volume independente e segredo/escopo por ambiente; proteger contra
perda/troca por registro antigo e obter o hash de uma fonte atual confiável.
Uma assinatura não prova atualidade de uma cópia antiga válida.

O registro não expira automaticamente (100 mil entradas/32 MiB). Manter suas
entradas durante toda a existência de backups que contenham o conteúdo. Definir
retenção, RPO/RTO e ensaiar novamente com banco/storage e credenciais do deploy.
O código não provisiona serviço externo nem apaga cópias históricas: a resposta
continua `RemovidaDoAmbienteAtivo`, com `EliminadaDasCopias=false`.

Configuração e comandos no [guia operacional](../../../../docs/operacao/exclusao-restauracao.md).
