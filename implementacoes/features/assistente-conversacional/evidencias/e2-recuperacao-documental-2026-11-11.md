# Recuperação documental integrada à conversa

Data: 2026-11-11. Backend e frontend implementados localmente, atrás de flag.
Complementa a [etapa documental](e2-documentos-2026-11-11.md). OCR permanece em
stand by; resumo/compactação do histórico continua como próximo recorte.
Sem deploy, commit, push ou alteração do banco/configuração do usuário nesta etapa.

## Comportamento disponível

O chat pode consultar documentos cujo conteúdo ultrapassa o teto anterior de
60.000 caracteres. O pedido inicial recebe um catálogo de fontes autorizadas;
o modelo consulta trechos conforme a necessidade, sem o usuário escolher modos.

- `listar_fontes`: metadados paginados e indicação de intenção Relevantes/Todos.
- `buscar_fontes`: busca full-text em português e texto exato, com escopo e
  qualidade filtrados antes do ranking e limite.
- `ler_fonte`: leitura sequencial por cursor de trecho, com filtro opcional de
  página quando a extração possuir paginação. DOCX/TXT não recebem página inventada.

O conjunto de fontes fica fixado no início do pedido. Cada ferramenta verifica
permissão atual, titularidade, manifesto, revisão da extração e qualidade. Uma
fonte adicionada posteriormente não entra naquela consulta, nem ocupa o lugar de
um resultado do snapshot por efeito de LIMIT. Fontes removidas/reprocessadas
interrompem o pedido; a publicação e a aprovação continuam revalidando as fontes.
Há verificação também antes das novas chamadas ao provedor e da contagem de tokens.

Cada trecho efetivamente retornado é contado uma vez, mesmo quando aparece em
buscas repetidas. Listar nomes de arquivos não conta como leitura. A interface
mostra progresso e cobertura por arquivo: elegíveis, consultados e excluídos.
Isso registra conteúdo fornecido à IA, sem comprovar compreensão ou qualidade jurídica.
Cobertura fica salva na resposta, inclusive se houver cancelamento/interrupção.

Quando o modelo identifica pedido sobre Todos, a intenção não pode ser rebaixada
depois para Relevantes. A saída estruturada também informa coberturaPretendida.
O servidor converte qualquer proposta de leitura integral incompleta em resposta
informativa sem grupos aplicáveis. Isso inclui lacunas de extração e regiões
excluídas; consultar todos os trechos elegíveis não apaga essas lacunas.
A interpretação semântica do pedido natural ainda exige avaliação com modelo real.

Consultas seletivas podem responder ou preparar alterações, com aviso de que
outros trechos não foram revisados integralmente. A aplicação permanece restrita
à aprovação visual e transação determinística já existentes.

## Provedor, limites e contabilização

Reutilizada a Responses API com ferramentas estritas, objetos fechados,
`parallel_tool_calls=false`, saída estruturada e `store=false`. O ciclo preserva
o output canônico, incluindo itens opacos de raciocínio criptografado, apenas em
memória durante o request. Relaciona resultados por call_id. Chamadas incompletas,
paralelas ou fora da allowlist não são executadas. Nenhuma ferramenta escreve na Peça.
Base: [Function calling](https://developers.openai.com/api/docs/guides/function-calling).

Antes de cada geração há contagem da entrada efetiva, incluindo definições de
ferramentas e resultados anteriores. O endpoint de contagem recebe somente os
campos suportados, sem `include` de raciocínio da geração. Base:
[Input token counts](https://developers.openai.com/api/reference/python/resources/responses/subresources/input_tokens/methods/count).

Defaults configuráveis:

| Limite | Valor |
|---|---|
| Consultas de ferramentas | 8; depois, uma última geração com tool_choice=none |
| Input+output acumulados | 180.000 tokens, com reserva do máximo de saída antes de cada chamada |
| Entrada por chamada | MaxInputTokens existente, 60.000 por padrão |
| Contexto acumulado em caracteres | MaxTotalContextCharacters existente, 80.000 por padrão |
| Retorno de busca/leitura | Até 12 trechos e 12.000 caracteres por retorno |
| Listagem de fontes | Até 25 por página, catálogo fechado de até 250 fontes |
| Tempo total | TimeoutSeconds existente, 90s por padrão, contado desde a preparação |

Ao atingir limite, o resultado é informativo/incompleto, sem proposta truncada
aplicável. Documentos, histórico e cobertura permanecem salvos. Novas tentativas
exigem novo pedido explícito; não há repetição paga automática.

Cada chamada de geração é registrada no ledger ao concluir/falhar, com ordinal
em attempt_count e mesma solicitacao_id. O resultado público agrega o uso das
chamadas; não há um segundo lançamento agregado que duplique consumo. Uso
desconhecido continua nullable. A política é versionada como
`recuperacao-documental-v1`. Orçamento financeiro distribuído e reservas duráveis
continuam fora deste recorte; o limite acumulado aqui é por execução em tokens.

## Persistência, protocolo e ativação

Migration `20261111205546_TrackAssistantDocumentCoverage`: campo JSONB opcional
cobertura_documental em ai_messages. Não apaga mensagens ou arquivos. Downgrade
é bloqueado se apagaria cobertura existente; rollback funcional usa a flag.

Habilitar `AssistenteDocumentos__RetrievalEnabled=true` depois da migration,
com documentos e as flags de revisão/conversa/histórico já ativas. O default
permanece false; os adaptadores anteriores preservam seu comportamento.
O modelo configurado é mantido, sem fallback silencioso. Nenhuma nova finalidade
de tratamento foi adicionada além do aviso documental vigente.

O POST natural `/interacoes` permanece SSE ligado ao request. Novo evento
`contexto.documental` usa o mesmo ID/contador sequencial e traz somente cobertura
pública. A conclusão e a leitura do histórico incluem coberturaDocumental.
Frontend valida o evento e recupera o progresso salvo após reabrir o chat.

## Evidências e limites restantes

- 231 testes backend passaram, sem skips, com PostgreSQL descartável e os
  extratores locais disponíveis. Incluem replay de itens canônicos, ledger por
  chamada, contagem/budget, revogação entre rodadas, proteção de snapshot,
  leitura por página, deduplicação de cobertura e checkpoint após cancelamento.
- Frontend: 118 testes unitários e 22 E2Es de conversa/revisão/documentos;
  build e lint passaram. Permanece aviso anterior de bundle maior que 500 kB.
- Fluxo com navegador, API e PostgreSQL reais, provedor simulado: TXT de mais
  de 100.000 caracteres, alvo localizado no fim do arquivo, cobertura 1/35,
  pedido Todos incompleto sem proposta, minuta seletiva aprovada e reload.
  Conferidos sete registros de consumo, somando 2.100 tokens de entrada e
  560 de saída fictícios, sem lançamento duplicado.
- Captura inspecionada em
  `../JuridicoAi-frontend/test-results/assistant-retrieval-api.png`, ignorada no Git.
  Dados fictícios, portas livres escolhidas dinamicamente; a 5173 foi preservada.

O limite de contexto do próprio chat ainda existe. Resumo versionado, recuperação
histórica, resumos documentais com cobertura integral extensa, worker da geração,
retomada de execução e exclusão autorizada permanecem pendentes. A consulta pode
atingir limites antes de ler integralmente um conjunto grande, sinalizando isso.
OCR continua em stand by. Não houve chamada real à OpenAI nem avaliação semântica
ou jurídica com o modelo configurado. A suíte legada completa de E2E não é
declarada aprovada; falhas anteriores permanecem registradas na E0.
