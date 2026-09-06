using System.Text.Json;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class Peca
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid CasoId { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public JsonDocument ConteudoJson { get; set; } = null!;
    public Guid? TemplateId { get; init; }
    public JsonDocument PageConfig { get; set; } = null!;
    public JsonDocument? HeaderJson { get; set; }
    public JsonDocument? FooterJson { get; set; }
    public JsonDocument Styles { get; set; } = null!;
    public PecaStatusEnum Status { get; set; } = PecaStatusEnum.Rascunho;
    public int Versao { get; set; } = 1;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
    public DateTime? DataExclusao { get; set; }
}
