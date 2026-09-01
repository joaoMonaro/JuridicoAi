namespace JuridicoAI.Api.Domain.Entities;

public enum CasoStatus
{
    Ativo,
    Arquivado
}

public sealed class Caso
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public CasoStatus Status { get; set; } = CasoStatus.Ativo;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}
