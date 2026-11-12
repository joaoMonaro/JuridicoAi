# Plano de implementação e evidências necessárias

> Reconciliação em **2026-11-12**, após geração durável, histórico pessoal e
> avaliação real reduzida. Referência consolidada: [estado atual](../estado-atual.md).

Este plano substitui as marcações desatualizadas do detalhamento inicial de
2026-11-11. `[x]` indica implementação ou verificação técnica com evidência;
`[ ]` indica trabalho ainda aberto. Entrega técnica não equivale a homologação
jurídica ou liberação do piloto. Os IDs P01–P09 remetem aos
[pontos restantes](../estado-atual.md#pontos-restantes), com natureza e critérios
de conclusão. Evidências históricas mantêm os limites de cada rodada.

## Decisões técnicas consolidadas

| Tema | Implementação atual |
|---|---|
| Aplicação | Monólito modular ASP.NET Core existente |
| Persistência | PostgreSQL 17 e JSONB; trabalhos duráveis no mesmo banco |
| Processamento | BackgroundService, leases, fence e checkpoints |
| Arquivos | IFileStorage com namespace privado por conversa |
| Autorização histórica | Sessão pessoal separada; leitura própria sem Membership, com conta ativa |
| IA | Responses, memória da aplicação, ferramentas autorizadas; modelo não grava a Peça |
| Documentos | OpenXml/Poppler para leitura nativa; pipeline Tesseract desativado |
| Recuperação | Busca full-text/exata filtrada, ferramentas paginadas e cobertura persistida |
| Editor | TipTap 3, plugin/decorations de revisão e splices versionados |
| Ancoragem | Aliases de snapshot, sem migração de IDs persistentes |
| Commit | Uma transação para Peça, decisões e recibo; reconciliação idempotente |

Não foi criado segundo backend, banco vetorial ou serviço cloud nesta etapa.
Provisionamento de produção, condições de redistribuição e preços ainda exigem
os fechamentos indicados abaixo. Valores de piloto não são SLAs aprovados.

## E0 — contratos e compatibilidade

- [x] Prova local de splice, diff e undo/redo no TipTap instalado, com conteúdo
  fictício; depois integrada ao editor real na E4.
- [ ] Consolidar fixtures completas do schema, incluindo marks, listas, links,
  headings, emoji e blocos desconhecidos existentes — P07.
- [x] Executar ferramentas e saída estruturada com GPT-6.1 Terra no recorte
  reduzido, com orçamento explícito e resultado técnico registrado.
- [ ] Ampliar a avaliação para os cenários adiados — P01.
- [ ] Registrar versões exatas e condições de redistribuição das dependências — P07.
- [ ] Calibrar qualidade/cobertura do OCR com corpus fictício — P09, **em espera**.

Evidências: [editor](../evidencias/e0-editor-2026-11-11.md),
[preflight](../../../../avaliacoes/assistente-conversacional/preflight-2026-11-12.md),
[avaliação real reduzida](../evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).

## E1 — persistência, acesso e fila

- [x] Migrations aditivas, repositórios, índices e FKs compostas.
- [x] Conversa privada por usuário/Peça, mensagens paginadas e idempotência.
- [x] Sessão pessoal de histórico, UI e provas de autorização sem Membership.
- [x] Worker do chat v2 com leases/checkpoints, progresso e bloqueio de resultados tardios.
- [x] Exclusão manual autorizada no banco e armazenamento ativos, com protocolo.
- [ ] Registro externo de exclusões e reaplicação automatizada antes de reabrir
  uma restauração, incluindo ensaio de banco e arquivos — P05.

Evidências: [revisão persistida](../evidencias/e1-revisao-persistida-2026-11-11.md),
[histórico](../evidencias/e1-e3-historico-2026-11-11.md),
[histórico pessoal/exclusão](../evidencias/e1-historico-pessoal-exclusao-2026-11-12.md).

## E2 — documentos e contexto

- [x] Upload privado, extração nativa PDF/DOCX/TXT, qualidade por trecho e progresso.
- [x] Extração de documentos do Caso com versão e autorização.
- [x] Limites técnicos de arquivo/páginas e processamento durável com recuperação de falhas.
- [x] Pipeline OCR e métricas iniciais; não houve aprovação da calibração nem ativação.
- [x] Busca full-text/exata e ferramentas de listagem, busca e leitura com cobertura.
- [x] Invalidação de derivados quando fontes são revogadas ou reprocessadas.
- [x] Memória versionada, proveniência e recuperação textual/cronológica do histórico.
- [x] Análise extensa por lotes, citações/sínteses, consolidação e checkpoints.
- [x] Verificar leitura parcial e instrução maliciosa no recorte real reduzido.
- [ ] Ampliar avaliação de fontes, memória extensa e correções com memória — P01/P02.
- [ ] Completar orçamento financeiro de produção — P03. Limites técnicos já existem.
- [ ] Calibrar/ativar leitura de digitalizações — P09, **em espera**.

Evidências: [documentos](../evidencias/e2-documentos-2026-11-11.md),
[recuperação](../evidencias/e2-recuperacao-documental-2026-11-11.md),
[memória](../evidencias/e2-memoria-conversa-2026-11-12.md),
[análise durável](../evidencias/e2-analise-documental-duravel-2026-11-12.md).

## E3 — conversa com IA e geração

- [x] Histórico elegível e Peça atual no contexto; mensagens originais preservadas.
- [x] Adaptador, ferramentas autorizadas, prompts/contratos versionados e limites técnicos.
- [x] Intenção automática e propostas na mesma saída estruturada, sem modos obrigatórios.
- [x] Ajuste de proposta referenciada substitui pendências e preserva grupos aprovados.
- [x] Chat v2 durável e minutas por seções com checkpoints/revisão; rascunho incompleto
  não é publicado como proposta integral aprovável.
- [x] Ledger por chamada física e somatório por execução, sem conteúdo jurídico.
- [x] Suíte conversacional e runner com controle de custo preparados e testados offline.
- [x] Minuta/correção/aprovação/consulta, leitura parcial e segurança com modelo
  real no recorte de quatro cenários/seis interações; zero falhas nos checks.
- [ ] Completar análise integral documental e memória extensa com modelo real;
  investigar consulta simples de 63,941 s — P01.
- [ ] Homologação humana de qualidade jurídica/fidelidade — P02.
- [ ] Tetos financeiros agregados de produção — P03.

Evidências: [conversa livre](../evidencias/e3-conversa-livre-2026-11-11.md),
[geração durável](../evidencias/e2-e3-geracao-duravel-2026-11-12.md),
[avaliação real reduzida](../evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).
O endpoint v1 independente mantém execução ligada à requisição; não se declara
migração de todos os contratos legados para o worker v2.

## E4 — edição visual e aplicação

- [x] Compilador de propostas, hashes, grupos e comparação determinística.
- [x] Extensão/toolbar de revisão e navegação entre mudanças no editor real.
- [x] ensurePersisted e coordenação de commit externo sem PATCH duplicado.
- [x] Serviço transacional, recibos, reconciliação e aplicação parcial.
- [x] Undo/redo e preservação de formatação verificados no recorte local/focado.
- [ ] Fechar matriz completa de schema/formatação, documentos longos, acessibilidade,
  paginação, persistência e exportação sem previews — P07. Isso amplia a validação
  da implementação existente; não significa que compilador ou transação faltem.

Evidências: [prova local](../evidencias/e0-editor-2026-11-11.md),
[integração persistida](../evidencias/e1-revisao-persistida-2026-11-11.md).

## E5 — integração e ativação

- [x] Testes com conteúdo fictício de isolamento, múltiplos titulares, perda de vínculo,
  geração durável e exclusão, incluindo API/PostgreSQL e E2E focados.
- [x] Flags operacionais e capacidades existentes selecionam a experiência disponível.
- [x] Aviso com persistência/anexos privados e versão de aceite correspondente.
- [x] Migrations aditivas e proteções de downgrade de estruturas com dados.
- [ ] Completar gate específico de rollout v2 por organização, separado das flags
  operacionais; validar seleção v1/v2 sem aplicação concorrente dos fluxos — P06.
- [ ] Homologar a jornada integrada com o modelo real e revisar resultados — P01/P02/P06.
- [ ] Rever texto/versão final do aviso e regressões da jornada — P07.
- [ ] Monitoramento e alertas de custo, falhas, jobs e exclusões — P04.
- [ ] Preparar armazenamento, HTTPS/origens, migrations, drenagem de workers e
  temporários legados; ensaiar deploy/rollback sem perda ou reexposição — P08.
- [ ] Comprovar proteção das exclusões em backups/restauração — P05.

Evidências: [geração](../evidencias/e2-e3-geracao-duravel-2026-11-12.md),
[histórico pessoal](../evidencias/e1-historico-pessoal-exclusao-2026-11-12.md).
O [procedimento operacional](06-operacao-exclusao-restauracao.md) está escrito;
sua existência não comprova provisionamento nem execução do ensaio.

## Matriz de validação e situação atual

| Cenário | Situação registrada | Fechamento restante |
|---|---|---|
| Dois usuários na mesma Peça; outra organização | Testes técnicos e recorte real de anexo privado de outro titular aprovados | Jornada ampliada e piloto: P01/P06 |
| Histórico sem Membership | API/PostgreSQL e UI focada aprovados; sem acesso a fontes atuais | Ambiente de implantação: P08 |
| Leitura nativa parcial/duvidosa | Gate, sinalização e resposta com fonte parcial verificados no recorte real | Avaliação ampliada/revisão: P01/P02 |
| OCR ruim/parcial/misto | Pipeline inicial; calibração insuficiente, desativado | P09 em espera |
| Correções e conversa longa | Correção/consulta após aprovação verificadas com modelo real; memória extensa com simulação | Memória extensa real: P01/P02 |
| Todos os documentos | Cobertura/checkpoints e bloqueio de conclusão incompleta testados | Qualidade real: P01/P02 |
| Aprovação, aplicação simultânea e perda de resposta | Transação, recibo, idempotência e reconciliação testados | Jornada integrada: P06/P07 |
| Autosave e aprovação parcial | Coordenação e remapeamento verificados em testes focados | Matriz ampliada: P07 |
| Undo/redo, marks e exportação | Recorte local/focado validado; exportação integral não homologada | P07 |
| Exclusão durante execução | Cancelamento, fence e expurgo ativo testados | Monitoramento/deploy: P04/P08 |
| Restauração de backup | Procedimento documentado; infraestrutura/ensaio pendentes | P05 |
| Limites, restart e timeout | Ledger/checkpoints/retomada testados; resultado incerto sem retry pago automático | Custo real e controle financeiro: P01/P03 |

Backend usa testes unitários e integração PostgreSQL para locks, transações e
constraints. Frontend tem testes unitários e Playwright focado. Os números da
última rodada, as ressalvas sobre E2E completo e as condições de ativação estão
no [estado atual](../estado-atual.md#verificações-já-registradas).

## Ordem de continuidade

1. Revisar a rodada real reduzida (P02) e investigar a latência observada.
2. Completar os cenários adiados de P01 com orçamento adicional definido.
3. Fechar P03–P08 e registrar evidências antes da liberação controlada.
4. Manter P09 em espera até repriorização explícita.

Não há prazo de entrega, SLA de exclusão de backups ou custo do piloto aprovado
por este plano. As flags permanecem desligadas por padrão.
