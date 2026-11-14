# Estado atual da implementação — assistente conversacional

Registro consolidado em **2026-11-12**, após a primeira avaliação real reduzida.
Atualização de **2026-11-14**: diagnóstico/correção local de M01 e instrumentação
verificados inicialmente sem chamada paga. Depois, comparação real de M01
concluída com chave temporária, teto de US$ 1 e custo de US$ 0,03260130.
Esta é a referência de andamento da feature. O [escopo](README.md) descreve o
produto; o [plano E0–E5](especificacoes/05-entrega-e-validacao.md) detalha os
checklists e o [TODO da feature](TODO.md) acompanha as pendências e melhorias.
Os [contratos efetivos](especificacoes/07-contratos-implementados.md) distinguem
rotas, tabelas e defaults implementados das projeções originais.
A [conferência documental](evidencias/conferencia-documental-2026-11-12.md)
registra os ajustes de coerência feitos após a avaliação.

## Ponto em que estamos

O fluxo principal está implementado no backend e no frontend: conversar com
continuidade, fornecer documentos, gerar uma minuta, revisar alterações no editor
e aplicá-las por aprovação visual. Também existem execução em segundo plano,
histórico pessoal e exclusão autorizada. A entrega **ainda não está homologada
para o piloto nem liberada em produção**.

A avaliação real reduzida passou nos checks técnicos. Os próximos trabalhos
concentram-se na revisão dos resultados, avaliação ampliada, fechamento da
compatibilidade do editor e preparação financeira e operacional. A
calibração do OCR está em espera por decisão do responsável pelo produto.

### Baseline deste registro

| Repositório | Branch | Commit registrado |
|---|---|---|
| Backend | `feature/melhora-tratamento-de-dados` | `4547485c31b0966af1aecb911f12858855dccf15` |
| Frontend | `feature/testes-e-teste` | `50aa43937ce92c688cc98c03383a422c7ff968cf` |

O commit backend `4547485` prepara a avaliação; a baseline da aplicação avaliada
nos testes anteriores é `b634f3b`. Os dois repositórios estavam commitados e
sincronizados com os respectivos remotos antes desta atualização documental.
A atualização inclui a rodada real descrita abaixo e uma correção no setup do
avaliador, registrada junto da documentação. Não representa novo deploy ou
ativação em produção.

“Implementado” significa código integrado e evidência técnica indicada abaixo.
Não equivale a aprovação jurídica, qualidade comprovada do modelo ou operação
validada em infraestrutura de produção. Evidências anteriores preservam seus
resultados e limitações na data de cada etapa.

## O que já foi feito

