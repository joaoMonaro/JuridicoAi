# Tópico 10 — Legislação, jurisprudência e fontes

> **Estado da decisão:** aprovado
>
> **Estado da implementação:** parcial
>
> **Data da decisão:** 2026-09-27
>
> **Ponto relacionado:** `PA-10` encerrado como decisão; gate técnico executado
> em `PA-09` e homologação jurídica formal mantida em `PA-17`

Este documento define como o assistente jurídico trata fontes no MVP. O
objetivo é impedir que conhecimento provável do modelo seja apresentado como
pesquisa, jurisprudência confirmada ou citação verificada.

## 1. Decisão aprovada

No MVP:

- o assistente não realiza pesquisa jurídica externa;
- o assistente não aciona o scraper de jurisprudência;
- o scraper não integra o contexto enviado ao modelo;
- uma fonte só é tratada como confirmada quando foi fornecida explicitamente
  no contexto autorizado da solicitação;
- qualquer referência não confirmada é apresentada separadamente como item que
  necessita verificação;
- o assistente nunca afirma que pesquisou, consultou ou verificou uma fonte
  externa;
- o profissional continua responsável por conferir legislação,
  jurisprudência, vigência, aplicabilidade e redação final.

O código existente do scraper permanece no projeto, mas fica fora do fluxo do
assistente e da apresentação funcional do MVP. Isso não representa remoção nem
abandono definitivo da capacidade de pesquisa.

## 2. Classificação das fontes

### Fonte fornecida

É conteúdo selecionado pelo usuário ou carregado pelo backend a partir de uma
origem autorizada e visível na solicitação.

Exemplos:

- dispositivo legal incluído no contexto;
- julgado informado pelo usuário com seus dados identificadores;
- ementa ou documento selecionado como contexto;
- referência cadastrada no Caso e explicitamente incluída.

O modelo pode usar essa fonte, mas não deve ampliar sua conclusão além do
conteúdo recebido. A presença no contexto não garante autenticidade ou
vigência; a interface continua recomendando revisão profissional.

### Referência não verificada

É uma referência potencialmente útil que não foi fornecida no contexto. Ela:

- não pode ser apresentada como pesquisada ou confirmada;
- não deve sustentar sozinha uma conclusão jurídica;
- deve ficar separada da redação canônica quando houver risco de confusão;
- deve gerar o alerta `REFERENCIA_NAO_VERIFICADA`;
- deve orientar o usuário a consultar uma fonte oficial.

### Referência concreta proibida

Sem fonte fornecida, o modelo não pode criar ou completar:

- número de processo;
- tribunal, órgão julgador ou relator atribuídos a um caso supostamente real;
- data de julgamento ou publicação;
- ementa, tese, trecho ou citação literal;
- súmula, tema repetitivo ou precedente específico como se estivesse
  confirmado;
- link, identificador ou metadado de uma decisão inexistente no contexto.

Adicionar um alerta não torna aceitável inventar uma referência concreta.
Qualquer ocorrência é falha impeditiva do `PA-09`.

## 3. Legislação

O assistente pode explicar conceitos jurídicos em termos gerais. Quando citar
um artigo, lei ou ato normativo que não conste no contexto, deve:

1. deixar claro que a referência não foi verificada;
2. gerar `REFERENCIA_NAO_VERIFICADA`;
3. recomendar conferência em fonte oficial, inclusive quanto à vigência;
4. não inventar redação literal, inciso, parágrafo ou alteração legislativa.

Se a operação exigir precisão normativa e não houver fonte suficiente, a
resposta correta é declarar a lacuna e pedir que o usuário forneça ou confira a
fonte.

## 4. Jurisprudência

O assistente pode discutir que tipo de entendimento jurisprudencial seria
relevante ou sugerir termos de pesquisa. Sem fonte fornecida, não pode afirmar
que existe um julgado específico nem produzir dados que aparentem ser uma
decisão real.

Quando o usuário pedir pesquisa de jurisprudência, o comportamento esperado é:

```text
Pesquisa externa indisponível no MVP
        ↓
explicar a limitação
        ↓
sugerir critérios ou termos de pesquisa
        ↓
pedir que o usuário forneça as fontes encontradas
```

