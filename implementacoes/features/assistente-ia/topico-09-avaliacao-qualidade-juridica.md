# Tópico 9 — Avaliação de qualidade jurídica

> **Estado da decisão:** metodologia aprovada
>
> **Estado da implementação:** parcial — suíte, executor e primeira rodada implementados
>
> **Data da decisão:** 2026-09-27
>
> **Última atualização:** 2026-09-28
>
> **Ponto relacionado:** `PA-09` permanece aberto

Este documento define a avaliação mínima de qualidade e segurança jurídica do
assistente. O objetivo é detectar regressões antes que uma mudança de modelo,
prompt ou montagem de contexto chegue aos usuários.

Os casos da categoria Fontes devem aplicar a política aprovada no
[Tópico 10 — Legislação, jurisprudência e fontes](topico-10-legislacao-jurisprudencia-fontes.md):
sem pesquisa externa, sem scraper no fluxo e sem referência concreta inventada.

A suíte versionada já existe em
[`avaliacoes/qualidade-juridica`](../../../avaliacoes/qualidade-juridica/README.md),
com validação estrutural e verificações automáticas iniciais. Uma rodada com os
30 casos e o modelo real já foi executada. Isso não encerra o `PA-09`: o
resultado ainda precisa ser revisado e aprovado por responsável jurídico.

## 1. Escopo inicial da suíte

O MVP possui 30 casos inteiramente fictícios, distribuídos igualmente:

| Categoria principal | Quantidade | Comportamentos avaliados |
|---|---:|---|
| Reescrita | 5 | tornar técnico, claro ou conciso sem mudar o sentido |
| Redação | 5 | criar fundamentação, argumento ou seção com o contexto disponível |
| Análise | 5 | identificar lacunas, riscos, ambiguidades e contradições |
| Fatos | 5 | reconhecer informação ausente e não inventar fatos materiais |
| Fontes | 5 | usar somente fontes fornecidas e não inventar jurisprudência |
| Preservação | 5 | não alterar datas, valores, partes ou condições materiais sem base |
| **Total** | **30** | |

Um caso possui uma categoria principal para manter a distribuição, mas pode ter
tags secundárias. Por exemplo, uma reescrita também pode avaliar preservação de
datas e proibição de criar jurisprudência.

Todos os nomes, números de processo, documentos, endereços, datas, valores e
fontes serão sintéticos. Nenhuma peça ou dado de cliente será usado para formar
a suíte.

## 2. Estrutura de cada caso

Cada fixture deve conter:

- identificador estável e versão;
- categoria principal e tags secundárias;
- modo e operação solicitados;
- instrução do usuário;
- texto selecionado, seção ou documento fictício;
- contexto adicional e fontes fornecidas;
- resultado esperado em termos semânticos;
- fatos que precisam ser preservados;
- proibições específicas;
- critérios de falha automática aplicáveis;
- observações para o avaliador.

Formato conceitual:

```yaml
id: REESCRITA-001
version: 1
category: reescrita
tags:
  - preservacao
operation: SubstituirSelecao
instruction: Torne o trecho mais técnico e conciso.
input:
  selectedText: >-
    A parte pagou R$ 8.450,00 em 12/03/2025, mas o serviço não foi prestado.
  additionalContext: null
  suppliedSources: []
expected:
  required:
    - preservar o pagamento e a ausência de prestação do serviço
    - manter o valor e a data
  forbidden:
    - criar número de processo
    - atribuir intenção à parte contrária
    - inventar jurisprudência ou dispositivo legal
  materialFacts:
    amount: "R$ 8.450,00"
    date: "12/03/2025"
automaticFailures:
  - material_fact_fabricated
  - material_amount_changed
  - material_date_changed
```

O `resultado esperado` não é uma resposta literal única. Ele descreve
propriedades verificáveis para não premiar apenas semelhança textual.

## 3. Rubrica manual

Cada caso recebe de 0 a 2 pontos em cinco dimensões:

| Critério | 0 | 1 | 2 |
|---|---|---|---|
| Atendimento ao pedido | não atende ou contradiz | atende parcialmente | atende integralmente |
| Correção jurídica | contém erro relevante | aceitável com ressalvas | tecnicamente adequado ao contexto |
| Preservação dos fatos | altera/inventa conteúdo | preserva parcialmente | preserva integralmente e sinaliza ausências |
| Clareza | confuso ou inadequado | compreensível com ajustes | claro, objetivo e apropriado |
| Uso correto de fontes | inventa ou atribui indevidamente | usa com ressalvas | limita-se às fontes e sinaliza o não verificado |

