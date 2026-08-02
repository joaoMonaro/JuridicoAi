using System.Globalization;
using System.Text;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Services;

public sealed class JurisprudenciaClassifierService
{
    private static readonly string[] TemaTermos =
        ["negativacao indevida", "inscricao indevida", "cadastro de inadimplentes", "orgaos de protecao ao credito"];
    private static readonly string[] SubtemaTermos =
        ["debito inexistente", "inexistencia do debito", "relacao juridica inexistente", "contrato inexistente"];
    private static readonly string[] DanoMoralTermos =
        ["dano moral", "danos morais", "indenizacao por dano moral"];
    private static readonly string[] ExclusaoTermos =
        ["exclusao", "baixa da negativacao", "retirada do nome", "cadastro de inadimplentes"];
    private static readonly string[] FavoraveisFortes =
        ["indenizacao mantida", "dano moral configurado", "negativacao indevida", "debito inexistente"];
    private static readonly string[] Desfavoraveis =
        ["dano moral nao configurado", "exercicio regular de direito", "negativacao legitima", "debito comprovado", "improcedente"];

    public JurisprudenciaClassificacao Classificar(Jurisprudencia jurisprudencia)
    {
        var texto = SemAcentos(jurisprudencia.Ementa).ToLowerInvariant();
        var tema = Encontrados(texto, TemaTermos);
        var subtema = Encontrados(texto, SubtemaTermos);
        var danoMoral = Encontrados(texto, DanoMoralTermos);
        var exclusao = Encontrados(texto, ExclusaoTermos);
        var favoraveis = Encontrados(texto, FavoraveisFortes).ToList();
        if (texto.Contains("sentenca mantida") && (favoraveis.Count > 0 || danoMoral.Count > 0)) favoraveis.Add("sentenca mantida");
        if (texto.Contains("recurso improvido") && texto.Contains("indenizacao")) favoraveis.Add("recurso improvido");
        var desfavoraveis = Encontrados(texto, Desfavoraveis);

        var resultado = favoraveis.Count > 0 && desfavoraveis.Count == 0
            ? "provavelmente_favoravel_consumidor"
            : desfavoraveis.Count > 0 && favoraveis.Count == 0
                ? "provavelmente_desfavoravel_consumidor"
                : "indefinido";

        var confianca = 0d;
        if (tema.Count > 0) confianca += .25;
        if (subtema.Count > 0) confianca += .20;
        if (danoMoral.Count > 0) confianca += .15;
        if (exclusao.Count > 0) confianca += .10;
        if (resultado != "indefinido") confianca += .20;
        if (!string.IsNullOrWhiteSpace(jurisprudencia.NumeroProcesso) &&
            !string.IsNullOrWhiteSpace(jurisprudencia.Link) && jurisprudencia.DataJulgamento.HasValue) confianca += .10;
        if (favoraveis.Count > 0 && desfavoraveis.Count > 0) confianca = Math.Min(confianca, .45);

        var palavras = tema.Concat(subtema).Concat(danoMoral).Concat(exclusao)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new JurisprudenciaClassificacao
        {
            Tema = tema.Count > 0 ? "negativacao_indevida" : "indefinido",
            Subtema = subtema.Count > 0 ? "debito_inexistente" : "indefinido",
            TipoAcao = subtema.Count > 0 && danoMoral.Count > 0
                ? "declaratoria_inexistencia_debito_indenizacao" : "indefinido",
            DanoMoral = danoMoral.Count > 0,
            ExclusaoNegativacao = exclusao.Count > 0,
            Resultado = resultado,
            PalavrasChave = palavras,
            Confianca = Math.Round(Math.Min(1, confianca), 2)
        };
    }

    private static IReadOnlyList<string> Encontrados(string texto, IEnumerable<string> termos) =>
        termos.Where(texto.Contains).ToList();

    private static string SemAcentos(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalizado.Length);
        foreach (var caractere in normalizado)
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark) builder.Append(caractere);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
