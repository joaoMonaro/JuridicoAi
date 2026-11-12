using System.Text.Json;
namespace JuridicoAI.Api.Domain.Entities;

public sealed class AssistenteGeracao
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public Guid ConversaId { get; init; }
    public Guid PedidoId { get; init; }
    public Guid RespostaId { get; init; }
    public Guid PecaId { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid ClientRequestId { get; init; }
    public string Estado { get; set; } = "NaFila";
    public string Modelo { get; init; } = "";
    public string PoliticaVersao { get; init; } = "";
    public string CasoSha256 { get; init; } = "";
    public JsonDocument FontesIds { get; init; } = null!;
    public JsonDocument? Contexto { get; set; }
    public JsonDocument? Plano { get; set; }
    public Guid? AnaliseId { get; set; }
    public string? CodigoErro { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseAte { get; set; }
    public DateTime CriadaEm { get; init; }
    public DateTime AtualizadaEm { get; set; }
    public DateTime PrazoEm { get; init; }
}
public sealed class AssistenteGeracaoEtapa
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid GeracaoId { get; init; }
    public Guid SolicitacaoId { get; init; }
    public int Chamada { get; init; }
    public string Tipo { get; init; } = "";
    public string Estado { get; set; } = "Chamando";
    public int TokensReservados { get; init; }
    public int TokensLiquidados { get; set; }
    public JsonDocument? Resultado { get; set; }
    public DateTime CriadaEm { get; init; }
}
