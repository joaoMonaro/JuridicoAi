# Fluxo do Scraper TJSP

Este documento explica, de forma simples, o que o projeto `JuridicoAI.Scrapper` faz para buscar jurisprudencias no site do TJSP.

## Objetivo

O scraper busca jurisprudencias na Consulta de Jurisprudencia do Segundo Grau do TJSP.

Ele recebe um termo de pesquisa, por exemplo:

```bash
uso indevido
```

E retorna um JSON com dados como:

- numero do processo;
- ementa;
- relator;
- comarca;
- orgao julgador;
- data de julgamento;
- data de publicacao;
- codigo do acordao;
- link do PDF/inteiro teor.

## Por Que Nao Basta Um Curl Simples

O TJSP nao aceita apenas um `POST` estatico para retornar os resultados.

A consulta depende de tres coisas dinamicas:

- cookies da sessao;
- `uuidCaptcha`;
- `recaptcha_response_token`.

Esses valores sao gerados quando a pagina real do TJSP e aberta no navegador.

Por isso o scraper usa duas etapas:

1. Um navegador automatizado para obter sessao e captcha.
2. Requests HTTP normais para buscar e paginar os resultados.

## Fluxo Geral

```text
1. Abrir a pagina do TJSP com Playwright
2. Ler o uuidCaptcha da pagina
3. Executar o reCAPTCHA v3 no navegador
4. Capturar cookies da sessao
5. Criar um HttpClient com esses cookies
6. Atualizar o uuidCaptcha no endpoint do TJSP
7. Fazer POST para resultadoCompleta.do
8. Receber HTML com a primeira pagina de resultados
9. Extrair os dados do HTML
10. Se precisar, buscar paginas seguintes em trocaDePagina.do
11. Gerar JSON final
```

## Endpoints Usados

### 1. Tela de Consulta

```text
GET https://esaj.tjsp.jus.br/cjsg/consultaCompleta.do
```

Usado pelo Playwright para abrir a pagina real do TJSP.

Dessa pagina o scraper extrai:

- `uuidCaptcha`;
- cookies;
- token do reCAPTCHA.

### 2. Controle de Captcha

```text
POST https://esaj.tjsp.jus.br/cjsg/captchaControleAcesso.do
```

Payload:

```text
uuidCaptcha=sajcaptcha_...
conversationId=
```

Esse endpoint pode devolver um novo `uuidCaptcha`.

Se vier um novo valor, o scraper usa esse valor atualizado no submit da busca.

### 3. Busca Principal

```text
POST https://esaj.tjsp.jus.br/cjsg/resultadoCompleta.do
```

Esse endpoint retorna a primeira pagina de resultados em HTML.

Quando da certo, o HTML contem:

```html
<div id="divDadosResultado-A">
```

E cada resultado aparece como:

```html
<tr class="fundocinza1">
```

### 4. Paginacao

```text
GET https://esaj.tjsp.jus.br/cjsg/trocaDePagina.do?tipoDeDecisao=A&pagina=2
```

Usado para buscar a pagina 2, 3, 4 etc.

A primeira pagina vem de `resultadoCompleta.do`.
As proximas paginas vem de `trocaDePagina.do`.

## O Que Cada Classe Faz

## `Program.cs`

E a entrada do executavel.

Responsabilidades:

- ler argumentos de linha de comando;
- validar entrada basica;
- chamar o scraper;
- imprimir o JSON no terminal ou salvar em arquivo;
- tratar erro de forma clara.

Exemplo:

```bash
dotnet run --project src/JuridicoAI.Scrapper/JuridicoAI.Scrapper.csproj -- \
  --termo "uso indevido" \
  --max 20 \
  --output resultado.json
```

## `TjspBrowserSessionProvider`

Abre o navegador com Playwright.

Responsabilidades:

- abrir `consultaCompleta.do`;
- esperar a pagina carregar;
- ler `#uuidCaptcha`;
- executar `grecaptcha.execute(...)`;
- capturar cookies;
- devolver uma sessao pronta para o `HttpClient`.

Ele nao faz scraping dos resultados.

Ele so prepara a sessao.

## `TjspJurisprudenciaClient`

Faz os requests HTTP para o TJSP.

Responsabilidades:

- usar os cookies capturados pelo Playwright;
- chamar `captchaControleAcesso.do`;
- chamar `resultadoCompleta.do`;
- chamar `trocaDePagina.do`;
- retornar HTML bruto.

Ele nao interpreta o HTML.

## `TjspPayloadBuilder`

Monta o formulario enviado para o TJSP.

Responsabilidades:

- criar os pares `chave=valor` do POST;
- preencher termo de busca;
- preencher token do reCAPTCHA;
- preencher `uuidCaptcha`;
- informar tipo de decisao.

O tipo padrao e:

```text
A = Acórdãos
```

Outros tipos aceitos:

```text
H = Homologações de acordo
D = Decisões monocráticas
```

## `TjspResultadoParser`

Extrai dados do HTML.

Responsabilidades:

- localizar o total de resultados;
- localizar cada item de jurisprudencia;
- extrair numero do processo;
- extrair `cdAcordao`;
- extrair `cdForo`;
- montar link do PDF;
- extrair classe/assunto;
- extrair relator;
- extrair comarca;
- extrair orgao julgador;
- extrair datas;
- extrair ementa.

Ele recebe HTML e retorna objetos C#.

## `TjspScraperService`

Orquestra o processo inteiro.

Responsabilidades:

