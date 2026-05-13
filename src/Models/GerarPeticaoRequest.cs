namespace JuridicoAI.Models;

/// <summary>
/// Dados necessários para gerar uma petição inicial com base em jurisprudências já coletadas.
/// </summary>
public sealed class GerarPeticaoRequest
{
    /// <example>Meu nome foi negativado por uma dívida que eu não reconheço.</example>
    public string DescricaoCaso { get; init; } = string.Empty;

    public IReadOnlyList<JurisprudenciaResultado> Jurisprudencias { get; init; } = [];
}
