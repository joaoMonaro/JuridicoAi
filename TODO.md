# TODO — Projeto JuridicoAI

Atualizado em **2026-11-23**. Acompanhamento geral das prioridades do projeto.
**Somente a seção “Essencial para publicar o MVP” reúne os bloqueios de lançamento,
em ordem decrescente de prioridade.** As demais tarefas continuam acompanhadas
abaixo, sem exigir a conclusão de todo o plano técnico para o primeiro piloto.

Referência de aceite: [MVP de redação de peças jurídicas](MVP.md), para um advogado
empresarial, com entrada de fatos e documentos pelo chat. Preservar a execução
local e manter novas funcionalidades fora do lançamento.

## Essencial para publicar o MVP

### 1. Ambiente online e preservação dos dados — prioridade alta

- [x] Preparar e executar o ensaio local isolado: imagens limpas, migrations
  controladas, leitura/exportação, persistência após reinício/recriação,
  backup/restauração em volumes novos e recuperação de configuração inválida.
  Ver [evidência do ensaio](implementacoes/features/preparacao-producao/evidencias/ensaio-local-2026-11-22.md).
  Repetir as verificações do ambiente ao configurar a hospedagem escolhida.
- [ ] Escolher a hospedagem e publicar o MVP em nuvem, com PostgreSQL privado,
  documentos em storage persistente e privado e dados/credenciais separados dos testes locais.
  Na publicação, preencher domínio/certificado HTTPS, origem real do frontend,
  endereços dos proxies e segredos do ambiente; conferir HTTPS e rate limit publicados.
- [ ] Aplicar as migrations e instalar as dependências de leitura de PDF com texto,
  DOCX e TXT e de exportação Word/PDF; manter OCR desativado.
- [ ] Verificar que reiniciar ou atualizar a instância preserva Peças, conversas e
  arquivos, incluindo o funcionamento das exclusões já disponíveis.
- [ ] Preparar backup básico de banco e documentos e comprovar uma restauração
  com dados fictícios; registrar como recuperar o serviço se a publicação falhar.

Detalhamento: recorte de **PROD-001/PROD-004/PROD-006/PROD-007/P08** no
[TODO de produção](implementacoes/features/preparacao-producao/TODO.md).
Homologação pode ser temporária; não é exigido manter três ambientes permanentes.

### 2. IA configurada e consumo controlado — prioridade alta

- [ ] Configurar modelo, chave, flags/permissões, tarifas reais, versão de preços
  e limite mensal da organização, reutilizando o controle financeiro **P03**.
- [ ] Conferir o bloqueio por limite e acompanhar consumo e tempo de resposta
  para o uso inicial, sem aumentar cotas automaticamente.
- [x] Executar o ensaio local com IA simulada e banco exclusivo, sem dados ou
  credenciais de produção, incluindo bloqueio financeiro antes do provedor.
  Repetir os checks na versão candidata. Testes reais exigem solicitação
  explícita e orçamento próprio; uma chave configurada não autoriza consumo em testes.

Detalhamento: **PROD-002/P03** no [TODO de produção](implementacoes/features/preparacao-producao/TODO.md)
e na [evidência financeira](implementacoes/features/assistente-conversacional/evidencias/p03-limite-mensal-2026-11-18.md).

### 3. Jornada completa validada com o advogado — prioridade alta

- [x] **Ensaio ampliado local (2026-11-23).** Passaram 79 testes de navegador e
  16 verificações com API real e IA simulada. Foram validados documentos longos,
  exportação Word/PDF em dois layouts, aprovação parcial, rejeição, recuperação
  do salvamento após reconexão, conflitos, troca de peça e avisos de anexos.
  Quatro peças e cinco anexos foram preservados após recriar os containers.
- [ ] **PROJ-003 — Validar a entrada e a redação pelo chat.** No MVP online e
  restrito, concluir um trabalho representativo: iniciar/retomar a Peça, fornecer
  fatos/anexos e obter uma minuta, sem cadastrar os dados jurídicos em outras telas.
