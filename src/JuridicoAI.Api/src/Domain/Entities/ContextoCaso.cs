namespace JuridicoAI.Api.Domain.Entities;

public enum TipoContextoCaso
{
    RelatoInicial,
    ResumoDosFatos,
    Cronologia,
    Observacao,
    Estrategia,
    Outro
}

public sealed class ContextoCaso
{
    public Guid Id { get; init; }
    public Guid CasoId { get; init; }
    public TipoContextoCaso Tipo { get; set; }
    public string? Titulo { get; set; }
    public string Conteudo { get; set; } = string.Empty;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}
