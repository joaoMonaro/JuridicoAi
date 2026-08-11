namespace JuridicoAI.Api.Domain.Entities;

public enum TipoEnvolvidoCaso
{
    Cliente,
    ParteContraria,
    Advogado,
    Testemunha,
    TerceiroInteressado,
    Outro
}

public sealed class EnvolvidoCaso
{
    public Guid Id { get; init; }
    public Guid CasoId { get; init; }
    public string Nome { get; set; } = string.Empty;
    public TipoEnvolvidoCaso Tipo { get; set; }
    public string? Documento { get; set; }
    public IReadOnlyDictionary<string, string?>? DadosAdicionais { get; set; }
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}
