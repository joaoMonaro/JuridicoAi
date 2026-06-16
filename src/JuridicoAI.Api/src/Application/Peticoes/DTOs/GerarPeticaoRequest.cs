using JuridicoAI.Domain.Entities;

namespace JuridicoAI.Api.Application.Peticoes.DTOs;

public class GerarPeticaoRequest
{
    public string DescricaoCaso { get; init; } = string.Empty;
    public string? TermoPesquisaJurisprudencia { get; init; }
    public int? QuantidadeMaximaJurisprudencias { get; init; }
    public IReadOnlyList<JurisprudenciaResultado> Jurisprudencias { get; init; } = [];    
}