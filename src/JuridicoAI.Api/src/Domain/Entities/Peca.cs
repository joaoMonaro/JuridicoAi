using System.Text.Json;

namespace JuridicoAI.Api.Domain.Entities;

public enum PecaStatus
{
    Rascunho,
    Finalizada
}

public sealed class Peca
{
    public Guid Id { get; init; }
    public Guid CasoId { get; init; }
    public string Titulo { get; set; } = string.Empty;
    public JsonDocument ConteudoJson { get; set; } = null!;
    public Guid? TemplateId { get; init; }
    public JsonDocument PageConfig { get; set; } = null!;
    public JsonDocument? HeaderJson { get; set; }
    public JsonDocument? FooterJson { get; set; }
    public JsonDocument Styles { get; set; } = null!;
    public PecaStatus Status { get; set; } = PecaStatus.Rascunho;
    public int Versao { get; set; } = 1;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
    public DateTime? DataExclusao { get; set; }
}