- pedir uma sessao ao `TjspBrowserSessionProvider`;
- criar o client HTTP;
- atualizar captcha;
- fazer a primeira busca;
- chamar o parser;
- buscar paginas seguintes se necessario;
- devolver o resultado final.

## Modelos

### `TjspSearchRequest`

Representa a entrada da busca.

Campos principais:

- `Termo`;
- `QuantidadeMaxima`;
- `TipoDecisao`;
- `IncluirSinonimos`;
- `OutputPath`.

### `TjspSearchResult`

Representa o resultado final da busca.

Campos principais:

- `Termo`;
- `TotalEncontrado`;
- `QuantidadeRetornada`;
- `Resultados`.

### `JurisprudenciaResultado`

Representa uma jurisprudencia individual.

Campos principais:

- `Tribunal`;
- `NumeroProcesso`;
- `Ementa`;
- `LinkPdf`;
- `CodigoAcordao`;
- `CodigoForo`;
- `ClasseAssunto`;
- `Relator`;
- `Comarca`;
- `OrgaoJulgador`;
- `DataJulgamento`;
- `DataPublicacao`;
- `Ocorrencias`.

## Exemplo de Uso

Buscar 1 resultado:

```bash
dotnet run --project src/JuridicoAI.Scrapper/JuridicoAI.Scrapper.csproj -- \
  --termo "uso indevido" \
  --max 1 \
  --output resultado.json
```

Buscar 40 resultados:

```bash
dotnet run --project src/JuridicoAI.Scrapper/JuridicoAI.Scrapper.csproj -- \
  --termo "negativação indevida dano moral" \
  --max 40 \
  --output resultado.json
```

Sem pesquisa por sinonimos:

```bash
dotnet run --project src/JuridicoAI.Scrapper/JuridicoAI.Scrapper.csproj -- \
  --termo "uso indevido" \
  --sem-sinonimos \
  --output resultado.json
```

Incluir HTML bruto da primeira pagina no JSON:

```bash
dotnet run --project src/JuridicoAI.Scrapper/JuridicoAI.Scrapper.csproj -- \
  --termo "uso indevido" \
  --raw-html \
  --output resultado.json
```

## Saida Esperada

O JSON tem este formato geral:

```json
{
  "Termo": "uso indevido",
  "TotalEncontrado": 573171,
  "QuantidadeRetornada": 1,
  "Resultados": [
    {
      "Tribunal": "TJSP",
      "NumeroProcesso": "1500765-74.2023.8.26.0038",
      "Ementa": "...",
      "LinkPdf": "https://esaj.tjsp.jus.br/cjsg/getArquivo.do?cdAcordao=20612605&cdForo=0",
      "CodigoAcordao": "20612605",
      "CodigoForo": "0",
      "ClasseAssunto": "Apelação Criminal / ...",
      "Relator": "Nogueira Nascimento",
      "Comarca": "Araras",
      "OrgaoJulgador": "12ª Câmara de Direito Criminal",
      "DataJulgamento": "11/06/2026",
      "DataPublicacao": "11/06/2026",
      "Ocorrencias": "(4 ocorrências encontradas no inteiro teor do documento)"
    }
  ]
}
```

## Como Saber Se Deu Certo

Durante a execucao, o terminal mostra:

```text
Consultando TJSP. Termo: 'uso indevido'. Max: 1.
Resultado salvo em: resultado.json
Total informado pelo TJSP: 573171. Itens retornados: 1.
```

Se `Itens retornados` for maior que zero, o fluxo funcionou.

## Falhas Comuns

### Browser nao encontrado

O scraper precisa de Chromium ou Google Chrome.

Ele procura nestes caminhos:

```text
/usr/bin/chromium
/usr/bin/chromium-browser
/snap/bin/chromium
/usr/bin/google-chrome
/usr/bin/google-chrome-stable
```

Se nenhum existir, instale os browsers do Playwright:

```bash
pwsh src/JuridicoAI.Scrapper/bin/Debug/net10.0/playwright.ps1 install chromium
```

Ou instale Google Chrome/Chromium no sistema.

### Resultado volta vazio

Possiveis causas:

- token do reCAPTCHA expirou;
- sessao foi invalidada;
- TJSP alterou protecao;
- TJSP retornou apenas o formulario;
- termo pesquisado nao trouxe resultado.

O scraper gera uma sessao nova a cada execucao para reduzir esse problema.

### Erro em paginas seguintes

A pagina 1 usa `resultadoCompleta.do`.

As paginas seguintes dependem da mesma sessao e usam `trocaDePagina.do`.

Se a sessao cair, a paginacao pode falhar.

## Limites Atuais

- Limite interno de resultados: 200 por execucao.
- Nao baixa o PDF automaticamente; apenas monta o `LinkPdf`.
- Nao persiste em banco.
- Nao tem retry automatico.
- Nao tem testes automatizados ainda.

## Melhorias Futuras

- Criar testes unitarios para `TjspResultadoParser` usando o HTML salvo em `contexto/har_entry0_resultadoCompleta.html`.
- Adicionar retry controlado quando o TJSP retornar formulario vazio.
- Adicionar opcao para baixar PDFs.
- Adicionar persistencia em MongoDB ou arquivo incremental.
- Adicionar logs estruturados.
- Adicionar configuracao por `appsettings.json` se o scraper crescer.

## Resumo Curto

O scraper funciona assim:

```text
Playwright pega sessao + captcha.
HttpClient faz os requests reais.
Parser extrai os dados do HTML.
Program.cs salva o JSON.
```
