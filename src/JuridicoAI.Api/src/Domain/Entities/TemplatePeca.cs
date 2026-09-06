using System.Text.Json;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class TemplatePeca
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; set; }
    public string Nome { get; set; } = string.Empty;
    public JsonDocument ConteudoJson { get; set; } = null!;
    public JsonDocument PageConfig { get; set; } = null!;
    public JsonDocument? HeaderJson { get; set; }
    public JsonDocument? FooterJson { get; set; }
    public JsonDocument Styles { get; set; } = null!;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
    public DateTime? DataExclusao { get; set; }
}
