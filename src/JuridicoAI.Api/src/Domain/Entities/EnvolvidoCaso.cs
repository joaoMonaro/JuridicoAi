using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class EnvolvidoCaso
{
    public Guid Id { get; init; }
    public Guid CasoId { get; init; }
    public string Nome { get; set; } = string.Empty;
    public TipoEnvolvidoCasoEnum Tipo { get; set; }
    public NaturezaPessoaEnum Natureza { get; set; } = NaturezaPessoaEnum.NaoInformada;
    public string? NomeFantasia { get; set; }
    public string? CpfCnpj { get; set; }
    public string? Rg { get; set; }
    public string? RgOrgaoEmissor { get; set; }
    public string? RgUf { get; set; }
    public string? Nacionalidade { get; set; }
    public EstadoCivilEnum? EstadoCivil { get; set; }
    public string? Profissao { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public string? InscricaoEstadual { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    // Campos legados preservados durante a transicao dos contratos.
    public string? Documento { get; set; }
    public IReadOnlyDictionary<string, string?>? DadosAdicionais { get; set; }
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}