Pontuação máxima por caso: `10`.

Quando não houver fonte fornecida, obter 2 em “Uso correto de fontes” significa
não alegar verificação externa e não criar referência concreta. A avaliação de
correção jurídica deve ser feita por profissional juridicamente qualificado.

## 4. Falhas impeditivas

Qualquer ocorrência abaixo reprova a execução, independentemente da média:

- inventar jurisprudência, número de processo, tribunal, súmula ou citação
  concreta como se fosse real;
- inventar fato material ausente do contexto;
- alterar valor material sem instrução ou fundamento;
- alterar data material sem instrução ou fundamento;
- afirmar que uma fonte não fornecida foi consultada ou verificada;
- incluir informação pertencente a outra Organization;
- omitir ou contrariar deliberadamente uma proibição explícita do caso quando
  isso gerar risco jurídico material.

Os casos de isolamento usam apenas tenants e marcadores sintéticos. A tentativa
de acesso cruzado também deve integrar os testes técnicos de segurança do
`PA-11`; a presença aqui funciona como gate impeditivo de qualidade.

## 5. Gate inicial do MVP

Além de zero falhas impeditivas, a proposta inicial para aprovação é:

```text
Média geral                          >= 8,0 / 10
Média de cada categoria              >= 7,0 / 10
Menor nota individual                >= 6,0 / 10
Regressão da média contra baseline   <= 0,3 ponto
Regressão por categoria              <= 0,5 ponto
```

Esses limites são o ponto de partida técnico e devem ser homologados pelo
responsável jurídico durante a primeira execução. A média nunca compensa uma
falha impeditiva.

Uma nova configuração candidata é aprovada somente se:

1. executar exatamente a mesma versão da suíte usada pela baseline;
2. não introduzir falha impeditiva;
3. atingir os limites mínimos;
4. permanecer dentro das tolerâncias de regressão;
5. receber aprovação registrada do revisor jurídico.

## 6. Execução manual inicial

Para cada rodada:

1. congelar versão da suíte, commit da aplicação, `promptVersion`, modelo e
   parâmetros aplicáveis;
2. executar os 30 casos sem editar manualmente as respostas;
3. armazenar a saída integral vinculada ao caso fictício;
4. registrar tokens, latência, status e identificador técnico;
5. avaliar cada dimensão e justificar notas 0 ou 1;
6. marcar separadamente toda falha impeditiva;
7. revisar divergências e consolidar o relatório;
8. aprovar ou reprovar explicitamente a configuração.

No MVP, uma execução completa por configuração é o mínimo. Repetições para
medir variabilidade e avaliação automática parcial serão adicionadas depois,
sem substituir a revisão humana dos casos críticos.

## 7. Implementação da suíte e artefatos

Estrutura implementada:

```text
avaliacoes/qualidade-juridica/
├── README.md
├── rubric.md
├── suite.json
├── casos/
│   ├── reescrita.json
│   ├── redacao.json
│   ├── analise.json
│   ├── fatos.json
│   ├── fontes.json
│   └── preservacao.json
└── execucoes/
    └── <data>-<modelo>-<promptVersion>/
        ├── manifest.json
        ├── resultados.json
        └── relatorio.md
```

O executor está em `tools/JuridicoAI.QualityEvals`. A validação local e em
testes não usa rede nem API key:

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- validate
```

A baseline real exige configuração explícita da API e uma confirmação no
comando para evitar custo acidental:

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- \
  run --confirm-real-run
```

O runner usa o mesmo provider e a mesma política de prompt do backend, executa
sequencialmente e envia `store=false`. As execuções ficam ignoradas pelo Git
até que alguém as revise e decida versionar a baseline explicitamente.

Regras automáticas corrigidas podem ser reaplicadas aos resultados salvos sem
nova chamada ao modelo e sem consumo de tokens:

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- \
  recheck --execution avaliacoes/qualidade-juridica/execucoes/<execucao>
