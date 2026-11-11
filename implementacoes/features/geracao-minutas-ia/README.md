# Geração de minutas por pedido livre na Peça

> **Estado:** ainda a implementar
>
> **Plano atual:** incorporado ao [assistente conversacional](../assistente-conversacional/README.md),
> cuja E0 do editor foi iniciada. Este documento preserva o planejamento anterior.
>
> **Data:** 2026-10-23
>
> **Origem:** validação manual com o proprietário do projeto
>
> **Relação:** assume o fluxo do PA-16 e a parte do PA-18/E4 necessária para
> reconhecer pedidos de criação de minuta

## Problema observado

O usuário colou no chat da Peça um pedido de elaboração de petição inicial com
fatos fictícios. Esperava receber uma minuta utilizável, com revisão e aplicação
explícitas. Relatou avisos e depois confirmou uma operação `ResponderNoPainel`,
sem aplicação na Peça.

O pedido livre atual começa em `Perguntar`. O frontend envia esse modo e o
backend restringe suas operações a `ResponderNoPainel`. Pedir uma minuta no
texto não muda automaticamente o modo nem cria uma proposta de documento.

Essa limitação foi identificada no código, mas o payload e a resposta completos
da execução manual não foram capturados. A ausência relatada do texto da minuta
continua exigindo diagnóstico: `ResponderNoPainel` limita a aplicação, mas não
explica sozinho uma resposta somente com alertas.