- [ ] Revisar fatos, fontes, lacunas e utilidade jurídica com o advogado (**P02**);
  confirmar que nenhuma alteração é aplicada sem aprovação.
- [ ] Verificar aprovação/rejeição, edição manual, salvamento e retomada após
  recarga ou falha de conexão, sem perda de texto ou duplicação de alterações.
- [ ] Conferir Word e PDF contra o texto salvo e o layout escolhido; apresentar
  as limitações de leitura dos anexos. Corrigir os defeitos que impedirem esse fluxo.

O fluxo já está implementado; falta homologar o uso e corrigir os impedimentos
identificados. Os [ajustes entregues](implementacoes/features/assistente-conversacional/evidencias/painel-simplificado-2026-11-17.md)
e as pendências **P01/P02/P07** estão no [TODO do assistente](implementacoes/features/assistente-conversacional/TODO.md).
Executar somente os cenários necessários para comprovar esse uso inicial;
rodadas reais adicionais dependem de solicitação e orçamento.

### 4. Conferência final e liberação — após os itens anteriores

- [ ] Executar os checks da versão candidata, incluindo PostgreSQL descartável,
  e verificar acesso ao banco/storage e o fluxo publicado.
- [ ] Definir quem acompanha o piloto e como consultar erros, consumo, jobs e
  exclusões pendentes, aproveitando os logs e o monitoramento já existentes.
- [ ] Registrar o commit, as verificações, o procedimento de publicação/recuperação
  e a aprovação dos critérios do [MVP](MVP.md); promover a versão conforme o
  [fluxo de versionamento](docs/versionamento.md).

Detalhamento: recorte de **PROD-005/PROD-008/P04** no
[TODO de produção](implementacoes/features/preparacao-producao/TODO.md).
A aprovação técnica não substitui a validação do advogado.

## Coordenação da preparação para produção

- [ ] **PROJ-007 — Planejar a preparação para produção preservando os testes locais.**
  **Estado: pacote e persistência ensaiados localmente; publicação em nuvem pendente,
  com monitoramento P04 parcialmente implementado.** Para este lançamento, acompanhar o recorte
  essencial acima. Os planos detalhados também contêm melhorias posteriores;
  não é necessário encerrar todos os seus itens para concluir o MVP.
  Ver [escopo e decisões de produção](implementacoes/features/preparacao-producao/README.md).

## Melhorias técnicas posteriores — não bloqueiam a publicação inicial

Estes itens permanecem abertos. Repriorizar se algum deles impedir os critérios
do MVP no ambiente ou no trabalho representativo do piloto.

- [ ] Automatizar build, testes e deploy em CI; a conferência da versão candidata
  pode ser executada manualmente no primeiro lançamento.
- [ ] Implementar readiness automatizada e completar canais de alerta, limiares
  e retenção de logs de **P04**, além do acompanhamento básico definido para o piloto.
- [ ] Ampliar a automação dos ensaios de deploy/rollback e a separação das
  permissões de migrations/runtime, conforme a operação evoluir.
- [ ] Corrigir respostas HTTP 500 para JSON inválido de Peças/templates, retornando
  HTTP 400 para tipos incorretos em `type`, `format` e margens.
- [ ] Tratar arquivos órfãos na exclusão de documentos do Caso com fila persistida
  e novas tentativas. O cadastro separado de documentos do Caso está fora do MVP.
- [ ] Adicionar orçamento, timeout e gravação parcial ao avaliador C# antes de
  voltar a utilizá-lo em rodadas pagas; usar o avaliador com orçamento já disponível
  nas validações necessárias ao MVP.
- [ ] Ampliar **P01/P07** para os demais cenários de análise documental, memória
  extensa e compatibilidade, além dos necessários para o uso inicial homologado.
