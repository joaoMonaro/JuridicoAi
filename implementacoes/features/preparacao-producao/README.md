# Preparação para produção e testes locais

> **Estado:** implementação parcial do monitoramento P04; demais frentes em planejamento.
> **Atualizado em:** 2026-11-19.
> **Acompanhamento:** [tarefas e critérios de aceite](TODO.md).
> **Escopo do produto:** [MVP de redação de peças jurídicas](../../../MVP.md).

## Objetivo

Preparar o JuridicoAI para operar em nuvem com dados jurídicos reais,
preservando uma versão configurável para desenvolvimento e testes locais.
O objetivo é usar o mesmo código, com configurações, credenciais e dados
separados por ambiente.

O monitoramento P04 tem implementação e ensaio local registrados em sua
[evidência](../assistente-conversacional/evidencias/p04-monitoramento-2026-11-19.md).
As demais frentes continuam em planejamento, sem contratação, deploy ou testes
pagos realizados por esta entrega. Marcar tarefas somente após sua verificação.

## Decisões confirmadas

- O servidor de destino será em nuvem; o provedor ainda não foi escolhido.
- A validação com o advogado ocorrerá com o MVP online.
- P05 e P06 estão adiados para depois do MVP.
- A preparação deve preservar a possibilidade de executar e configurar o
  projeto localmente para testes.
- Chamadas reais à IA em testes ficam desativadas por padrão e só podem ser
  executadas quando explicitamente solicitadas pelo responsável pelo projeto.
- A presença de uma chave, de um modelo configurado ou da habilitação de IA
  na aplicação não autoriza testes pagos.
- A implementação iniciada limita-se ao monitoramento P04; as demais frentes
  permanecem em planejamento.

## Política de testes com IA

Esta regra vale para testes automatizados, avaliações, scripts de verificação
e execuções manuais feitas para validar a implementação, em qualquer ambiente.

| Execução | Comportamento planejado |
|---|---|
| Build, inicialização de testes e suíte padrão | Nenhuma chamada real ao provedor |
| Testes unitários e HTTP | IA simulada; nenhum consumo de tokens |
| Integração com PostgreSQL | Banco descartável e IA simulada |
| Testes com IA real | Execução separada e habilitação explícita para a rodada solicitada |
| Uso normal da aplicação | Configuração própria do ambiente; sua habilitação não se propaga aos testes |

Para uma rodada real, registrar solicitação, ambiente, cenários, modelo,
tarifas conferidas e orçamento autorizado antes da execução. A confirmação
técnica no comando complementa a solicitação do responsável; não a substitui.
Sem habilitação explícita ou sem orçamento definido, a rodada não deve começar.
Resultados salvos devem poder ser analisados novamente sem repetir chamadas.

O bloqueio deve ocorrer antes de qualquer chamada ao provedor, inclusive
contagem de tokens remota, diagnóstico ou tentativa automática com outro modelo.
Credenciais herdadas do terminal, user-secrets ou configurações de produção
não devem alterar o comportamento da suíte padrão.

Há mecanismos existentes a reaproveitar nos avaliadores:
[avaliação conversacional](../../../avaliacoes/assistente-conversacional/README.md)
e [qualidade jurídica](../../../avaliacoes/qualidade-juridica/README.md).
A cobertura de todos os caminhos e a forma final de habilitação ainda precisam
ser definidas e verificadas durante a implementação.

## Organização proposta dos ambientes

A tabela descreve o comportamento desejado, ainda sem configurar os ambientes.
Os nomes finais dos perfis e das opções serão definidos no desenho técnico.

| Aspecto | Desenvolvimento/testes locais | Homologação em nuvem | Produção |
|---|---|---|---|
| Transporte | HTTP em loopback permitido | HTTPS | HTTPS |
| Origem do frontend | Localhost conforme configuração local | Lista explícita de origens | Lista explícita de origens |
| Cadastro | Configurável para cenários locais | Restrito aos participantes autorizados | Restrito ao piloto, sujeito à definição da política |
| Dados e credenciais | Recursos de teste separados | Dados fictícios e segredos próprios | Dados reais e segredos próprios |
| Banco | PostgreSQL local ou descartável | Banco separado, sem acesso público | Banco privado |
| Documentos | Diretório local configurável | Storage persistente de homologação | Storage persistente e privado |
| Migrations | Automáticas quando explicitamente configuradas | Aplicação controlada e ensaiada | Aplicação controlada |
| IA em testes | Simulada por padrão; real só por solicitação | Mesma regra de autorização | Mesma regra de autorização |

