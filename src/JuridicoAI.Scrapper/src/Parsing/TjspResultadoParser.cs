using System.Net;
using System.Text.RegularExpressions;
using JuridicoAI.Scrapper.Models;

namespace JuridicoAI.Scrapper.Parsing;

public sealed class TjspResultadoParser
{
    private const string TribunalNome = "TJSP";
    private const string BaseUrl = "https://esaj.tjsp.jus.br";

    private static readonly Regex TotalRegex = new(
        "id=\\\"totalResultadoAba-[A-Z]\\\"[^>]*value=\\\"(?<total>\\d+)\\\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ResultadoInicioRegex = new(
        "<tr\\s+class=\\\"fundocinza1\\\"[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ProcessoRegex = new(
        "<a[^>]*class=\\\"[^\\\"]*esajLinkLogin[^\\\"]*downloadEmenta[^\\\"]*\\\"[^>]*cdAcordao=\\\"(?<cdAcordao>\\d+)\\\"[^>]*cdForo=\\\"(?<cdForo>\\d+)\\\"[^>]*>(?<processo>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CampoRegex = new(
        "<strong>\\s*(?<chave>[^:<]+):\\s*</strong>\\s*(?<valor>.*?)\\s*</td>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex EmentaSemFormatacaoRegex = new(
        "<div\\s+id=\\\"dadosSemFormatacao_(?<cdAcordao>\\d+)\\\"[^>]*>.*?<div\\s+id=\\\"textAreaDados_\\k<cdAcordao>\\\"[^>]*class=\\\"[^\\\"]*mensagemSemFormatacao[^\\\"]*\\\"[^>]*>(?<ementa>.*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex EmentaFormatadaRegex = new(
        "<div\\s+align=\\\"justify\\\"[^>]*style=\\\"display:\\s*none;?\\\"[^>]*>\\s*<strong>\\s*Ementa:\\s*</strong>(?<ementa>.*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OcorrenciasRegex = new(
        "<span\\s+class=\\\"segredoJustica\\\"[^>]*>(?<texto>.*?)</span>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagsRegex = new("<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex EspacosRegex = new("\\s+", RegexOptions.Compiled);

    public TjspParsedResult Parse(string html, int quantidadeMaxima)
    {
        if (string.IsNullOrWhiteSpace(html) || quantidadeMaxima <= 0)
        {
            return new TjspParsedResult(0, []);
        }

        var htmlResultados = ObterHtmlResultados(html);
        var total = ObterTotal(htmlResultados);
        var chunks = ResultadoInicioRegex.Split(htmlResultados)
            .Skip(1)
            .Take(quantidadeMaxima)
            .ToArray();

        var resultados = new List<JurisprudenciaResultado>(chunks.Length);

        foreach (var chunk in chunks)
        {
            var resultado = ParseItem(chunk);
            if (!string.IsNullOrWhiteSpace(resultado.NumeroProcesso) || !string.IsNullOrWhiteSpace(resultado.Ementa))
            {
                resultados.Add(resultado);
            }
        }

        return new TjspParsedResult(total, resultados);
    }

    public static string ObterHtmlResultados(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var partes = html.Split("#%#quebra_resposta#%#", StringSplitOptions.None);
        return partes.Length > 1 ? partes[0] : html;
    }

    private static JurisprudenciaResultado ParseItem(string html)
    {
        var processoMatch = ProcessoRegex.Match(html);
        var cdAcordao = processoMatch.Success ? processoMatch.Groups["cdAcordao"].Value : string.Empty;
        var cdForo = processoMatch.Success ? processoMatch.Groups["cdForo"].Value : "0";
        var processo = processoMatch.Success ? LimparTexto(processoMatch.Groups["processo"].Value) : string.Empty;
        var campos = ObterCampos(html);
        var ementaSemFormatacao = ObterEmentaSemFormatacao(html);
        var ementa = !string.IsNullOrWhiteSpace(ementaSemFormatacao)
            ? ementaSemFormatacao
            : ObterEmentaFormatada(html);

        return new JurisprudenciaResultado
        {
            Tribunal = TribunalNome,
            NumeroProcesso = processo,
            Ementa = ementa,
            LinkPdf = MontarLinkPdf(cdAcordao, cdForo),
            CodigoAcordao = string.IsNullOrWhiteSpace(cdAcordao) ? null : cdAcordao,
            CodigoForo = string.IsNullOrWhiteSpace(cdForo) ? null : cdForo,
            ClasseAssunto = ObterValor(campos, "classe/assunto"),
            Relator = ObterValor(campos, "relator(a)", "relator", "relatora"),
            Comarca = ObterValor(campos, "comarca"),
            OrgaoJulgador = ObterValor(campos, "orgao julgador", "órgão julgador"),
            DataJulgamento = ObterValor(campos, "data do julgamento", "data de julgamento"),
            DataPublicacao = ObterValor(campos, "data de publicacao", "data de publicação"),
            Ocorrencias = ObterOcorrencias(html),
            ConteudoBruto = html
        };
    }

    private static int ObterTotal(string html)
    {
        var match = TotalRegex.Match(html);
        return match.Success && int.TryParse(match.Groups["total"].Value, out var total) ? total : 0;
    }

    private static Dictionary<string, string> ObterCampos(string html)
    {
        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in CampoRegex.Matches(html))
        {
            var chave = NormalizarChave(match.Groups["chave"].Value);
            var valor = LimparTexto(match.Groups["valor"].Value);

            if (!string.IsNullOrWhiteSpace(chave) && !string.IsNullOrWhiteSpace(valor))
            {
                campos[chave] = valor;
            }
        }

        return campos;
    }