- [ ] **M03 — Rever a política de cotas para etapas internas e mensagens.**
  A consulta real ainda aguardou aproximadamente 18,9 s pela reposição da cota.
  Definir a política antes de alterar limites, preservando concorrência, proteção
  contra abuso e orçamento. PROJ-001/M01 permanece concluído.
  Ver [M03 no TODO do assistente](implementacoes/features/assistente-conversacional/TODO.md#melhorias-evidenciadas-na-avaliação-reduzida).

## Melhorias futuras — fora do MVP

- [ ] **P05 — Proteção das exclusões após restauração. Em espera.** Volume
  independente e reaplicação automática adiados por decisão de produto em
  2026-11-19. Código preservado em `feature/p05-exclusoes-restauracao`, separado
  de `homologacao`; não bloqueia o MVP. Exclusão no ambiente ativo e backup básico
  continuam no escopo.

- [ ] **PROJ-006 — Definir a interação por seleção de trechos no assistente. Em espera.**
  Seleção de trechos pausada por decisão do produto em 2026-11-17. Novos pedidos
  e reenvios do painel não devem capturar ou transmitir a seleção nem o cursor
  do editor. Antes de retomar, definir se a seleção será referência ou limite
  da edição, sua indicação/remoção no chat e seu comportamento após editar,
  recarregar ou reenviar um pedido. Edição por instruções na conversa continua
  disponível, com aprovação das propostas. Esta decisão futura não bloqueia o MVP.

- [ ] **PROJ-004 — Pesquisa de jurisprudência na web pelo chatbot.** Integrar
  pesquisa web em fontes oficiais, com filtros por tribunal, referências
  verificáveis e links preservados no histórico. Permitir propor a inclusão dos
  julgados na Peça, mantendo a aplicação sujeita à aprovação do usuário.

- [ ] **PROJ-005 — RAG sobre base jurídica de referência.** Definir o acervo,
  a ingestão, a indexação e a recuperação de trechos relevantes para fundamentar
  respostas e propostas do assistente, com identificação das fontes e respeito
  às permissões de acesso. Avaliar a qualidade da recuperação e das citações.

Estas melhorias ficam para depois da conclusão e validação do MVP; não são
critérios de lançamento nem reintroduzem o scraper no escopo atual.

## Entregas concluídas

Os resultados abaixo são evidências históricas das respectivas entregas.
A liberação em produção exige validar novamente a versão candidata.

- [x] **PROD-003 — Corrigir a corrida refresh/logout e verificar isolamento e revogação da sessão principal.**
  Concluído em 2026-11-20: credencial estável de revogação, renovação atômica e
  logout independente da última rotação. Tokens legados preservados por migration.
  JWT real e PostgreSQL descartável comprovaram as duas ordens da corrida,
  resposta atrasada, revogação por sessão e isolamento entre organizações e
  entre titulares de conversas/anexos. **401 testes aprovados e 1 de OCR ignorado**
  na suíte completa, sem IA real. Ver [evidência da sessão principal](implementacoes/features/preparacao-producao/evidencias/sessao-principal-2026-11-20.md).

- [x] **PROD-003 — Proteger transporte, origens e encaminhamento por proxy no backend.**
  Concluído em 2026-11-20: HTTP recusado com 400/`HTTPS_REQUIRED` antes dos
  endpoints; `Auth:SecureRefreshCookie=true` no perfil Production, com rejeição
  da inicialização se desabilitado. HTTP local preservado em Development.
  `Cors:AllowedOrigins` define a lista exata de origens HTTPS em produção,
  compartilhada com a sessão pessoal; configuração vazia ou inválida impede
  a inicialização. Localhost automático somente em Development.
  Proxies confiáveis definidos por `ReverseProxy:KnownProxies`/`KnownNetworks`;
  listas vazias desativam encaminhamento. IP e protocolo originais são tratados
  antes de HTTPS e do rate limit, preservando 10 requisições/minuto por IP.
  **68 testes aprovados**, incluindo TLS real, inicialização, cookies, CORS,
  sessão pessoal, proxies, cadeias e bloqueio por IP sem aceitar falsificações.
  Certificado/domínio, origem, proxies reais e segredos serão definidos na publicação já prevista.

- [x] **P06 — Bloquear cadastro público por configuração e liberar o agente ao participante autenticado.**
  Concluído em 2026-11-20: `Auth:RegistrationEnabled=false` bloqueia o registro;
  provisionamento por CLI preservado e acesso ao agente automático em organizações
  ativas, respeitando os controles existentes. Sem gate adicional de v2.
  **79 testes aprovados**, incluindo PostgreSQL descartável e IA simulada.
  Ver [evidência P06](implementacoes/features/assistente-conversacional/evidencias/p06-acesso-piloto-2026-11-20.md).

- [x] **PROJ-002 — Retirar o scraper e o fluxo de jurisprudências do MVP. Prioridade: `high` (alta).**

Decisão de escopo: a coleta e o processamento de jurisprudências acrescentam
complexidade desnecessária ao MVP. As rotas anteriores permitiam ingestão e
reprocessamento sem autenticação, além de consulta pública aos dados brutos.

**Concluído em 2026-11-17:** endpoints de `/api/jurisprudencias`, serviços,
repositórios, configurações e dependências exclusivos retirados da API,
incluindo MongoDB e Playwright. Solução, Docker Compose e documentação
atualizados. O scraper foi extraído para `../JuridicoAI.Scrapper`, com CLI
independente e cópia do legado de integração para consulta.

Verificação: build sem avisos/erros, **291 testes aprovados e 1 de OCR ignorado**,
incluindo PostgreSQL descartável e regressão das quatro rotas removidas. Nenhum
banco ou volume existente foi apagado. Ver
[registro da extração e validação](implementacoes/alreadyDone/extracao-scraper-2026-11-17.md).

- [x] **PROJ-001 — Testar e corrigir a latência da IA. Prioridade: `medium` (média).**

Na avaliação reduzida, uma consulta simples sobre a minuta aprovada levou
**63,941 segundos**, enquanto as demais interações ficaram entre 5,300 e
22,993 segundos. O tempo medido é o da interação completa; os registros originais
não separaram as etapas da demora.

**Concluído em 2026-11-14:** diagnóstico, correção, instrumentação e repetição
real com GPT-6.1 Terra. A consulta passou de **63,941 s para 22,697 s** nesta
comparação. O cenário completo de três interações passou nos checks técnicos,
com custo calculado de **US$ 0,03260130**. Ver
[evidência real](implementacoes/features/assistente-conversacional/evidencias/m01-latencia-real-2026-11-14.md).

A geração no provedor levou 2,555 s; a espera dominante foi de aproximadamente
18,905 s até a admissão pelo limite local. Essa política remanescente está em
**M03 (medium)** no TODO da feature, separada da correção de reagendamento.

Verificações realizadas:

1. Consulta reproduzida e etapas medidas separadamente.
2. Reagendamento excessivo e consumo de cota em negações corrigidos/testados.
3. Tempos comparados com o modelo real, sem elevar cotas ou reduzir contexto.
4. Diagnóstico/resultados registrados. Não há SLA aprovado; novas rodadas pagas
   continuam exigindo orçamento definido.

Detalhamento: [M01/M03 no TODO da feature](implementacoes/features/assistente-conversacional/TODO.md#melhorias-evidenciadas-na-avaliação-reduzida).
A melhoria foi observada em uma rodada e não garante a latência de todo pedido.

## Acompanhamento por feature

- [Preparação para produção — ambientes, segurança, testes pagos e operação](implementacoes/features/preparacao-producao/TODO.md).
- [Assistente conversacional — entregas, validações e melhorias](implementacoes/features/assistente-conversacional/TODO.md).
