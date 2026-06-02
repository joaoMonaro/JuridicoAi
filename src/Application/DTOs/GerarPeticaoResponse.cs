namespace JuridicoAI.Application.DTOs;

/// <summary>
/// Resultado da geração da petição.
/// </summary>
public sealed class GerarPeticaoResponse
{
    public string Peticao { get; init; } = string.Empty;
    public IReadOnlyList<string> Alertas { get; init; } = [];
    public IReadOnlyList<JurisprudenciaUtilizada> JurisprudenciasUtilizadas { get; init; } = [];
}

public sealed class JurisprudenciaUtilizada
{
    public string Tribunal { get; init; } = string.Empty;
    public string NumeroProcesso { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
}
