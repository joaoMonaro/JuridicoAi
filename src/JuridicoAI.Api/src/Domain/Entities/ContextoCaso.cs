using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class ContextoCaso
{
    public Guid Id { get; init; }
    public Guid CasoId { get; init; }
    public TipoContextoCasoEnum Tipo { get; set; }
    public string? Titulo { get; set; }
    public string Conteudo { get; set; } = string.Empty;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}
