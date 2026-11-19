# TODO — Projeto JuridicoAI

Atualizado em **2026-11-18**. Acompanhamento geral das prioridades do projeto.
O detalhamento de cada feature permanece no seu próprio TODO.

O [escopo de lançamento do MVP](MVP.md) registra o uso inicial por um advogado
empresarial, a entrada de dados pelo chatbot e o congelamento de novas
funcionalidades de produto. Seus critérios definem o ponto de conclusão do MVP.

## Pendência com alta prioridade

- [ ] **PROJ-007 — Planejar a preparação para produção preservando os testes locais.**
  **Estado: planejamento; implementação ainda não iniciada.** O destino será em
  nuvem, com configuração local preservada. Testes com IA real ficam desligados
  por padrão e só podem ser executados quando explicitamente solicitados, com
  orçamento definido; ter chave configurada não autoriza consumo. Escopo,
  decisões em aberto e tarefas PROD-001–PROD-008 estão na
  [feature de preparação para produção](implementacoes/features/preparacao-producao/README.md)
  e em seu [TODO](implementacoes/features/preparacao-producao/TODO.md).

- [ ] Validar e concluir a jornada de redação de peças conforme os critérios do
  [MVP](MVP.md), usando a implementação existente do assistente conversacional.

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

- [ ] **PROJ-003 — Validar e simplificar a entrada na redação pelo chat. Prioridade: `high` (alta).**

**Estado conferido no código em 2026-11-18:** já é possível criar um Caso e uma
Peça vazia, fornecer fatos e anexos na conversa e pedir uma minuta para revisão
e aprovação. O fluxo dispensa o cadastro prévio de empresas, partes, endereços,
contextos e documentos separados do Caso. A conferência do código e dos testes
existentes não equivale a uma nova execução com o modelo real ou à homologação
pelo advogado.

A entrada atual exige título e descrição do Caso no frontend e título da Peça;
o modelo da Peça é opcional. A descrição do Caso já é opcional na API. Caso e
Peça organizam o trabalho, enquanto os dados jurídicos e arquivos podem ser
fornecidos pelo chat. O painel usa a Peça salva, a conversa e seus anexos;
materiais antigos do Caso são consultados mediante pedido expresso. Ver
[ajustes já entregues](implementacoes/features/assistente-conversacional/evidencias/painel-simplificado-2026-11-17.md).

**Trabalho restante:** conferir com o advogado se o caminho de criar/abrir Caso,
criar/retomar Peça e acessar o chat está claro; identificar campos, etapas ou
atalhos que provoquem repetição de dados ou desviem para cadastros dispensáveis.
Simplificar os pontos identificados e corrigir os impedimentos encontrados,
reaproveitando o fluxo implementado. A necessidade de alterar a criação de Caso
ou Peça deve resultar dessa validação, preservando os registros existentes.

**Critérios de conclusão:**

- [ ] Registrar uma execução representativa em que o advogado inicia o trabalho,
  fornece fatos/anexos no chat e chega à primeira minuta revisada e aprovada sem
  precisar cadastrar os dados jurídicos em outras telas.
- [ ] Registrar os pontos de atrito da entrada e corrigir os que impedem ou
  confundem essa jornada, verificando novamente o percurso após as mudanças.
- [ ] Preservar o acesso aos trabalhos existentes, às configurações/modelos de
  layout, à edição manual, ao salvamento, à retomada e à exportação.

A tarefa permanece aberta para validação de uso e eventuais ajustes de jornada.
A qualidade jurídica, a compatibilidade completa do editor e as condições
financeiras e operacionais seguem no
[TODO do assistente](implementacoes/features/assistente-conversacional/TODO.md).
Cadastros estruturados automáticos pela IA e seleção de trechos continuam fora
deste recorte, conforme o MVP e a PROJ-006.

## Pendências

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

## Melhorias futuras — fora do MVP

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

## Acompanhamento por feature

- [Preparação para produção — ambientes, segurança, testes pagos e operação](implementacoes/features/preparacao-producao/TODO.md).
- [Assistente conversacional — entregas, validações e melhorias](implementacoes/features/assistente-conversacional/TODO.md).