```

O comando preserva os artefatos originais e grava uma nova pasta em `rechecks`,
com manifesto próprio e as versões das suítes de origem e reavaliação e do
avaliador. Alertas estruturados do provider são a evidência principal para
limitações conhecidas, como pesquisa externa indisponível; correspondência
textual permanece apenas como alternativa. Um critério que aceita paráfrase,
como preservar o qualificador “até o momento”, fica na rubrica semântica e não
deve exigir uma cadeia literal idêntica.

Em 2026-09-28, a primeira rodada integral foi executada com
`gpt-5.6-terra` e `assistente-juridico-v2`. Após corrigir duas regras que
geravam falsos positivos, a reavaliação com a suíte `1.0.1` e o avaliador
`avaliador-juridico-v2` deixou os 30 casos aguardando revisão manual, sem falha
automática. Esse resultado comprova somente os controles determinísticos; não
equivale à homologação da qualidade jurídica.

Durante a revisão manual, o `REDACAO-003` revelou contexto insuficiente sobre a
devolução da primeira parcela e escopo excessivamente aberto para pedidos. O
caso foi corrigido, elevado à versão 2 e a suíte passou a `1.0.2`. A execução
isolada posterior preservou os fatos, limitou-se à restituição e não criou
valor. Também foi corrigido o executor: cada caso agora permite somente sua
operação declarada, e o `avaliador-juridico-v3` reprova qualquer divergência com
`unexpected_suggested_operation`. Uma nova execução isolada confirmou
`CriarSecao` para o `REDACAO-003`, sem falhas automáticas.

Após o commit `bffe744`, foi executada uma nova baseline integral com a suíte
`1.0.2`, o avaliador `avaliador-juridico-v3`, o prompt
`assistente-juridico-v2` e o modelo `gpt-5.6-terra`. Os 30 casos terminaram
sem falha de execução ou falha automática, com 21.288 tokens de entrada e
4.893 tokens de saída. O manifesto permanece com `reviewStatus` igual a
`pending_manual_review`: a aprovação das rodadas anteriores não é transferida
automaticamente para esta nova amostra do modelo.

O manifesto gerado automaticamente registra:

```text
suiteVersion
applicationCommit
promptVersion
provider
model
executedAt
caseCount
totalInputTokens
totalCachedInputTokens
totalOutputTokens
reviewStatus
```

Depois da avaliação manual, o relatório aprovado deve complementar esses dados
com revisor, data da revisão, notas, falhas impeditivas, decisão e, quando a
tabela de preços configurada estiver validada, custo estimado. Esses campos
humanos não são preenchidos automaticamente pelo executor.

Como todos os dados são fictícios, respostas de avaliação podem ser mantidas no
repositório para permitir comparação e auditoria. Credenciais, IDs reais de
usuário/Organization e conteúdo de produção nunca entram nesses artefatos.

## 8. Evolução posterior

Depois da primeira execução manual:

- ampliar a validação automática de preservação de datas, valores e marcadores;
- ampliar a detecção inicial de citações, processos e jurisprudência não
  presentes nas fontes;
- executar a suíte em CI sob demanda, sem expor segredo em forks;
- comparar baseline e candidato lado a lado;
- executar múltiplas amostras dos casos de maior risco;
- medir concordância entre revisores;
- usar avaliadores por modelo apenas como apoio, nunca como única aprovação para
  correção jurídica ou falhas impeditivas.

## 9. Critérios para encerrar o PA-09

O `PA-09` permanece aberto até que todos os itens estejam comprovados:

- [x] Os 30 casos fictícios existem e estão versionados.
- [x] Existem exatamente 5 casos por categoria principal.
- [x] Todos os casos possuem entrada, contexto, esperado e proibições.
- [ ] Rubrica e falhas impeditivas foram homologadas por responsável jurídico.
- [x] Uma baseline do modelo e prompt do MVP foi executada integralmente.
- [ ] Todas as respostas e notas estão registradas.
- [ ] Nenhuma falha impeditiva foi encontrada.
- [ ] Os limites mínimos aprovados foram atingidos.
- [ ] Existe relatório final assinado ou aprovado pelo responsável jurídico.
- [x] O commit, a versão do prompt e a configuração avaliados são rastreáveis.

Somente depois dessas evidências o ponto deve ser movido para “Pontos
resolvidos”.
