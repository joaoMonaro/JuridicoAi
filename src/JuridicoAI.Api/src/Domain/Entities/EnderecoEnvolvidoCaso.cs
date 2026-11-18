using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class EnderecoEnvolvidoCaso
{
    public Guid Id { get; init; }
    public Guid EnvolvidoCasoId { get; init; }
    public TipoEnderecoEnum Tipo { get; set; }
    public bool Principal { get; set; }
    public string? Cep { get; set; }
    public string Logradouro { get; set; } = string.Empty;
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string Cidade { get; set; } = string.Empty;
    public string? Uf { get; set; }
    public string? EstadoProvincia { get; set; }
    public string Pais { get; set; } = "BR";
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}
