# TODO — Projeto JuridicoAI

Atualizado em **2026-11-16**. Acompanhamento geral das prioridades do projeto.
O detalhamento de cada feature permanece no seu próprio TODO.

O [escopo de lançamento do MVP](MVP.md) registra o uso inicial por um advogado
empresarial, a entrada de dados pelo chatbot e o congelamento de novas
funcionalidades de produto. Seus critérios definem o ponto de conclusão do MVP.

## Pendência com alta prioridade

- [ ] Validar e concluir a jornada de redação de peças conforme os critérios do
  [MVP](MVP.md), usando a implementação existente do assistente conversacional.

- [ ] **PROJ-002 — Retirar o scraper e o fluxo de jurisprudências do MVP. Prioridade: `high` (alta).**

Decisão de escopo: a coleta e o processamento de jurisprudências acrescentam
complexidade desnecessária ao MVP. As rotas atuais permitem ingestão e
reprocessamento sem autenticação, além de consulta pública aos dados brutos.

Retirar os endpoints de `/api/jurisprudencias`, o projeto `JuridicoAI.Scrapper`
e os serviços, repositórios, configurações e dependências exclusivos desse fluxo,
incluindo MongoDB e Playwright. Atualizar a solução, o Docker Compose e a
documentação para refletir o escopo reduzido. A retirada deve preservar os fluxos
de autenticação, Casos, Peças, documentos, exportação e assistente de IA.

Critério de conclusão: nenhuma rota de jurisprudências exposta nem dependência
desse fluxo exigida para compilar ou executar o MVP, com os testes dos fluxos
preservados aprovados. A tarefa não autoriza apagar dados ou volumes existentes.

- [ ] **PROJ-003 — Adequar a jornada ao MVP sem cadastros manuais de dados. Prioridade: `high` (alta).**

Retirar da jornada os formulários de dados jurídicos e o cadastro separado de
documentos do Caso. Fatos, dados de empresas/partes e arquivos entram no contexto
pelo chatbot. Manter configurações e modelos de layout da Peça, edição manual
do texto, salvamento, retomada e exportação.

Simplificar a entrada no trabalho, hoje dependente dos formulários de criação
de Caso e Peça, reaproveitando as entidades existentes sem exigir repetição dos
dados fornecidos na conversa. Esta tarefa ajusta o fluxo existente; não inclui
cadastros estruturados automáticos pela IA nem ampliação de funcionalidades.
Validar a jornada com as tarefas do advogado empresarial definidas no escopo.
Não apagar registros existentes como parte da retirada das telas.

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

- [Assistente conversacional — entregas, validações e melhorias](implementacoes/features/assistente-conversacional/TODO.md).
