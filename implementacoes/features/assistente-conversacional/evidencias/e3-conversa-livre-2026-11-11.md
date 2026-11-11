# Conversa livre e propostas automáticas

Data: 2026-11-11. Recorte de E3 integrado às etapas de histórico e revisão.
Implementação local no backend e em `../JuridicoAi-frontend`, sem deploy,
commit ou push. Não conclui documentos, memória extensa ou execução durável.

## Comportamento entregue

- O usuário escreve normalmente, sem escolher Perguntar/Reescrever/Redigir.
  O modelo distingue resposta informativa, edição e minuta inicial na mesma
  geração, com saída estruturada. Não há classificador separado nem regras de
  palavras-chave para decidir a intenção em produção.
- Perguntas recebem uma resposta no chat. Pedidos claros de mudança produzem
  diretamente uma proposta persistida e abrem a comparação no documento.
  Não há a etapa intermediária “deseja preparar/aplicar?”. Ambiguidade que
  impeça definir a mudança ainda pode gerar uma pergunta de esclarecimento.
- O usuário aprova na comparação. Até esse momento, preparar ou ajustar uma
  proposta não altera a Peça. A aplicação continua determinística, versionada e
  transacional; o modelo não recebe ferramenta de gravação.
- O próximo pedido usa a Peça atual e o histórico elegível da conversa privada.
  Selecionar um trecho é opcional. A seleção só acompanha o pedido enquanto
  corresponder à revisão capturada no editor.
- Uma proposta pendente pode ser referenciada no pedido seguinte. Se o modelo
  indicar substituição, o servidor publica a nova proposta e marca a anterior
  como substituída na mesma transação. Grupos já aprovados permanecem aplicados.
  A proposta anterior continua legível, mas não volta a ser aprovável.
- Alteração concorrente da Peça preserva o resultado como desatualizado e
  impede aplicação. Uma publicação desatualizada não substitui a proposta anterior.
- Reabrir um cartão consulta a proposta canônica. Replay de request concluído
  também recupera o estado atual, sem repetir a chamada ao provedor.

## Implementação e fronteiras

Novo `POST /assistente/v2/conversas/{id}/interacoes`, com JSON estrito:

```json
{
  "clientRequestId": "f114ea70-48ad-4f14-a148-4592cefc20cd",
  "texto": "Melhore a redação dos fatos preservando os valores informados.",
  "versaoPecaEsperada": 3
}
```

Campos opcionais: `selecao`, `propostaReferenciadaId`, `contextoAdicional`,
`incluirDadosCaso` e `contextoCasoIds`. O contrato efetivo está em
`EnviarInteracaoLivreRequest`; não aceita modo ou operação enviados pelo cliente.
A referência precisa pertencer ao titular, organização e conversa atuais.
O POST retorna SSE 200 no protocolo existente, com proposta canônica no evento
concluído. O endpoint `/mensagens` permanece como adaptador dos modos anteriores.
Não se declara implementado o contrato planejado HTTP 202 com worker/eventos duráveis.

O provedor Responses existente é reutilizado, com `store=false`, contagem de
tokens e ledger. A política nova é `assistente-livre-v1`, acrescida da política
conversacional. A política legada mantém sua versão e instruções anteriores.
O schema automático é fechado e recursivo, com intenções e nós TipTap permitidos;
o servidor confere intenção, permissões, grupos e compilação antes de publicar.
O mecanismo segue a documentação de
[Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs).
Schema válido não comprova qualidade semântica ou jurídica.

As alterações são no corpo do documento. Metadados, margens, cabeçalho e rodapé
não são ferramentas deste recorte. Histórico de propostas inclui decisões e
substituições para distinguir rascunhos pendentes de conteúdo aprovado.

## Ativação, acesso e rollback

Aplicar, em ordem, as migrations:

1. `20261111132819_AddAssistantReviewWorkspace`.
2. `20261111142012_PersistAssistantConversationMessages`.
3. `20261111182103_TrackSupersededAssistantProposals`.

Em ambiente de teste, com provedor e controles operacionais configurados:

