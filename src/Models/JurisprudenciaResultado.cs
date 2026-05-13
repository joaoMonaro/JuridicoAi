namespace JuridicoAI.Models;

public sealed class JurisprudenciaResultado
{
    public string Tribunal { get; init; } = string.Empty;
    public string NumeroProcesso { get; init; } = string.Empty;
    public string Ementa { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
    public string? Relator { get; init; }
    public string? DataJulgamento { get; init; }
}
