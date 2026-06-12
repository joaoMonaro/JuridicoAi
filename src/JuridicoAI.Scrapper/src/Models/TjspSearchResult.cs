namespace JuridicoAI.Scrapper.Models;

public sealed class TjspSearchResult
{
    public string Termo { get; init; } = string.Empty;
    public int TotalEncontrado { get; init; }
    public int QuantidadeRetornada { get; init; }
    public IReadOnlyList<JurisprudenciaResultado> Resultados { get; init; } = [];
    public string? HtmlPrimeiraPagina { get; init; }
}