**Atualização da validação em 2026-10-24:** o usuário apresentou uma minuta
gerada com os dados fictícios. A produção do texto foi observada nessa nova
tentativa; modo/operação efetivos, aplicação e persistência ainda não foram
confirmados. Esse resultado não comprova intenção automática ou entrega do
fluxo próprio de documento completo. O acompanhamento está na
[evidência manual](evidencias/validacao-manual-experiencia-2026-10-23.md#vm-03--minuta-apresentada-pelo-usuário-em-2026-10-24).

## Objetivo de produto

Dentro de uma Peça aberta, o usuário descreve o caso em linguagem natural,
recebe uma minuta para revisar e escolhe inseri-la no documento. Não precisa
conhecer modos ou operações internas.

Fluxo esperado:

1. Abrir uma Peça e escrever um pedido como “elabore uma minuta com estes fatos”.
2. Receber o texto da minuta em uma proposta separada do documento.
3. Ver dados faltantes e observações em um bloco separado.
4. Revisar e, se desejar, editar a proposta.
5. Confirmar a aplicação em um destino apresentado claramente.
6. Aguardar o autosave e poder desfazer a aplicação.

Gerar não altera a Peça. Cancelar, rejeitar ou falhar preserva o documento.

## O que já pode ser reutilizado

| Base existente | Limite atual |
|---|---|
| Chat, chips e composer | pedido livre começa em pergunta, sem intenção automática |
| Provider com saída estruturada e alertas | contrato distingue modos e operações de trechos, sem geração integral própria |
| Propostas editáveis e aplicação explícita | inserção e substituição existentes não definem o fluxo de documento completo |
| Âncora, autosave e undo/redo | precisam de cobertura específica para a nova operação |
| Flags e kill switch | `AI_DOCUMENT_GENERATION` é persistível, mas não anunciada nem atendida por fluxo próprio |
| Limites e ledger | há teto único de saída; faltam perfil e medição específicos da minuta |

Referências do código atual:

- [DTOs e operações](../../../src/JuridicoAI.Api/src/Application/DTOs/AssistenteIaDtos.cs).
- [Preparação, permissões e validação da saída](../../../src/JuridicoAI.Api/src/Application/Services/AssistenteIaApplicationService.cs).
- [Prompt interno](../../../src/JuridicoAI.Api/src/Application/Services/AssistentePromptPolicy.cs).
- [Flags e capacidades](../../../src/JuridicoAI.Api/src/Application/Services/AssistenteFeatureAccessService.cs).
- Frontend, no repositório irmão `JuridicoAi-frontend`:
  `src/features/ai-assistant/components/assistant-panel.tsx` e
  `src/features/ai-assistant/editor.ts`.

## Primeira entrega proposta

- Reconhecer o pedido livre de criação de minuta sem exigir ação técnica prévia.
- Definir contrato tipado que diferencie documento completo de trecho ou seção.
- Calcular no backend as operações permitidas pelas flags, pelo contexto e pelo
  destino; validar a recomendação do modelo antes de expor aplicação.
- Definir e exigir a autorização específica de geração integral, preservando
  os controles globais, a transparência e o isolamento por Organization.
- Gerar uma proposta completa, com texto editável e avisos separados.
- Usar `[PREENCHER]` para lacunas que permitam elaborar uma minuta preliminar.
  Não inventar fatos, fontes ou dados para preencher essas lacunas.
- Distinguir aviso de revisão, necessidade real de esclarecimento e erro de
  geração. A presença de alertas não deve, por si só, substituir o texto pedido.
- Exigir confirmação explícita para aplicar, com destino claro e proteção do
  conteúdo que já estiver na Peça.
- Definir limites de entrada/saída próprios e apresentar falha controlada quando
  a geração estiver incompleta; não aplicar texto truncado como minuta completa.
- Reutilizar autosave e undo/redo, impedindo aplicação duplicada ou em documento
  alterado desde o pedido.

O modelo só pode recomendar operações autorizadas pelo backend. Reconhecimento
de intenção não será baseado apenas em palavras-chave no frontend, nem abrirá
permissões por decisão do modelo.

## Decisões a detalhar antes de codificar

| Tema | Direção e definição restante |
|---|---|
| Contrato | definir entrada conversacional, tipo de proposta de documento e compatibilidade com clientes atuais |
| Destino | definir aplicação em Peça vazia e comportamento em Peça com conteúdo; nunca substituir conteúdo existente silenciosamente |
| Formatação | definir títulos, parágrafos e listas; a inserção atual converte texto em parágrafos, sem composição de documento completo |
| Contexto | enviar somente dados e textos escolhidos pelo usuário, sem anexar automaticamente o Caso ou o documento inteiro |
| Lacunas | gerar campos pendentes quando possível e pedir esclarecimento quando uma ambiguidade impedir uma proposta útil |
| Limites | avaliar o perfil `Document` de até 8.000 tokens proposto no PA-01, compatibilizar limite de caracteres e detectar resposta incompleta |
| Histórico | primeira entrega com pedido autossuficiente; não prometer que mensagens anteriores são contexto enquanto isso não existir |

Essas definições detalham a proposta; não são declaração de implementação ou
aprovação de um contrato técnico final.

## Plano de implementação

- [ ] Reproduzir o teste manual e capturar modo/operação e resultado completo,
  sem registrar conteúdo jurídico ou dados sensíveis em logs de produção.
- [ ] Definir o contrato de geração integral e o tratamento da intenção livre.
- [ ] Implementar autorização, operações permitidas e capacidades no backend.
- [ ] Versionar o prompt para geração de minuta com lacunas e alertas separados.
- [ ] Configurar limites específicos e tratamento de saída incompleta.
- [ ] Implementar apresentação da proposta e fluxo explícito de aplicação.
- [ ] Validar preservação do conteúdo existente, âncora, autosave e undo/redo.
- [ ] Adaptar testes backend, componentes e E2E aos cenários de aceite.
- [ ] Atualizar e executar as avaliações pertinentes quando prompt/contrato
  mudarem, sem atribuir à nova configuração a aprovação da baseline antiga.
- [ ] Repetir a validação manual com o proprietário do projeto e registrar o resultado.

## Critérios de aceite

- [ ] Colar o prompt fictício no chat gera uma proposta de minuta sem escolher `Redigir`.
- [ ] Uma pergunta informativa continua retornando resposta sem proposta de escrita indevida.
- [ ] A autorização da organização é validada pelo backend para geração integral.
- [ ] A resposta contém a minuta solicitada; alertas não substituem o conteúdo útil.
- [ ] Dados ausentes são apresentados como campos pendentes ou esclarecimentos justificados.
- [ ] Fatos e referências não são inventados para completar a minuta.
- [ ] Avisos ficam separados do texto aplicado na Peça.
- [ ] A geração, o cancelamento e a rejeição não alteram o documento.
- [ ] A aplicação mostra destino e exige confirmação explícita.
- [ ] Uma Peça com conteúdo não sofre substituição silenciosa.
- [ ] Alterar a Peça durante a geração bloqueia aplicação em destino desatualizado.
- [ ] A proposta só pode ser aplicada uma vez e com autosave em `SALVO`.
- [ ] Undo/redo e persistência após recarregar funcionam após aplicação.
- [ ] Saída inválida, incompleta ou acima do limite produz feedback claro.
- [ ] Testes pertinentes e validação manual do novo fluxo estão registrados.

## Evoluções posteriores em espera

Revisão e aplicação por seção, comparação do documento inteiro, histórico
persistente e ajustes que utilizem mensagens anteriores não estão incluídos
implicitamente na primeira entrega. A intenção automática geral para revisão
e reescrita permanece no [backlog em espera](../../standBy/README.md).

Os gates de segurança operacional, acessibilidade e validação jurídica mantêm
seus critérios antes de piloto externo, produção ou comercialização.

## Estimativa inicial

Complexidade média, estimada em **4–6 dias úteis (32–48 horas)** para a primeira
entrega e sua validação. Uma versão com revisão por seção, comparação integral
e ajustes com contexto conversacional foi estimada em **8–12 dias úteis no
total**, sujeita ao detalhamento desses escopos.

São faixas de planejamento, não compromisso de prazo ou orçamento aprovado.
O custo de desenvolvimento depende do valor/hora. O custo operacional precisa
ser medido com tokens reais e tarifas configuradas no ledger; não há estimativa
por minuta validada nesta sessão.

## Evidências e referências

- [Sessão manual de 2026-10-23](evidencias/validacao-manual-experiencia-2026-10-23.md).
- [Prompt fictício de reprodução](evidencias/prompts/prompt-geracao-peca-ficticia.txt).
- [PA-16 anterior](../../standBy/assistente-ia/planejamento/pontos-em-aberto.md#pa-16--fluxo-seguro-de-geração-integral-da-peça).
- [Plano anterior de intenção conversacional](../../standBy/assistente-ia/planejamento/redesign-conversacional.md#e4--pedido-livre-e-intenção-conversacional).
- [Perfis de saída e operação](../../standBy/assistente-ia/decisoes/pa-01-modelo-e-operacao.md).
- [Baseline técnica anterior](../../alreadyDone/assistente-ia/decisoes/pa-09-avaliacao-qualidade-juridica.md).