| Frente | Comportamento implementado | Evidência |
|---|---|---|
| Conversa livre | Intenção identificada na própria geração; perguntas, edição e minuta sem seleção obrigatória de modo. O modelo prepara propostas, sem gravar a Peça. | [Conversa livre](evidencias/e3-conversa-livre-2026-11-11.md) |
| Persistência e privacidade | Uma conversa por usuário/Peça, perguntas e respostas paginadas, propostas e estados persistidos. Outro usuário tem seu próprio chat; anexos enviados permanecem privados à conversa. | [Histórico](evidencias/e1-e3-historico-2026-11-11.md), [documentos](evidencias/e2-documentos-2026-11-11.md) |
| Revisão e aplicação | Comparação no documento, grupos de mudanças, aprovação/rejeição individual ou em lote. Aprovar aplica sem nova pergunta no chat. Compilação, hashes, controle de versão, recibo idempotente e transação conjunta com a Peça. | [Revisão persistida](evidencias/e1-revisao-persistida-2026-11-11.md) |
| Editor e autosave | Coordenação por `ensurePersisted`, reconciliação após perda de resposta, aplicação parcial e undo/redo. Ajustar proposta pendente preserva grupos já aprovados. | [Editor local](evidencias/e0-editor-2026-11-11.md), [revisão persistida](evidencias/e1-revisao-persistida-2026-11-11.md), [conversa livre](evidencias/e3-conversa-livre-2026-11-11.md) |
| Fontes automáticas | Peça atual, histórico elegível, anexos privados e materiais autorizados do mesmo Caso. Busca/leitura filtradas por autorização; fontes revogadas ou alteradas invalidam derivados. | [Recuperação documental](evidencias/e2-recuperacao-documental-2026-11-11.md) |
| Leitura documental | Upload e processamento durável, leitura nativa de PDF/DOCX/TXT, qualidade por trecho e progresso. Trechos duvidosos são excluídos e cobertura parcial é indicada. | [Documentos](evidencias/e2-documentos-2026-11-11.md) |
| Memória de conversa | Resumo versionado, proveniência e recuperação lexical/cronológica das mensagens originais. Histórico integral preservado, correções recentes priorizadas e contexto derivado invalidável. | [Memória](evidencias/e2-memoria-conversa-2026-11-12.md) |
| Análise extensa | Leitura por lotes, sínteses com citações literais, consolidação e cobertura persistida. Checkpoints e reaproveitamento seletivo de partes confirmadas. | [Análise durável](evidencias/e2-analise-documental-duravel-2026-11-12.md) |
| Geração durável | Pedido do chat v2 persistido antes da geração, worker com leases/fence, progresso, reconexão e retomada explícita. Desconectar o chat não cancela o trabalho. | [Geração durável](evidencias/e2-e3-geracao-duravel-2026-11-12.md) |
| Minutas por seções | Planejamento, geração e revisão de seções com checkpoints. Uma proposta integral é publicada após as verificações, sem expor rascunho parcial como aprovável nem exigir “continue”. | [Geração por seções](evidencias/e2-e3-geracao-duravel-2026-11-12.md) |
| Consumo técnico | Reserva/ledger por chamada física, somatório por execução e limites de tokens, chamadas e duração. Chamada de resultado indeterminado não é repetida automaticamente. | [Geração durável](evidencias/e2-e3-geracao-duravel-2026-11-12.md) |
| Histórico pessoal | Sessão separada, leitura do próprio histórico e download de anexos privados mesmo após perder vínculo organizacional, com conta ativa. Sem acesso ao Caso/Peça atuais, geração ou aplicação. | [Histórico pessoal](evidencias/e1-historico-pessoal-exclusao-2026-11-12.md), [frontend](../../../../JuridicoAi-frontend/docs/assistant-personal-history.md) |
| Exclusão autorizada | Confirmação explícita, protocolo idempotente, bloqueio imediato da conversa, cancelamento/fence e worker de expurgo do ambiente ativo. Preserva texto aprovado na Peça e arquivos do Caso. Não há expiração automática do chat. | [Exclusão](evidencias/e1-historico-pessoal-exclusao-2026-11-12.md) |
| Avaliação | Suíte conversacional com sete cenários/nove interações fictícias e runner com reserva de custo. Recorte de quatro cenários/seis interações executado com GPT-6.1 Terra: checks técnicos aprovados; avaliação ampla e revisão jurídica pendentes. | [Suíte](../../../avaliacoes/assistente-conversacional/README.md), [preflight](../../../avaliacoes/assistente-conversacional/preflight-2026-11-12.md) |

## Verificações já registradas

Resultados anteriores de backend/frontend preservados abaixo; a avaliação real
reduzida foi executada nesta atualização e está identificada separadamente:

- Backend: **287 testes aprovados e 1 teste de OCR ignorado** na rodada local de
  2026-11-14, por indisponibilidade do runtime OCR. A rodada anterior de 2026-11-12
  tinha 281 aprovados. Inclui PostgreSQL, transações, autorização, jobs, exclusão,
  reagendamento, admissão e medições de latência.
- Frontend: **133 testes unitários**, build e lint aprovados.
- Playwright: **30 cenários focados distintos aprovados**, incluindo histórico
  pessoal, conversa livre, histórico e revisão persistida. Não corresponde à
  aprovação da suíte E2E inteira do produto; falhas de regressão anteriores estão
  registradas na [evidência do editor](evidencias/e0-editor-2026-11-11.md).
