# TODO — Preparação para produção e testes locais

Atualizado em **2026-11-20**. **Estado: monitoramento P04 parcial e acesso P06 simplificado implementado.**
Referência: [escopo, decisões confirmadas e questões em aberto](README.md).

## Decisões registradas

- [x] Registrar o destino em nuvem, preservando configuração e testes locais.
- [x] Registrar a exigência de solicitação explícita para testes com IA real.
- [x] Separar neste plano decisões confirmadas, propostas e escolhas pendentes.

Os itens acima representam documentação. P04 possui implementação e ensaio local;
P06 simplificado tem código e testes concluídos. As demais frentes e as condições
operacionais indicadas abaixo continuam pendentes.

## Etapa 1 — Ambientes e controle dos testes pagos

### PROD-001 — Definir configuração por ambiente

- [ ] Definir perfis locais, homologação e produção, com precedência documentada
  entre arquivos, variáveis e user-secrets.
- [ ] Manter o mesmo código e separar bancos, arquivos, credenciais e limites.
- [ ] Preservar execução local com HTTP/localhost e opções locais de cadastro e migrations.
- [ ] Preparar exemplos de configuração sem valores sensíveis e comandos de execução.
- [ ] Validar a inicialização local e de produção sem usar dados reais.

### PROD-002 — Impedir consumo acidental de tokens em testes

- [ ] Separar a suíte padrão dos cenários com IA real; reutilizar os avaliadores existentes.
- [ ] Fazer a suíte padrão usar somente provedores simulados, inclusive com chave
  real presente no ambiente e IA habilitada na configuração da aplicação.
- [ ] Bloquear chamadas ao provedor antes do envio, incluindo contagem remota
  de tokens e tentativas automáticas.
- [ ] Exigir comando separado, habilitação explícita e orçamento para cada rodada
  real solicitada pelo responsável pelo projeto.
- [ ] Conferir autorização, ambiente, modelo, tarifas, cenários e limites no preflight,
  sem gerar consumo; não iniciar com requisitos ausentes.
- [ ] Não disparar testes reais automaticamente em build, inicialização, testes comuns,
  CI ou deploy.
- [ ] Verificar os bloqueios com testes sem acesso ao provedor e documentar o procedimento.

**Aceite:** um comando comum de testes não pode consumir tokens, mesmo com
credenciais disponíveis. A validação desta proteção não exige rodada paga.
Uma rodada real futura depende de solicitação explícita e orçamento definido.

## Etapa 2 — Segurança da API

### PROD-003 — Proteger acesso e transporte

- [ ] Exigir cookie de refresh com `Secure` em produção e rejeitar configuração insegura.
- [ ] Configurar CORS por ambiente; permitir somente origens explícitas em produção.
- [x] Implementar bloqueio configurável do cadastro público e acesso automático
  ao agente para o participante autenticado (**P06**). Concluído e testado em
  2026-11-20; provisionamento por CLI preservado, sem gate adicional de v2.
- [ ] Validar configurações obrigatórias sem expor segredos nos erros ou logs.
- [ ] Definir HTTPS na borda, proxies confiáveis e tratamento dos headers encaminhados.
- [ ] Conferir rate limit com o IP correto atrás do proxy, sem confiar em headers arbitrários.
- [ ] Cobrir cadastro, cookies, origens, proxy e rejeição de configuração insegura
  com verificações automatizadas sem IA real.

## Etapa 3 — Infraestrutura e publicação

### PROD-004 — Definir hospedagem e persistência

- [ ] Escolher provedor/região, modalidade de execução, domínio e orçamento.
- [ ] Definir pacote de publicação e dependências de extração documental/exportação PDF.
- [ ] Configurar PostgreSQL privado e credenciais próprias, com permissões adequadas.
- [ ] Definir storage persistente e privado; verificar sua compatibilidade com
  `IFileStorage`, downloads, workers e exclusões.