```text
AssistenteIa__ReviewV2Enabled=true
AssistenteIa__ConversationV2Enabled=true
AssistenteIa__AutomaticConversationEnabled=true
```

As três flags ficam desligadas por padrão. A terceira exige as anteriores.
`suportaConversaLivre` ativa o fluxo na UI; sem essa capacidade, permanece o
adaptador anterior. O aviso de persistência/uso do histórico da etapa anterior
continua vigente; não há nova finalidade ou envio de anexos nesta etapa.

`AI_ASSISTANT` permite a conversa; `AI_EDIT` permite propostas de edição e a
aplicação; `AI_DOCUMENT_GENERATION` também é exigida para minuta inicial ou
substituição integral do corpo. A cobertura é conferida sobre o conjunto das
operações, independentemente da intenção declarada pelo modelo. Por prudência,
substituir o único bloco significativo de uma Peça também exige geração integral.
Uma edição localizada que preserve outros blocos significativos exige apenas
edição. As permissões são revalidadas antes da publicação e da aplicação.

A migration adiciona vínculo de substituição com FK composta restritiva, sem
exclusão de mensagens. Rollback funcional desliga a flag e preserva os dados.
O downgrade é bloqueado quando apagaria vínculos de substituição, evitando
reativar propostas antigas. A revisão salva continua disponível com sua própria
flag e permissões. Banco, configuração e permissões do ambiente do usuário não
foram alterados; migrations só foram executadas em PostgreSQL descartável.

## Evidências

| Verificação | Resultado |
|---|---|
| Backend, incluindo integração PostgreSQL descartável | 192 testes passaram, sem skips |
| Frontend Vitest | 114 testes passaram |
| Playwright: conversa livre, histórico e revisão local/persistida | 18 testes passaram |
| Build e lint frontend | Passaram; permanece aviso de bundle maior que 500 kB |
| EF `has-pending-model-changes` | Modelo coincide com migrations |
| Schema de contratos e exemplos fictícios | 9 exemplos validados |
| Fluxo com navegador, frontend, API e PostgreSQL reais | Relato, correção, minuta, ajuste pendente, aprovação, edição localizada após revogar geração integral e reload |

Os testes novos cobrem geração única sem classificador, JSON inválido/incompleto,
catálogo por permissão, publicação sem escrita na Peça, substituição preservando
grupos aplicados, revogação de acesso, cobertura integral e rollback de publicação.
No navegador, a minuta abre diretamente o preview; a aprovação é a única escrita.
O histórico recarregado contém os pedidos/respostas sem duplicações técnicas.

O teste integrado usou contas sintéticas, conteúdo fictício e provedor simulado
local. Os schemas de dez requests capturados nesse teste foram conferidos como
JSON Schema válido, com objetos fechados e todas as propriedades obrigatórias.
Captura inspecionada em
`../JuridicoAi-frontend/test-results/assistant-free-chat-api.png`, ignorada pelo Git.
Não houve chamada ao modelo real: naturalidade, classificação semântica e qualidade
jurídica ainda precisam de avaliação com o modelo configurado. A suíte legada
completa de Playwright não é declarada aprovada; as falhas anteriores estão na E0.

## Limites e próximo trabalho

- Geração ainda ligada ao request SSE, sem worker, retomada ou checkpoints.
- Contexto básico limitado a 80.000 caracteres por padrão e 1.000 mensagens,
  com erro explícito em excesso; ainda sem resumo versionado/busca histórica.
- Saída limitada pela configuração atual (6.000 tokens por padrão), com até
  20.000 caracteres de texto explicativo; compilador limita o JSON do documento
  a 1.048.576 caracteres e
  30.000 nós. Respostas incompletas não viram propostas aplicáveis. Geração
  extensa em etapas continua pendente.
- Upload privado, leitura/OCR e recuperação documental automática ainda pendentes.
  Contextos explícitos do Caso existentes permanecem sujeitos à autorização.
- Área pessoal após perda de vínculo e exclusão manual autorizada ainda pendentes.
- Avaliação semântica com modelo real e calibração operacional antes da liberação.
