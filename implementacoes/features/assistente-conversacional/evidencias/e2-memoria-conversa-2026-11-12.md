# Memória versionada e recuperação do histórico

Data: 2026-11-12. Implementação local no backend e no frontend, atrás de flag.
Complementa a [recuperação documental](e2-recuperacao-documental-2026-11-11.md).
OCR permanece em stand by. Nenhum deploy, commit ou push nesta etapa.

## Comportamento entregue

A conversa extensa usa um resumo privado versionado, mensagens recentes completas
e consulta aos originais. O histórico integral não é substituído nem excluído.
A Peça atual continua determinando o texto efetivamente aplicado; rascunhos,
decisões declaradas pelo usuário e decisões registradas das propostas não são
tratados como se fossem a mesma coisa.

A compactação é automática. Não existe uma nova confirmação para organizar o
contexto. Alterar a Peça continua exigindo a comparação e aprovação visual já
existentes. O painel mostra “Contexto da conversa”, com explicação do uso de resumo,
aviso de fontes desconsideradas e de mensagens ainda fora do resumo. Esse estado
é persistido e pode ser recuperado mesmo se a geração for interrompida.

## Montagem e atualização

`AssistenteHistoryService` monta o contexto no banco durante a reserva do pedido;
a transação termina antes de qualquer chamada ao provedor. O orquestrador conserva
as mensagens recentes completas dentro do orçamento de caracteres. Até 64 mensagens
novas desde o resumo podem permanecer sem compactação; ao superar esse limite,
retém inicialmente as 16 mais recentes. O limite de caracteres também pode
antecipar a compactação. Assim, a atualização não exige uma chamada extra a cada
mensagem curta.

Quando há mensagens antigas a compactar, o contexto inclui um lote cronológico
completo de até 100 mensagens, limitado pelo espaço disponível. A primeira rodada
do provedor exige a função `atualizar_memoria` por `tool_choice`. O modelo recebe
somente a memória anterior ainda elegível e o lote explícito. O servidor valida:

- Até 40 registros, com categoria, texto e uma a quatro origens.
- Citação literal de 4 a 300 caracteres encontrada no texto da mensagem original
  indicada, na memória anterior válida ou no lote fornecido.
- Sequência e ID pertencentes à mesma conversa, dentro do conjunto oferecido.
- Origem real e hash calculados pelo servidor, não escolhidos pelo modelo.
- Conteúdo de assistente somente como `Rascunho`; não pode virar fato informado
  pelo usuário, preferência ou decisão factual por efeito da compactação.
- Tamanho do registro e do resumo completo, estado ativo da resposta e versão
  anterior da memória sob lock da conversa antes do commit.

As categorias são FatoInformado, Correcao, Preferencia, Decisao, Pendencia,
Conflito e Rascunho. Decisao registra declaração do usuário, sem comprovar aplicação.
A política dá precedência às correções explícitas posteriores, incluindo as
mensagens recentes e o pedido atual. Contradições não resolvidas devem manter
ambas as origens. O hash comprova a versão da origem, não a fidelidade semântica
da síntese; essa qualidade ainda exige avaliação com o modelo real.

A versão salva registra `ate_sequencia`, política e resposta que a produziu.
Versões anteriores e mensagens originais permanecem. Um pedido pode criar uma
versão; novas atualizações avançam sobre os lotes restantes nos pedidos seguintes.
Uma faixa ainda não resumida fica explicitamente indicada e disponível para consulta.
`ate_sequencia` significa faixa processada para síntese seletiva, não garantia de
que todos os detalhes foram retidos no resumo. A síntese não equivale a leitura
integral de todos os documentos ou de toda a conversa numa nova geração.