    private static string? ObterValor(IReadOnlyDictionary<string, string> campos, params string[] chaves)
    {
        foreach (var chave in chaves)
        {
            if (campos.TryGetValue(NormalizarChave(chave), out var valor) && !string.IsNullOrWhiteSpace(valor))
            {
                return valor;
            }
        }

        return null;
    }

    private static string ObterEmentaSemFormatacao(string html)
    {
        var match = EmentaSemFormatacaoRegex.Match(html);
        return match.Success ? LimparTexto(match.Groups["ementa"].Value) : string.Empty;
    }

    private static string ObterEmentaFormatada(string html)
    {
        var matches = EmentaFormatadaRegex.Matches(html);
        return matches.Count == 0 ? string.Empty : LimparTexto(matches[^1].Groups["ementa"].Value);
    }

    private static string? ObterOcorrencias(string html)
    {
        var match = OcorrenciasRegex.Match(html);
        var texto = match.Success ? LimparTexto(match.Groups["texto"].Value) : string.Empty;
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private static string MontarLinkPdf(string cdAcordao, string cdForo)
    {
        if (string.IsNullOrWhiteSpace(cdAcordao))
        {
            return string.Empty;
        }

        var foro = string.IsNullOrWhiteSpace(cdForo) ? "0" : cdForo;
        return $"{BaseUrl}/cjsg/getArquivo.do?cdAcordao={Uri.EscapeDataString(cdAcordao)}&cdForo={Uri.EscapeDataString(foro)}";
    }

    private static string LimparTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var semTags = TagsRegex.Replace(texto, " ");
        var decodificado = WebUtility.HtmlDecode(semTags);
        return EspacosRegex.Replace(decodificado, " ").Trim();
    }

    private static string NormalizarChave(string chave)
    {
        return chave
            .Trim()
            .ToLowerInvariant()
            .Replace("á", "a")
            .Replace("à", "a")
            .Replace("ã", "a")
            .Replace("â", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("õ", "o")
            .Replace("ô", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");
    }
}

public sealed record TjspParsedResult(int Total, IReadOnlyList<JurisprudenciaResultado> Resultados);
