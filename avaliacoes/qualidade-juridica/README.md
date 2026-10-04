# Avaliação de qualidade jurídica

Esta suíte contém 30 casos inteiramente fictícios para detectar regressões de
qualidade e segurança antes de alterar modelo, prompt ou montagem de contexto.
Ela não usa peças, clientes, processos ou identificadores reais.

## Estrutura

```text
qualidade-juridica/
├── suite.json
├── rubric.md
├── casos/
│   ├── reescrita.json
│   ├── redacao.json
│   ├── analise.json
│   ├── fatos.json
│   ├── fontes.json
│   └── preservacao.json
└── execucoes/
    └── .gitkeep
```

Cada arquivo possui cinco casos. `expected.required` e `expected.forbidden`
são critérios semânticos para revisão humana. `materialFacts`, alertas
obrigatórios, recusa de pesquisa externa e `forbiddenPhrases` permitem algumas
verificações determinísticas, mas não substituem avaliação jurídica. A busca de
fatos materiais considera texto, resumo, alertas estruturados e achados.

## Validar sem API key

Na raiz do repositório:

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- validate
```

Esse comando não acessa a rede. Ele confirma quantidade, distribuição, IDs,
campos obrigatórios e compatibilidade entre modo e operação.

Os mesmos controles são executados pela suíte de testes do backend.

## Executar com o modelo real

Configure `AssistenteIa:ApiKey` e `AssistenteIa:Model` com User Secrets do
projeto da API. O avaliador compartilha o mesmo `UserSecretsId`:

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- run --confirm-real-run
```

Para executar somente um caso durante o desenvolvimento:

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- \
  run --confirm-real-run --case FONTES-001
```

`--confirm-real-run` é obrigatório para impedir chamadas e custos acidentais.
O runner é sequencial e usa `store=false` por meio do provider existente. Cada
caso envia somente sua operação declarada como permitida. O avaliador também
reprova a resposta com `unexpected_suggested_operation` se o provider devolver
uma operação diferente.

## Reavaliar resultados salvos sem usar a API

Quando uma regra determinística do avaliador for corrigida, os resultados já
salvos podem ser processados novamente sem repetir as chamadas ao modelo:

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- \
  recheck --execution avaliacoes/qualidade-juridica/execucoes/<execucao>
```

O comando não carrega API key e não chama o provedor. Os arquivos originais
permanecem intactos, e a nova avaliação é criada em:

```text
<execucao>/rechecks/<data>-<versaoDoAvaliador>/
├── manifest.json
├── resultados.json
└── relatorio.md
```

O manifesto da reavaliação referencia a execução de origem e registra as
versões da suíte original, da suíte atual e do avaliador utilizado. A revisão
jurídica manual continua obrigatória.

Cada execução cria:

```text
execucoes/<data>-<modelo>-<promptVersion>/
├── manifest.json
├── resultados.json
└── relatorio.md
```

As execuções são ignoradas pelo Git por padrão. Depois da revisão, uma baseline
aprovada pode ser adicionada explicitamente com `git add -f`, pois todas as
entradas são fictícias. Isso evita versionar por engano uma resposta que tenha
introduzido dado externo. Chaves e variáveis de ambiente nunca são gravadas.

O comando `validate` e os testes automatizados comprovam a estrutura da suíte,
mas não avaliam sozinhos a qualidade jurídica das respostas. O `PA-09` foi
encerrado tecnicamente após execução integral, revisão das seis categorias,
correção dos bloqueios encontrados e aceitação da configuração congelada pelo
responsável pelo produto. A homologação jurídica formal continua em `PA-17`.

## Aprovação

A automação detecta somente sinais objetivos. A revisão técnica deve preencher
a rubrica, registrar falhas impeditivas e aprovar ou reprovar cada nova
configuração. Antes da comercialização, um profissional juridicamente
qualificado ainda deve homologar os critérios e a qualidade conforme `PA-17`.
Consulte [rubric.md](rubric.md) e o
[Tópico 9](../../implementacoes/features/assistente-ia/decisoes/pa-09-avaliacao-qualidade-juridica.md).