- [ ] Definir armazenamento de segredos, permissões de execução e temporários.
- [ ] Preparar homologação com dados fictícios e verificar que testes não alcancem produção.
- [ ] Documentar a opção local e impedir o uso acidental do Compose local como
  uma configuração pronta para exposição pública.

### PROD-005 — Verificar disponibilidade e acompanhar falhas

P04 disponibiliza liveness HTTP, métricas, coleta periódica, painel e regras de
alerta locais. A prontidão completa, canal real, responsável, retenção de logs e
ensaio no ambiente de deploy continuam abertos. Ver
[evidência P04](../assistente-conversacional/evidencias/p04-monitoramento-2026-11-19.md).

- [ ] Separar liveness de readiness; readiness deve conferir banco, migrations
  necessárias e acesso ao storage, sem revelar detalhes sensíveis.
- [ ] Definir métricas, destino de logs, alertas e resposta para falhas, consumo,
  jobs sem progresso e exclusões pendentes, em conjunto com P04 do assistente.
- [ ] Configurar tarifas e limites de IA do ambiente, reutilizando P03.
- [ ] Verificar indisponibilidade do banco/storage e falha de workers com dados fictícios.

## Etapa 4 — Recuperação e integridade dos dados

### PROD-006 — Proteger backup, restauração e exclusões

- [ ] Definir retenção, perda tolerável de dados e tempo de recuperação.
- [ ] Preparar backup consistente de banco e documentos e proteção das credenciais.
- [ ] **P05 — Em espera, fora do MVP:** registro independente das exclusões e
  reaplicação após restauração. Código preservado em branch separada; retomar
  quando repriorizado.
- [ ] Tratar arquivos órfãos em falhas de exclusão de documentos do Caso, com
  registro persistido, novas tentativas e auditoria.
- [ ] Ensaiar backup/restauração básicos com dados fictícios, preservando Peças
  e arquivos. A reaplicação automática de exclusões após backup pertence ao P05 adiado.

### PROD-007 — Ensaiar deploy e rollback

- [ ] Definir execução controlada de migrations e separação das permissões de runtime.
- [ ] Definir drenagem de workers, retomada de jobs e tratamento de reservas financeiras.
- [ ] Ensaiar atualização e rollback sem downgrade destrutivo, perda de arquivos
  ou reaparecimento de conversas em exclusão, em conjunto com P08.
- [ ] Verificar que reinicialização e substituição da instância preservam dados.
- [ ] Registrar versão/commit, verificações, critérios de interrupção e recuperação.

## Etapa 5 — Liberação do piloto

### PROD-008 — Homologar e registrar a decisão de liberação

- [ ] Conferir os critérios do MVP e as pendências P01/P02/P07 do assistente.
- [ ] Validar com o advogado no MVP online, em acesso restrito: entrada pelo chat, anexos, geração, aprovação,
  edição manual, salvamento, retomada e exportação Word/PDF.
- [ ] Planejar separadamente qualquer rodada de IA real necessária a essa
  homologação e executá-la somente após solicitação explícita e orçamento.
- [ ] Reexecutar os checks adequados à versão candidata; registrar evidências
  atuais, sem reutilizar aprovações de commits anteriores automaticamente.
- [ ] Conferir acesso restrito ao usuário/organização do piloto e acompanhamento
  operacional, usando o cadastro fechado e o provisionamento por CLI já entregues.
- [ ] Registrar a decisão de liberação e a versão entregue, conforme o fluxo
  de [versionamento](../../../docs/versionamento.md).

## Como acompanhar

Para cada entrega concluída, registrar commit, ambiente, comando/verificação,
resultado e limitações em um documento de evidência desta feature. Nunca incluir
chaves, tokens, senhas ou conteúdo jurídico real nesses registros.

Manter os status relacionados no [TODO do assistente](../assistente-conversacional/TODO.md)
coerentes. Configurar a nuvem ou passar a suíte de testes não encerra sozinho as
etapas de restauração, revisão jurídica ou liberação do piloto.
