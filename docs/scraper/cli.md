# CLI do scraper

## Comando básico

```bash
dotnet run --project src/JuridicoAI.Scrapper/JuridicoAI.Scrapper.csproj -- \
  --termo "uso indevido" \
  --max 20 \
  --output resultado.json
```

O separador `--` entrega ao executável os argumentos que aparecem depois dele.

## Opções

| Opção | Atalho | Obrigatória | Padrão | Função |
|---|---|---|---|---|
| `--termo` | `-t` | sim | — | termo de pesquisa livre |
| `--max` | `-m` | não | `20` | quantidade solicitada, maior que zero |
| `--tipo-decisao` | — | não | `A` | `A`, `H` ou `D` |
| `--sem-sinonimos` | — | não | desligado | desativa sinônimos |
| `--raw-html` | — | não | desligado | inclui a resposta completa da primeira página |
| `--output` | `-o` | não | — | salva o JSON no caminho informado |
| `--help` | `-h` | não | — | exibe ajuda |

O teto interno é 200 resultados. `--max` valida apenas se o número é positivo;
valores acima de 200 são reduzidos pelo serviço.

## Exemplos

Imprimir JSON no terminal:

```bash
dotnet run --project src/JuridicoAI.Scrapper -- \
  -t "dívida inexistente" -m 5
```

Buscar decisões monocráticas sem sinônimos:

```bash
dotnet run --project src/JuridicoAI.Scrapper -- \
  --termo "negativação indevida" \
  --tipo-decisao D \
  --sem-sinonimos \
  --output resultado.json
```

Guardar também a primeira página HTML:

```bash
dotnet run --project src/JuridicoAI.Scrapper -- \
  --termo "dano moral" \
  --raw-html \
  --output resultado-com-html.json
```

Mesmo sem `--raw-html`, cada item possui `ConteudoBruto` com seu trecho HTML. A
opção acrescenta `HtmlPrimeiraPagina` ao nível superior do JSON.

## Saídas padrão

- o JSON vai para `stdout` quando `--output` é omitido;
- mensagens de progresso e resumo vão para `stderr`;
- com `--output`, o diretório é criado automaticamente e o JSON é salvo em
  UTF-8.

Isso permite redirecionar somente o JSON:

```bash
dotnet run --project src/JuridicoAI.Scrapper -- \
  --termo "dano moral" --max 5 > resultado.json
```

## Códigos de saída

| Código | Significado |
|---:|---|
| `0` | execução ou exibição de ajuda concluída |
| `1` | argumentos inválidos ou falha de execução |
| `130` | cancelamento por `Ctrl+C` |

Argumentos desconhecidos, valores ausentes, tipo fora de `A/H/D` e `--max`
inválido produzem mensagem em `stderr`, exibem a ajuda e encerram com código 1.

## Caminho de saída

O caminho de `--output` é convertido para absoluto em relação ao diretório
atual. Se o arquivo já existir, `File.WriteAllTextAsync` substitui seu
conteúdo.
