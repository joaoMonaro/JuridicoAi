namespace JuridicoAI.Api.Domain.Entities;

public enum CasoStatus
{
    Ativo,
    Arquivado
}

public sealed class Caso
{
    public Guid Id { get; init; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public CasoStatus Status { get; set; } = CasoStatus.Ativo;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}
