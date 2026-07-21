using System.ComponentModel.DataAnnotations;

namespace JuridicoAI.Api.Application.DTOs;

public sealed class IngestJurisprudenciasRequest
{
    [Required, MinLength(3)]
    public string Termo { get; init; } = string.Empty;

    [Range(1, 200)]
    public int? MaxResultados { get; init; }
}

public sealed record IngestionSummary(
    string TermoBuscado,
    int Coletados,
    int NovosRaw,
    int Duplicados,
    int Classificados,
    int Falhas);

public sealed record ReprocessSummary(int Encontrados, int Reprocessados, int Falhas);

public sealed record JurisprudenciaQuery(
    string? Tema,
    string? Subtema,
    string? Resultado,
    bool? DanoMoral,
    string? Fonte,
    int Limite = 50);
