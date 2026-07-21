using System.Globalization;
using System.Text.RegularExpressions;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Services;

public sealed partial class JurisprudenciaNormalizerService
{
    private static readonly string[] DateFormats =
        ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy"];

    public Jurisprudencia Normalizar(JurisprudenciaRaw raw)
    {
        if (string.IsNullOrWhiteSpace(raw.Id))
        {
            throw new ArgumentException("O dado bruto precisa estar persistido antes da normalizacao.", nameof(raw));
        }

        return new Jurisprudencia
        {
            RawId = raw.Id,
            Fonte = Limpar(raw.Fonte).ToUpperInvariant(),
            NumeroProcesso = Limpar(raw.NumeroProcesso),
            Relator = NuloSeVazio(raw.Relator),
            OrgaoJulgador = NuloSeVazio(raw.OrgaoJulgador),
            DataJulgamento = ParseData(raw.DataJulgamento),
            DataPublicacao = ParseData(raw.DataPublicacao),
            Ementa = Limpar(raw.EmentaBruta),
            Link = Limpar(raw.Link),
            DataProcessamento = DateTime.UtcNow
        };
    }

    private static string Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : EspacosRegex().Replace(valor, " ").Trim();

    private static string? NuloSeVazio(string? valor)
    {
        var limpo = Limpar(valor);
        return limpo.Length == 0 ? null : limpo;
    }

    private static DateTime? ParseData(string? valor)
    {
        var limpo = Limpar(valor);
        if (limpo.Length == 0) return null;

        return DateTime.TryParseExact(limpo, DateFormats, CultureInfo.GetCultureInfo("pt-BR"),
            DateTimeStyles.AssumeLocal, out var data)
            ? DateTime.SpecifyKind(data, DateTimeKind.Utc)
            : null;
    }

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex EspacosRegex();
}
