# Integração técnica com o TJSP

## Fluxo completo

```text
1. GET consultaCompleta.do em Chromium headless
2. aguardar #uuidCaptcha e grecaptcha.execute
3. obter uuidCaptcha, token reCAPTCHA e cookies
4. fechar o contexto do navegador
5. criar HttpClient com o CookieContainer
6. POST captchaControleAcesso.do
7. POST resultadoCompleta.do
8. analisar a primeira página
9. GET trocaDePagina.do para páginas adicionais
10. compor TjspSearchResult
```

## Endpoints

Base:

```text
https://esaj.tjsp.jus.br
```

| Método | Endpoint | Uso |
|---|---|---|
| `GET` | `/cjsg/consultaCompleta.do` | cria a sessão no navegador |
| `POST` | `/cjsg/captchaControleAcesso.do` | tenta atualizar `uuidCaptcha` |
| `POST` | `/cjsg/resultadoCompleta.do` | envia a busca e recebe a página 1 |
| `GET` | `/cjsg/trocaDePagina.do` | recebe páginas 2 em diante |
| `GET` | `/cjsg/getArquivo.do` | link montado para o PDF/inteiro teor |

## Sessão de navegador

`TjspBrowserSessionProvider` inicia Chromium headless com locale `pt-BR`,
user-agent fixo e timeout de 30 segundos para inicialização e navegação.

Ele procura primeiro um navegador instalado em:

```text
/usr/bin/chromium
/usr/bin/chromium-browser
/snap/bin/chromium
/usr/bin/google-chrome
/usr/bin/google-chrome-stable
```

Se nenhum caminho existir, Playwright tenta usar o executável de sua própria
instalação.

Na página de consulta, o provider:

- aguarda o seletor `#uuidCaptcha`;
- aguarda `window.grecaptcha.execute`;
- executa reCAPTCHA com ação `consulta`;
- captura os cookies associados à base do TJSP.

A site key do reCAPTCHA está codificada na classe. Mudanças feitas pelo TJSP
podem exigir atualização do código.

## Cliente HTTP

O `TjspJurisprudenciaClient` reutiliza os cookies do navegador, aceita
descompressão gzip, deflate e Brotli, e envia cabeçalhos de navegador,
`Accept-Language`, `Origin` e `Referer`.

O controle de captcha é tolerante a falhas:

- status HTTP não bem-sucedido mantém o UUID anterior;
- corpo que não seja JSON mantém o UUID anterior;
- JSON sem `uuidCaptcha` mantém o UUID anterior.

A busca principal e a paginação, por outro lado, lançam
`InvalidOperationException` para status HTTP não bem-sucedido.

## Formulário de pesquisa

O payload usa `dados.buscaInteiroTeor` para o termo, pesquisa todas as origens
(`dados.origensSelecionadas=T`) e ordena por data de publicação
(`dados.ordenarPor=dtPublicacao`).

Campos de classe, assunto, comarca, relator e intervalos de data são enviados
vazios. O formulário inclui o token do reCAPTCHA, o UUID e o tipo de decisão.
O campo `dados.pesquisarComSinonimos` aparece duas vezes, reproduzindo o
comportamento implementado.

## Parser HTML

O parser:

- separa respostas concatenadas pelo marcador
  `#%#quebra_resposta#%#`;
- lê o total em `totalResultadoAba-*`;
- separa itens por `<tr class="fundocinza1">`;
- identifica processo e códigos em links `downloadEmenta`;
- lê pares `<strong>Campo:</strong> Valor`;
- prefere a ementa sem formatação e usa a formatada como fallback;
- remove tags, decodifica entidades HTML e normaliza espaços;
- monta o link `getArquivo.do` com `cdAcordao` e `cdForo`.

Um item só é incluído quando possui número do processo ou ementa.

## Pontos frágeis

A integração depende de detalhes não contratuais do portal:

- seletores e nomes de campos;
- site key e ação do reCAPTCHA;
- cookies e regras antiautomação;
- classes e ids HTML;
- marcador de resposta e tamanho da página;
- disponibilidade e política de uso do TJSP.

Mudanças no portal podem produzir exceções ou, de forma mais sutil, zero itens
ou campos vazios. Ao investigar, compare o HTML recente com os seletores e
expressões regulares antes de concluir que não há resultados.

O scraper não implementa retentativas, backoff, limitação de taxa, proxy,
persistência de sessão ou detecção estruturada de página de bloqueio.