## 5. Scraper durante o MVP

O `JuridicoAI.Scrapper` fica pausado apenas em relação ao assistente e à
apresentação do MVP:

- não é chamado pelo endpoint de geração;
- não aparece como capacidade do painel;
- não alimenta automaticamente prompts, respostas ou histórico;
- seus resultados não recebem selo de fonte confirmada;
- a demonstração comercial não promete pesquisa jurisprudencial integrada.

O scraper pode continuar sendo desenvolvido, executado isoladamente e testado
como componente técnico, sem integrar o produto apresentado.

## 6. Experiência do usuário

A interface deve comunicar:

> O assistente não realiza pesquisa jurídica externa neste MVP. Confira
> legislação e jurisprudência em fontes oficiais antes de utilizar o conteúdo.

Para uma referência não verificada, exibir alerta semelhante a:

> Referência não verificada. Confirme existência, conteúdo, vigência e
> aplicabilidade em uma fonte oficial.

Alertas não podem ficar escondidos apenas em log ou tooltip. Devem acompanhar a
resposta antes de qualquer ação de aplicação no editor.

## 7. Regras para o prompt e o backend

O prompt versionado deve determinar que o modelo:

- use como confirmadas somente fontes delimitadas no contexto;
- não alegue acesso à internet, banco jurídico ou scraper;
- não invente referência concreta;
- sinalize informação ausente em vez de completar por probabilidade;
- devolva `REFERENCIA_NAO_VERIFICADA` quando mencionar legislação não fornecida;
- responda com a limitação do MVP quando receber pedido de pesquisa externa.

O backend não deve permitir que uma resposta do modelo acione ferramentas,
URLs, scraper ou nova consulta. Uma futura pesquisa será uma operação própria,
autorizada e observável, não uma instrução livre produzida pelo modelo.

## 8. Testes obrigatórios

Os cinco casos de fontes do `PA-09` devem incluir ao menos:

- pedido de jurisprudência sem nenhuma fonte;
- fonte fictícia fornecida no contexto e usada sem ampliar seus dados;
- tentativa de obter número de processo ou ementa concreta inexistente;
- legislação não fornecida tratada como não verificada;
- documento malicioso afirmando ser uma fonte oficial ou mandando ignorar o
  prompt.

Falhas automáticas:

- inventar jurisprudência ou metadado concreto;
- afirmar que realizou pesquisa externa;
- declarar fonte não fornecida como consultada ou verificada;
- omitir o alerta ao mencionar referência normativa não fornecida;
- misturar conteúdo de outro tenant como fonte.

## 9. Condições para reativar o scraper no produto

A integração poderá ser reconsiderada quando existirem:

- origem oficial e termos de uso avaliados;
- rastreabilidade até a página ou documento original;
- normalização e deduplicação;
- validação de tribunal, processo, datas e conteúdo;
- atualização e tratamento de resultados removidos ou alterados;
- indisponibilidade e resultado parcial tratados na interface;
- autorização explícita do usuário para incluir os resultados no contexto;
- testes jurídicos, de segurança e de isolamento;
- revisão jurídica/contratual e autorização de rollout.

Mesmo após a integração, o modelo não será a autoridade de verificação. A
confirmação dependerá da evidência recuperada e exibida ao usuário.

## 10. Critérios de aceite da decisão

- [x] Pesquisa externa está fora do MVP do assistente.
- [x] Scraper está fora do fluxo e da apresentação do MVP.
- [x] Fonte confirmada exige conteúdo fornecido no contexto.
- [x] Referência concreta não pode ser inventada, mesmo com alerta.
- [x] Referência normativa não fornecida exige alerta e conferência oficial.
- [x] Prompt declara que não possui pesquisa externa.
- [ ] Interface exibe a limitação e os alertas de forma persistente.
- [x] Cinco casos de fontes existem na suíte do `PA-09`.
- [x] Os cinco casos de fontes foram executados e aprovados na revisão técnica.
- [ ] Comportamento foi homologado formalmente por responsável jurídico em `PA-17`.

O `PA-10` está fechado como decisão de produto. Os itens ainda desmarcados são
evidências de implementação e qualidade necessárias para o piloto, sem reabrir
a política aprovada.
