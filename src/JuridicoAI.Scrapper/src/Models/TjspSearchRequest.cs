namespace JuridicoAI.Scrapper.Models;

public sealed class TjspSearchRequest
{
    public string Termo { get; init; } = string.Empty;
    public int QuantidadeMaxima { get; init; } = 20;
    public string TipoDecisao { get; init; } = "A";
    public bool IncluirSinonimos { get; init; } = true;
    public bool SalvarHtmlBruto { get; init; }
    public string? OutputPath { get; init; }
}
