namespace JuridicoAI.Scrapper.Models;

public sealed class JurisprudenciaResultado
{
    public string Tribunal { get; init; } = string.Empty;
    public string NumeroProcesso { get; init; } = string.Empty;
    public string Ementa { get; init; } = string.Empty;
    public string LinkPdf { get; init; } = string.Empty;
    public string? CodigoAcordao { get; init; }
    public string? CodigoForo { get; init; }
    public string? ClasseAssunto { get; init; }
    public string? Relator { get; init; }
    public string? Comarca { get; init; }
    public string? OrgaoJulgador { get; init; }
    public string? DataJulgamento { get; init; }
    public string? DataPublicacao { get; init; }
    public string? Ocorrencias { get; init; }
    public string? ConteudoBruto { get; init; }
}