- API HTTP e PostgreSQL reais, com dados fictícios: geração durável com provedor
  simulado; histórico pessoal, separação de sessões e exclusão sem chamadas de IA.
  Ver [geração](evidencias/e2-e3-geracao-duravel-2026-11-12.md) e
  [histórico pessoal](evidencias/e1-historico-pessoal-exclusao-2026-11-12.md).
- Ferramenta de avaliação: **24 testes offline aprovados** em 2026-11-14 (20 na
  preparação anterior), incluindo extração protegida dos tempos, e validação estrutural
  das suítes. A [preparação](../../../avaliacoes/assistente-conversacional/preflight-2026-11-12.md)
  original fez **zero chamadas ao modelo real e incorreu em US$ 0 de geração**.
- Rodada real reduzida: **6 interações, 18 chamadas físicas, zero falhas nos checks**,
  custo calculado de **US$ 0,04549805**, uso conciliado com o ledger. API/PostgreSQL
  descartáveis encerrados. Ver [resultados e limitações](evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).
- M01 local: regressão de espera adicional de 60 s reproduzida/corrigida;
  tentativas negadas não consomem cota. API/PostgreSQL e transporte em memória
  verificaram as medições separadas, sem representar benchmark do modelo. Ver
  [diagnóstico e testes](evidencias/m01-latencia-local-2026-11-14.md).
- M01 real: três interações aprovadas; consulta de **63,941 s para 22,697 s**.
  Provedor levou 2,555 s; o limite local dominou a espera restante. Onze chamadas,
  custo US$ 0,03260130, sem uso incerto. Ver [confirmação real](evidencias/m01-latencia-real-2026-11-14.md).

Testes com provedor simulado comprovam mecanismos e contratos. A rodada real
acrescenta evidência limitada aos quatro cenários executados; fidelidade às fontes,
memória extensa e qualidade jurídica ainda exigem avaliação ampliada/revisão humana. A baseline antiga de respostas isoladas não
homologa o novo fluxo conversacional.

## Limites e condições atuais

- As flags de revisão, conversa, intenção automática, geração durável, memória
  e documentos estão **desligadas por padrão** na configuração versionada. Há
  permissões de IA por organização; falta o gate específico de rollout v2.
- Histórico pessoal e processamento de exclusões aceitas não dependem de manter
  as flags de geração habilitadas nem do vínculo organizacional do titular.
- O endpoint v1 independente conserva o fluxo anterior ligado à requisição.
  A durabilidade geral entregue é a do chat v2; o contrato legado ainda existe.
- O pipeline OCR existe, mas permanece desativado e sem calibração aprovada.
  Imagens e regiões digitalizadas continuam sinalizadas como não lidas.
- Há limites técnicos por execução, incluindo até 12 chamadas físicas/180 mil
  tokens na geração durável. Isso não implementa teto financeiro distribuído de
  produção nem garante geração de qualquer tamanho. Revisão interna da minuta
  não certifica mérito jurídico ou veracidade de suas fontes.
- Exclusão concluída informa `RemovidaDoAmbienteAtivo`. Não há comprovação de
  expurgo dos backups; `EliminadaDasCopias` permanece falso.

## Pontos restantes

Os identificadores abaixo são compartilhados com o TODO e o plano. Há trabalho
de código, avaliação, configuração e operação; cada tipo tem conclusão própria.
Valores de orçamento, retenção, alertas e critérios do piloto ainda precisam ser
fechados, sem tratar propostas do desenho como SLAs aprovados.

