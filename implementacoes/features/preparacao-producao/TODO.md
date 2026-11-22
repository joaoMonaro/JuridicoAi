# TODO — Preparação para produção e testes locais

Atualizado em **2026-11-22**. **Estado: PROD-003 e P06 concluídos; pacote e persistência ensaiados localmente; monitoramento P04 parcial e deploy em nuvem pendente.**
Referência: [escopo, decisões confirmadas e questões em aberto](README.md).

## Decisões registradas

- [x] Registrar o destino em nuvem, preservando configuração e testes locais.
- [x] Registrar a exigência de solicitação explícita para testes com IA real.
- [x] Separar neste plano decisões confirmadas, propostas e escolhas pendentes.

Os itens acima representam documentação. P04 possui implementação e ensaio local;
PROD-003 e P06 simplificado têm código e testes concluídos. As demais frentes e as condições
operacionais indicadas abaixo continuam pendentes. O
[ensaio local de 2026-11-22](evidencias/ensaio-local-2026-11-22.md) comprovou o
recorte local de PROD-001/004/006/007, sem encerrar a configuração da nuvem.

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

- [x] Exigir HTTPS no backend de produção: HTTP retorna 400/`HTTPS_REQUIRED`
  antes dos endpoints, sem aceitar cabeçalhos de proxy por conta própria.
  Concluído em 2026-11-20, com HTTP local preservado em Development.
- [x] Exigir cookie de refresh com `Secure` em produção e rejeitar configuração
  insegura na inicialização. Padrão `true` no perfil Production. Concluído em
  2026-11-20; 24 testes de transporte/autenticação aprovados, incluindo TLS real
  com certificado temporário e cookies de login/refresh/logout.
- [x] Configurar CORS por ambiente; permitir somente origens HTTPS explícitas em
  produção por `Cors:AllowedOrigins`, com validação na inicialização. Mesma lista
  para a sessão pessoal; localhost automático apenas em Development.
  Concluído em 2026-11-20: 47 testes de transporte/autenticação aprovados.
- [x] Implementar bloqueio configurável do cadastro público e acesso automático
  ao agente para o participante autenticado (**P06**). Concluído e testado em
  2026-11-20; provisionamento por CLI preservado, sem gate adicional de v2.
- [x] Verificar isolamento e revogação da sessão principal e corrigir a corrida
  entre refresh e logout. Credencial estável, UPDATEs condicionais e migration
  compatível com tokens legados. Concluído em 2026-11-20: 25 testes focados e
  regressão completa com **401 aprovados e 1 de OCR ignorado**, sem IA real.
  Ver [evidência de sessão e isolamento](evidencias/sessao-principal-2026-11-20.md).
- [x] Validar as configurações de autenticação, transporte, origens e proxies
  na inicialização, sem incluir seus valores nas mensagens de erro.
- [x] Implementar proxies confiáveis por `ReverseProxy:KnownProxies` e
  `KnownNetworks`, com limite de saltos (`ForwardLimit=1` por padrão), exigência
  de contagens iguais em `X-Forwarded-For`/`X-Forwarded-Proto` e encaminhamento
  desativado quando as listas estiverem vazias. Concluído em 2026-11-20.
- [x] Aplicar e testar o rate limit com o IP correto atrás do proxy, sem confiar
  em headers de origens desconhecidas. Mantidas 10 requisições/minuto por IP.
- [x] Cobrir cadastro, cookies, origens, proxy e rejeição de configuração insegura
  com testes sem IA real: 68 aprovados em 2026-11-20, incluindo TLS em loopback,
  HTTP 429 na 11ª requisição, isolamento entre IPs, cadeias e cabeçalhos forjados.

## Etapa 3 — Infraestrutura e publicação

### PROD-004 — Definir hospedagem e persistência

- [ ] Escolher provedor/região, modalidade de execução, domínio e orçamento;
  configurar certificado HTTPS e os IPs/redes reais dos proxies na publicação.
- [x] Definir e ensaiar pacote em containers com as dependências de extração
  PDF/DOCX/TXT e exportação Word/PDF. Build limpo de API e frontend, HTTPS local,
  configuração Production e correção do conversor headless em 2026-11-22.
  Endereços, segredos e persistência da nuvem ainda exigem configuração própria.
- [ ] Configurar PostgreSQL privado e credenciais próprias, com permissões adequadas.
- [ ] Definir storage persistente e privado; verificar sua compatibilidade com
  `IFileStorage`, downloads, workers e exclusões.
- [ ] Definir e injetar os segredos de produção durante a publicação, junto com
  seu armazenamento, permissões de execução e temporários (decisão de 2026-11-20).
- [ ] Preparar homologação com dados fictícios e verificar que testes não alcancem produção.
- [x] Documentar opção de ensaio separada do Compose de desenvolvimento,
  com porta somente em loopback, volumes exclusivos e API/banco/simulador em
  rede interna sem internet. Ver [roteiro](../../../ops/rehearsal/README.md).

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
- [x] Preparar e executar backup local consistente de banco e documentos com
  API/web interrompidos, checksums e diretório privado. Destino independente,
  retenção e credenciais da hospedagem continuam pendentes.
- [ ] **P05 — Em espera, fora do MVP:** registro independente das exclusões e
  reaplicação após restauração. Código preservado em branch separada; retomar
  quando repriorizado.
- [ ] Tratar arquivos órfãos em falhas de exclusão de documentos do Caso, com
  registro persistido, novas tentativas e auditoria.
- [x] Ensaiar backup/restauração básicos com dados fictícios em projeto e volumes
  novos: Peça, versão, histórico, anexos por SHA-256 e exportações conferidos.
  Repetir no ambiente escolhido. A reaplicação automática de exclusões após
  backup pertence ao P05 adiado.

### PROD-007 — Ensaiar deploy e rollback

- [ ] Definir execução controlada de migrations e separação das permissões de runtime.
- [ ] Definir drenagem de workers, retomada de jobs e tratamento de reservas financeiras.
- [ ] Ensaiar atualização e rollback sem downgrade destrutivo, perda de arquivos
  ou reaparecimento de conversas em exclusão, em conjunto com P08.
- [x] Verificar localmente reinicialização dos serviços e substituição dos
  containers de API/web preservando dados. Repetir na hospedagem escolhida.
- [x] Registrar commits-base, hashes das fontes/imagens, verificações e recuperação
  do ensaio local. Registrar novamente a versão candidata e o aceite da publicação.

O comando `migrate-database` permite aplicar migrations antes de iniciar o runtime
com `RunMigrations=false`. A recuperação comprovada recusa configuração insegura
e volta à mesma imagem válida; não é downgrade de schema nem prova de
compatibilidade com migrations futuras. Separação SQL de migrations/runtime
e drenagem com jobs em andamento não foram implementadas neste recorte.

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
