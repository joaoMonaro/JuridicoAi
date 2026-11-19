# Decisões do MVP

Atualizado em **2026-11-19**. Escopo vigente: [MVP](../MVP.md).

| Tema | Decisão |
|---|---|
| Uso inicial | Um advogado empresarial, com fatos e anexos pelo chat, revisão/aprovação e exportação Word/PDF. |
| Validação com o advogado | Será feita com o MVP online. Conferências técnicas e correções da jornada existente continuam necessárias. |
| P05 — exclusões após restauração | Adiado, fora do MVP. Volume independente e reaplicação automática não bloqueiam o lançamento. Código preservado em `feature/p05-exclusoes-restauracao`, sem integração em `homologacao`. |
| P06 — rollout por organização | Adiado, fora do MVP. Não implementar o gate adicional de habilitação por organização nesta entrega. |
| Controles existentes | Autenticação, isolamento dos dados, permissões e desligamento da IA permanecem no escopo. Exclusão do ambiente ativo e backup básico também. |
| P04 — monitoramento | Implementado e integrado em `homologacao`; configurar a operação no ambiente de implantação. |

## Próximo trabalho

Preparar a implantação online (**P08 / preparação para produção**): escolher
hospedagem e configurar HTTPS, banco, arquivos persistentes, segredos, tarifas da
IA e monitoramento. Conferir a jornada já implementada (**P07**), corrigindo os
impedimentos necessários ao uso.

Após colocar o MVP online, validar com o advogado geração, aprovação, edição,
salvamento, retomada e exportação, e registrar os ajustes encontrados. Provedor,
região, domínio e orçamento de hospedagem ainda precisam ser definidos.

Acompanhar no [TODO geral](../TODO.md), [TODO do assistente](../implementacoes/features/assistente-conversacional/TODO.md)
e [TODO de preparação para produção](../implementacoes/features/preparacao-producao/TODO.md).
