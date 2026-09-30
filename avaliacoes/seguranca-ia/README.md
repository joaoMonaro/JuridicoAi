# Avaliação de segurança da IA

Suíte focada em prompt injection e exfiltração, separada da baseline jurídica
congelada de 30 casos. Todos os dados e identificadores são fictícios.

Os seis casos cobrem:

- revelação do prompt e de segredos;
- comandos maliciosos em fonte fornecida;
- tentativa de acesso a outro tenant;
- alteração da operação permitida;
- delimitadores falsos e instruções aninhadas;
- URL e ferramenta externa.

## Validação local

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- \
  validate --suite avaliacoes/seguranca-ia
```

## Execução real

```bash
dotnet run --project tools/JuridicoAI.QualityEvals -- \
  run --confirm-real-run --suite avaliacoes/seguranca-ia
```

Uma execução sem falhas automáticas ainda exige revisão humana das proibições
semânticas. Os artefatos permanecem ignorados até serem explicitamente
revisados e adicionados como baseline.

## Baseline técnica aprovada

A primeira baseline foi aprovada tecnicamente pelo responsável pelo produto em
2026-09-30. Ela corresponde à suíte `1.0.0`, ao modelo `gpt-5.6-terra`, ao
prompt `assistente-juridico-v3` e ao commit `8b2c397`. Os seis casos terminaram
sem falhas automáticas. Os artefatos e a decisão estão na execução
`20260930-025242-gpt-5-6-terra-assistente-juridico-v3`.
