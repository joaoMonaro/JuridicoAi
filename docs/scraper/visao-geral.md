# Scraper de jurisprudências do TJSP

## Objetivo

`JuridicoAI.Scrapper` pesquisa jurisprudências do Segundo Grau no portal
e-SAJ/TJSP. Ele pode ser executado como CLI ou referenciado como biblioteca pela
API.

O scraper retorna:

- total informado pelo TJSP;
- quantidade efetivamente coletada;
- número do processo e ementa;
- códigos do acórdão e foro;
- link do PDF/inteiro teor;
- classe/assunto, relator, comarca e órgão julgador;
- datas de julgamento e publicação;
- texto de ocorrências;
- trecho HTML bruto de cada resultado.

> **Escopo do MVP do assistente:** o scraper permanece disponível como
> componente técnico isolado, mas não é chamado pelo assistente de IA, não
> alimenta seus prompts e não integra a apresentação funcional do MVP. A
> política e as condições para uma integração futura estão no
> [Tópico 10 — Legislação, jurisprudência e fontes](../../implementacoes/features/assistente-ia/topico-10-legislacao-jurisprudencia-fontes.md).

## Estratégia híbrida

Uma consulta depende de cookies, `uuidCaptcha` e token do reCAPTCHA. Por isso o
fluxo combina:

1. Playwright para abrir a página real em Chromium e criar a sessão;
2. `HttpClient` com os cookies capturados para consulta e paginação;
3. expressões regulares para extrair dados do HTML.

Consulte [Integração técnica com o TJSP](integracao-tjsp.md) para os endpoints e
detalhes de sessão.

## Componentes

| Componente | Responsabilidade |
|---|---|
| `TjspBrowserSessionProvider` | inicia Chromium, obtém captcha, token e cookies |
| `TjspJurisprudenciaClient` | envia requests HTTP ao e-SAJ |
| `TjspPayloadBuilder` | monta o formulário de pesquisa livre |
| `TjspResultadoParser` | extrai total e itens do HTML |
| `TjspScraperService` | valida, orquestra e pagina |
| classes em `Cli/` | argumentos, mensagens, JSON e códigos de saída |

## Contrato de entrada

`TjspSearchRequest`:

| Campo | Padrão | Observação |
|---|---:|---|
| `Termo` | vazio | obrigatório no serviço |
| `QuantidadeMaxima` | `20` | precisa ser positiva; teto efetivo 200 |
| `TipoDecisao` | `A` | CLI aceita `A`, `H` ou `D` |
| `IncluirSinonimos` | `true` | enviado como `S` ou `N` |
| `SalvarHtmlBruto` | `false` | controla a página inicial completa |
| `OutputPath` | `null` | usado somente pela CLI |

Tipos de decisão:

- `A`: acórdãos;
- `H`: homologações de acordo;
- `D`: decisões monocráticas.

## Contrato de saída

`TjspSearchResult` contém:

```json
{
  "Termo": "negativação indevida",
  "TotalEncontrado": 123,
  "QuantidadeRetornada": 20,
  "Resultados": [
    {
      "Tribunal": "TJSP",
      "NumeroProcesso": "...",
      "Ementa": "...",
      "LinkPdf": "https://esaj.tjsp.jus.br/cjsg/getArquivo.do?...",
      "CodigoAcordao": "...",
      "CodigoForo": "0",
      "ClasseAssunto": "...",
      "Relator": "...",
      "Comarca": "...",
      "OrgaoJulgador": "...",
      "DataJulgamento": "dd/MM/yyyy",
      "DataPublicacao": "dd/MM/yyyy",
      "Ocorrencias": "...",
      "ConteudoBruto": "..."
    }
  ],
  "HtmlPrimeiraPagina": null
}
```

`ConteudoBruto` é preenchido pelo parser em cada item independentemente da opção
`SalvarHtmlBruto`. Essa opção controla somente `HtmlPrimeiraPagina`, que contém
a resposta completa da primeira página.

## Paginação e limites

O código considera 20 itens por página. A primeira página vem da busca
principal; as demais são solicitadas sequencialmente. O número de páginas é o
menor valor entre:

- páginas disponíveis segundo o total informado pelo TJSP;
- páginas necessárias para atender `QuantidadeMaxima`.

O serviço limita a coleta a 200 itens. Um valor maior não gera erro na chamada
direta ao serviço: ele é reduzido para 200. A CLI registra no início o valor
solicitado, que pode ser maior que o teto efetivo.

## Uso dentro da API

A API sempre pesquisa acórdãos, com sinônimos e com armazenamento da página
bruta habilitado. Ela transforma cada `JurisprudenciaResultado` em
`JurisprudenciaRaw`; nesse mapeamento, o `ConteudoBruto` do item é persistido em
`htmlBruto`.
