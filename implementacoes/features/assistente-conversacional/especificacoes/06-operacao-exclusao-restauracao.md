# Exclusão e restauração — procedimento operacional

Complemento técnico de 2026-11-12. A remoção autorizada no banco/armazenamento ativo
está implementada. Este documento não comprova expurgo de backups nem executa
restauração em infraestrutura do produto.

## Antes da ativação em produção

- Aplicar as migrations e drenar binários/workers anteriores. Conferir temporários
  legados de extração sem identificador de conversa; o novo worker usa um namespace
  privado por conversa para permitir seu expurgo autorizado.
- Configurar HTTPS, origens pessoais permitidas e frontend/API no mesmo site.
- Garantir volume persistente e permissões do usuário de serviço para exclusão dos
  arquivos originais e temporários. Falhas mantêm a conversa bloqueada e o protocolo
  pendente, sem anunciar conclusão.
- Monitorar protocolos `PendenteDeRetomada` e idade de solicitações ainda abertas.
  Não registrar texto, títulos, nomes de anexos, credenciais ou payloads de chat.
- Definir e verificar retenção das cópias de segurança. Os prazos de 24h/30 dias do
  desenho original são propostas, sem SLA implementado ou contratado.

## Registro externo mínimo

Manter uma cópia protegida e independente dos backups de conteúdo das autorizações
já aceitas. Campos mínimos: requestId, userId, conversaId e autorizadaEm. Exportar
as autorizações aceitas, inclusive pendentes, não somente remoções concluídas.
Não incluir mensagens, título, nome/caminho de arquivo ou documentos. A integridade,
autenticidade e disponibilidade desse registro precisam de validação operacional;
esta implementação não provisiona um serviço externo ou política de backup.

O protocolo da aplicação permanece no próprio banco. Sozinho ele não impede que
uma restauração anterior ao pedido de exclusão ressuscite conteúdo; por isso o
registro externo e o procedimento abaixo são condições para reabrir um ambiente
restaurado. Não alterar `eliminadaDasCopias` apenas porque o banco ativo foi limpo.

## Sequência obrigatória de restauração

1. Isolar o ambiente de tráfego público; pausar geração, análise e extração.
2. Restaurar banco e armazenamento e aplicar as migrations atuais ainda em
   manutenção. Revogar sessões restauradas para não reabrir sessões já encerradas.
3. Importar e verificar o registro externo das exclusões aceitas depois do ponto
   restaurado. Associar cada registro ao par exato titular/conversa.
4. Recolocar essas conversas em `EmExclusao`, invalidar leases e reconstruir os
   trabalhos de remoção a partir dos anexos privados restaurados. Revalidar o
   namespace de armazenamento; nunca remover fontes compartilhadas do Caso.
   Mesmo um protocolo restaurado como concluído precisa ser reaplicado se seu
   conteúdo voltou pelo backup de arquivos/banco.
5. Executar o worker de exclusão isolado, preservando os requestIds autorizados.
   Conferir protocolos concluídos, ausência de conteúdo/derivados/arquivos privados
   e preservação das Peças/documentos compartilhados. Não abrir a aplicação enquanto
   houver conteúdo de conversas cuja exclusão já estava autorizada.
6. Só então liberar autenticação/tráfego e retomar o processamento normal.

A automatização e o ensaio dessa sequência com o backup real do ambiente ainda são
pendências operacionais. Até comprovar a remoção das cópias ou o fim verificado da
retenção, informar somente **RemovidaDoAmbienteAtivo**.
