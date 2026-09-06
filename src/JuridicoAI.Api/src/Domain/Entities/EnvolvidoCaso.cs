using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class EnvolvidoCaso
{
    public Guid Id { get; init; }
    public Guid CasoId { get; init; }
    public string Nome { get; set; } = string.Empty;
    public TipoEnvolvidoCasoEnum Tipo { get; set; }
    public string? Documento { get; set; }
    public IReadOnlyDictionary<string, string?>? DadosAdicionais { get; set; }
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}