O uso de IA real na aplicação local será uma opção a definir. Sua eventual
habilitação deve continuar independente dos testes e usar credenciais e limites
próprios. Nenhum teste deve apontar para dados de produção por padrão.

## Frentes de trabalho

1. Definir configurações por ambiente e os bloqueios contra testes pagos
   acidentais.
2. Proteger cookies, restringir origens, definir cadastro e validar configurações
   obrigatórias antes de a API atender requisições.
3. Escolher a infraestrutura e preparar publicação, persistência e segredos.
4. Implementar verificações de disponibilidade e monitoramento operacional.
5. Preparar backup e restauração básicos; proteção automática das exclusões (P05) adiada.
6. Ensaiar deploy/rollback e validar a jornada do MVP antes de liberar o piloto.

Os critérios verificáveis e as dependências estão no [TODO](TODO.md).

## Relação com o assistente conversacional

Esta feature organiza a preparação transversal do projeto. As condições
específicas da IA permanecem no
[TODO do assistente](../assistente-conversacional/TODO.md):

| Frente do assistente | Relação com este planejamento |
|---|---|
| P03 — Controle financeiro | Configurar tarifas/limites reais por ambiente; aproveitar o controle já implementado |
| P04 — Monitoramento | Implementação local disponível; configurar canal/responsável e validar a operação do ambiente |
| P05 — Backup/exclusão | Em espera, fora do MVP; código preservado em branch separada |
| P06 — Rollout | Em espera, fora do MVP; controles existentes de acesso e isolamento permanecem |
| P07/P01/P02 — Compatibilidade e homologação | Comprovar o fluxo e revisar resultados antes da liberação |
| P08 — Implantação | Ensaiar migrations, drenagem de workers, deploy e rollback |

Atualizar as referências quando uma entrega compartilhada for concluída. Este
plano não marca essas pendências como resolvidas nem transfere aprovações de
avaliações antigas para uma nova versão.

## Base da análise

A análise realizada nesta conversa avaliou o backend no commit `9da8a1b`:
build e publicação em Release concluídos; 324 testes aprovados, zero falhas
e um teste de OCR ignorado, com PostgreSQL descartável. A consulta ao NuGet
não reportou vulnerabilidades conhecidas naquela execução. Não houve chamada
paga ao modelo nessa análise.

Foram observados, em uma instância local em modo Production, cookie de refresh
sem `Secure`, cadastro anônimo ativo, CORS aceitando localhost com credenciais
e resposta 200 na raiz mesmo sem banco configurado. O Compose publica a porta
do PostgreSQL sem restringir o IP de bind; a exposição efetiva depende da rede.
Esses pontos orientam as tarefas, sem afirmar que a infraestrutura futura já
está configurada ou foi auditada.

Os números acima são evidência histórica dessa revisão, não uma nova execução
de testes por esta documentação nem certificação do frontend ou da nuvem.

## Decisões em aberto

- Provedor, região, modalidade de hospedagem, domínio e orçamento de operação.
- Necessidade de homologação permanente ou criada sob demanda.
- Volume persistente ou armazenamento de objetos para os documentos; confirmar
  compatibilidade com o storage local usado atualmente.
- Política final de cadastro e provisionamento do primeiro usuário.
- Perfis locais necessários e experiência de habilitação de IA real na aplicação.
- Comando/opção para testes reais, orçamento por rodada e mecanismo para bloquear
  saídas ao provedor na suíte comum.
- Retenção dos backups, RPO/RTO, destino independente do registro de exclusões
  e limiares dos alertas. RPO define perda de dados tolerável; RTO, tempo de recuperação.

Uma instância da API com banco gerenciado privado é uma proposta inicial para o
piloto de um advogado, ainda sem escolha de serviço. Múltiplas instâncias exigem
definição de storage compartilhado e limites agregados de requisições.

## Condição para liberação

P05 (volume independente/restauração) e P06 (gate adicional de rollout) foram
adiados em 2026-11-19 e não bloqueiam o MVP. Ver [decisões](../../../docs/decisoes-mvp.md).

Concluir as tarefas de segurança, isolamento, persistência, recuperação e
operação; registrar os ensaios; cumprir os critérios do MVP e validar a jornada
com o advogado. Planejamento, build e testes automatizados isoladamente não
declaram o ambiente liberado para produção.
