using JuridicoAI.Domain.Entities;

namespace JuridicoAI.Application.DTOs;

/// <summary>
/// Dados necessários para gerar uma petição inicial com base em jurisprudências já coletadas
/// ou pesquisadas automaticamente.
/// </summary>
public sealed class GerarPeticaoRequest
{
    /// <example>Meu nome foi negativado por uma dívida que eu não reconheço.</example>
    public string DescricaoCaso { get; init; } = string.Empty;

    /// <summary>
    /// Termo opcional para pesquisar jurisprudências no TJSP antes da geração.
    /// </summary>
    /// <example>negativação indevida dívida não reconhecida dano moral</example>
    public string? TermoPesquisaJurisprudencia { get; init; }

    /// <summary>
    /// Quantidade máxima de jurisprudências a pesquisar no TJSP. O padrão é 10.
    /// </summary>
    /// <example>10</example>
    public int? QuantidadeMaximaJurisprudencias { get; init; }

    public IReadOnlyList<JurisprudenciaResultado> Jurisprudencias { get; init; } = [];
}
