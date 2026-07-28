# Normalização e classificação

## Normalização

`JurisprudenciaNormalizerService` converte um `JurisprudenciaRaw` já persistido
em `Jurisprudencia`.

As transformações são:

- exige `raw.Id`, usado como `rawId`;
- reduz sequências de espaços e remove espaços nas extremidades;
- converte `fonte` para maiúsculas;
- transforma relator e órgão julgador vazios em `null`;
- tenta converter datas para UTC;
- copia e limpa ementa e link;
- define `dataProcessamento` com o horário UTC atual.

Formatos de data aceitos:

```text
dd/MM/yyyy
d/M/yyyy
yyyy-MM-dd
dd-MM-yyyy
d-M-yyyy
```

Uma data vazia ou inválida vira `null`; ela não causa falha no item.

## Classificador por regras

`JurisprudenciaClassifierService` não chama IA. Ele remove acentos, converte a
ementa para minúsculas e procura expressões fixas.

### Categorias detectadas

| Categoria | Exemplos de expressões |
|---|---|
| tema | `negativacao indevida`, `inscricao indevida`, `cadastro de inadimplentes` |
| subtema | `debito inexistente`, `inexistencia do debito`, `contrato inexistente` |
| dano moral | `dano moral`, `danos morais`, `indenizacao por dano moral` |
| exclusão | `exclusao`, `baixa da negativacao`, `retirada do nome` |
| favorável forte | `indenizacao mantida`, `dano moral configurado`, `debito inexistente` |
| desfavorável | `dano moral nao configurado`, `negativacao legitima`, `improcedente` |

A lista completa e autoritativa está em
`JurisprudenciaClassifierService.cs`.

### Resultado

- somente sinais favoráveis:
  `provavelmente_favoravel_consumidor`;
- somente sinais desfavoráveis:
  `provavelmente_desfavoravel_consumidor`;
- nenhum sinal ou sinais conflitantes: `indefinido`.

Há duas regras adicionais:

- `sentenca mantida` conta como favorável se já houver sinal favorável ou dano
  moral;
- `recurso improvido` conta como favorável quando a ementa contém
  `indenizacao`.

Essas regras são heurísticas: o sentido de uma decisão pode depender de quem
recorreu e do contexto. O resultado não deve ser tratado como parecer jurídico.

## Campos derivados

| Campo | Regra |
|---|---|
| `area` | sempre `direito_consumidor` |
| `tema` | `negativacao_indevida` ou `indefinido` |
| `subtema` | `debito_inexistente` ou `indefinido` |
| `tipoAcao` | `declaratoria_inexistencia_debito_indenizacao` quando há subtema e dano moral |
| `danoMoral` | existe expressão da categoria |
| `exclusaoNegativacao` | existe expressão da categoria |
| `valorDanoMoral` | não é preenchido atualmente |
| `palavrasChave` | expressões encontradas, sem duplicatas |
| `statusClassificacao` | `classificado` |
| `versaoClassificador` | `regras-v1` |

## Confiança

A pontuação soma:

| Evidência | Peso |
|---|---:|
| tema detectado | 0,25 |
| subtema detectado | 0,20 |
| dano moral detectado | 0,15 |
| exclusão detectada | 0,10 |
| resultado não indefinido | 0,20 |
| processo, link e data de julgamento preenchidos | 0,10 |

O valor é limitado a 1 e arredondado para duas casas. Se houver sinais
favoráveis e desfavoráveis ao mesmo tempo, o teto passa a ser 0,45.