| ID | Ponto restante | Natureza e condição de conclusão |
|---|---|---|
| P01 | Completar avaliação conversacional real | **Avaliação.** Recorte inicial concluído: quatro cenários/seis interações, GPT-6.1 Terra, US$ 0,04549805. Restam CONV-002/003/004. A comparação real de M01 foi concluída; política de espera restante está em M03. Definir orçamento adicional e credencial temporária para nova rodada; registrar/corrigir achados. |
| P02 | Homologar qualidade jurídica e fidelidade às fontes | **Revisão humana.** Revisar os artefatos reais de P01 com a rubrica, conferir fatos/citações e aprovar ou registrar limitações. Aprovação de checks automáticos não encerra esta etapa. |
| P03 | Completar controle financeiro de produção | **Implementação + configuração.** Definir preços e limites financeiros do piloto, reserva/coordenação de consumo agregado entre instâncias e tratamento visível ao atingir limites ou haver custo incerto. O proxy de orçamento da avaliação não substitui esse controle do produto. |
| P04 | Preparar monitoramento operacional | **Instrumentação + operação.** Fechar métricas, alertas e rotina de resposta para custo, falhas, jobs sem progresso, leases e exclusões pendentes. Usar metadados técnicos, sem conteúdo jurídico ou credenciais nos logs. |
| P05 | Proteger exclusões após restauração | **Implementação + infraestrutura + ensaio.** Provisionar registro mínimo externo de autorizações, definir retenção e automatizar reaplicação das exclusões. Ensaiar restauração de banco/arquivos isolada e comprovar ausência dos chats excluídos antes de liberar tráfego. [Procedimento](especificacoes/06-operacao-exclusao-restauracao.md). |
| P06 | Preparar ativação controlada do v2 | **Implementação + validação.** Completar gate por organização separado das flags operacionais e conferir seleção v1/v2, permissões e jornadas multiusuário nas condições do piloto. Validar com modelo real a integração já exercitada por simulação. |
| P07 | Fechar compatibilidade e experiência do editor | **Validação + documentação.** Consolidar fixtures do schema existente, incluindo marks/listas/links/headings/emoji/blocos desconhecidos; ampliar verificação de documentos longos, acessibilidade, paginação de revisão, undo/autosave e exportação sem previews. Registrar versões/licenças das dependências e revisar o aviso vigente. Corrigir regressões necessárias à jornada; a suíte E2E completa ainda não está homologada. |
| P08 | Preparar deploy e rollback do piloto | **Configuração + ensaio operacional.** Aplicar migrations aditivas, drenar workers antigos, conferir temporários legados e permissões/volume persistente; configurar HTTPS e origens pessoais. Ensaiar rollback sem apagar dados v2 nem reabrir conversas em exclusão. [Checklist operacional](especificacoes/06-operacao-exclusao-restauracao.md). |
| P09 | Calibrar OCR | **Em espera.** Retomar somente quando repriorizado. Usar corpus representativo, medir cobertura/qualidade e aprovar critérios antes de ativar. [Registro da pausa](../../standBy/calibracao-ocr/README.md). |

O aviso já menciona persistência e anexos privados e possui versionamento de
aceite; P07 trata de sua revisão final, não da criação dessa funcionalidade.
A comparação, o compilador, os recibos e a transação já existem; os testes mais
amplos de P07 não devem reaparecer como ausência dessas implementações.

## Próxima etapa

O próximo desenvolvimento é **P03 — controle financeiro de produção**. O recorte
local de M01 está implementado/testado: reagendamento e admissão corrigidos,
medições por etapa disponíveis. A comparação real também foi concluída: 22,697 s
na consulta, ante 63,941 s. A espera remanescente por cota está em M03; esse ganho
observado não foi inferido da prova sintética nem configura SLA.

Revisar os resultados reais com a rubrica (**P02**). A primeira rodada de P01 já está
concluída no [recorte reduzido](../../../avaliacoes/assistente-conversacional/rodada-1usd.md):
quatro cenários/seis interações, abaixo do teto total de US$ 1,00. Os três cenários
adiados permanecem pendentes e exigem orçamento adicional definido para execução.

P03–P08 continuam como condições para liberar o piloto. M01 está concluído no
recorte de latência; M03 acompanha a política de cotas. M02 registra a correção concluída
na preparação do teste de isolamento. P09 não bloqueia a
continuidade do escopo ativo de documentos nativos. A chave foi temporária e não
foi mantida configurada para novas chamadas. Não houve ativação em produção.