A atualização faz parte do mesmo ciclo de ferramentas, timeout, limite acumulado
de tokens e ledger da geração. Cada chamada física é registrada uma vez. Não há
chamada paralela, repetição paga automática de falhas HTTP nem mudança de modelo.
A escolha forçada de função segue a documentação oficial de
[Function calling](https://developers.openai.com/api/docs/guides/function-calling).
Mantidos `store=false`, estado manual na aplicação e replay canônico restrito
à execução corrente.

## Recuperação e autorização

`consultar_historico` recebe `consulta` nullable, `aposSequencia`, `ateSequencia`
nullable e `limite` até 12. O limite superior é a última sequência anterior ao
pedido corrente; nem mensagens futuras nem outra conversa entram na consulta.

A busca textual usa full-text em português e texto exato com curingas escapados,
restrita às declarações originais do usuário, incluindo contexto adicional.
Ela não pesquisa respostas antigas do assistente como fonte factual. Consulta
por intervalo (`consulta=null`) retorna os dois autores pela projeção de elegibilidade,
com estados atuais das propostas e grupos. Os resultados têm sequência, ID,
autor e cursor; são mensagens completas, sem corte silencioso de texto. Mensagem
que não cabe no limite retorna erro explícito, preservando os originais.

`AssistenteHistoryProjection` compartilha as regras do histórico básico: respostas
rejeitadas/incompletas não entram no novo contexto; conteúdo de assistente ligado
a Peça antiga, dados compartilhados de Caso ou manifesto documental inelegível
é omitido, mantendo o histórico original legível. A consulta cronológica não
promove conteúdo omitido a fonte confiável.

Cada registro da memória é revalidado contra a projeção atual das mensagens
citadas. Mudança na Peça, decisões de propostas, exclusão/reprocessamento de fonte
ou alteração de qualidade invalida os registros dependentes. A invalidação não
apaga declarações independentes do usuário. Origens de toda a entrada fornecida,
incluindo consultas posteriores, são verificadas antes da contagem inicial,
antes das chamadas seguintes ao provedor e na publicação final. Mudança durante
a execução interrompe o resultado tardio com `AI_HISTORY_SOURCE_CHANGED`.

A autorização atual exige titularidade, organização, acesso à Peça, feature de
chat, flags e transparência confirmada. Cancelamento, expiração e desabilitação
impedem novas ferramentas; um checkpoint já confirmado permanece salvo. Não foi
implementada nesta etapa a área pessoal após perda de vínculo.

## Dados, ativação e rollback

Migration `20261112120759_AddAssistantVersionedMemory`:

- `ai_memories`: versões append-only, JSONB com registros/proveniência, sequência
  coberta, política, timestamps e FKs compostas restritivas.
- Unicidade `(conversa_id, versao)` e `resposta_id` para evitar dupla atualização.
- `ai_messages.contexto_historico`: metadados públicos da montagem do contexto.
- Índice GIN parcial sobre `to_tsvector('portuguese', texto)` de mensagens de usuário.

Downgrade que apagaria memória ou metadados existentes é bloqueado. Rollback
funcional: desativar `AssistenteIa:HistoryMemoryEnabled`, preservando dados.
Não foi aplicada migration nem alterada configuração no banco do usuário.

`HistoryMemoryEnabled=false` por padrão. Para habilitar, aplicar as migrations e
habilitar também Enabled, ReviewV2Enabled, ConversationV2Enabled,
AutomaticConversationEnabled e AI_ASSISTANT da organização, com o modelo
configurado e a transparência confirmada. Memória não depende de habilitar documentos
ou recuperação documental. Quando ambos estão ativos, as ferramentas compartilham
as restrições mais conservadoras de rodadas e consumo.

| Configuração em AssistenteIa | Default |
|---|---|
| HistoryCompactionMessages | 64 |
| HistoryRecentMessages | 16 |
| HistoryRecentCharacters | 16.000 |
| HistoryBatchCharacters | 24.000 |
| HistoryMemoryCharacters | 12.000 |
| HistoryToolRounds | 8, mais a rodada final sem ferramentas |
| HistoryTotalTokens | 180.000 de entrada+saída acumuladas |

Continuam valendo MaxTotalContextCharacters, MaxInputTokens, MaxOutputTokens e
TimeoutSeconds. Se memória obrigatória ou consulta não couber, o limite gera
resposta informativa, sem proposta truncada aplicável. O adaptador anterior e o
comportamento com a flag desligada permanecem disponíveis.

## Verificação local

- PostgreSQL descartável real: mais de mil mensagens, síntese com proveniência,
  busca de detalhe fora da janela, reabertura, idempotência, correções, decisões
  rejeitadas, isolamento, cursor congelado, wildcard, invalidação por versão e
  reprocessamento, cancelamento, falha transacional e flags.
- Protocolo Responses com provedor simulado: função obrigatória, recusa de função
  indevida/resultado prematuro, contagem com o mesmo tool_choice e schema sem
  cobertura documental quando só há memória, uso por chamada e limites.
- Frontend: SSE com progresso de contexto e validação, preservação após
  interrupção/reload, comparação visual e aprovação como única aplicação.

| Verificação | Resultado |
|---|---|
| Backend completo com PostgreSQL real | 242 passaram, 1 teste de OCR local ignorado por ausência do runtime de teste |
| Recorte específico de memória | 14 passaram, incluídos na suíte completa |
| Vitest | 123 passaram |
| Playwright: conversa livre, histórico e revisão persistida | 20 passaram |
| Build de backend/frontend e lint do frontend | Passaram; aviso de bundle >500KB já existente |
| EF has-pending-model-changes | Sem mudanças pendentes |
| HTTP com API e PostgreSQL reais, provedor simulado | Resumo + recuperação + resposta, reabertura e replay idempotente passaram |

Na integração HTTP foram preservadas 1.004 mensagens originais e adicionadas
somente as duas mensagens do pedido, totalizando 1.006. Uma memória foi persistida;
o modelo simulado consultou “Helena” fora da janela recente/lote de compactação,
recuperando o valor R$ 876,54 da sequência 777. Foram três chamadas e três
contagens de entrada; o ledger contém somente três lançamentos, ordinais 1/2/3,
900 tokens de entrada e 240 de saída sintéticos, sem lançamento agregado duplicado.
O replay do mesmo pedido não chamou o provedor; a metadata foi recuperada no
histórico e a Peça manteve a versão original. API e provedor locais usaram portas
dinâmicas 36519 e 43823, sem utilizar 5173; serviços encerrados após a verificação.
A primeira massa manual tinha enums em formato externo no JSON interno persistido;
o fixture foi corrigido para o formato numérico usado pela aplicação. O servidor
simulado também foi ajustado para aceitar Transfer-Encoding chunked. Esses ajustes
foram restritos ao harness temporário, sem alteração dos contratos da aplicação.

Os testes usam dados e respostas sintéticas. Nenhuma chamada à OpenAI foi feita.
OCR não foi ativado nem calibrado. A compactação é seletiva; recuperação lexical
pode não localizar paráfrases, e a interpretação de correções e a fidelidade da
síntese precisam de avaliação real antes da liberação. Leitura documental extensa
com resumos/checkpoints, geração durável e exclusão manual autorizada continuam
como itens separados do escopo.
