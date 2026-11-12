using System.Text.Json;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class AssistenteAnaliseDocumental
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public Guid ConversaId { get; init; }
    public Guid PedidoId { get; init; }
    public Guid RespostaId { get; init; }
    public Guid PecaId { get; init; }
    public Guid CasoId { get; init; }
    public Guid ClientRequestId { get; init; }
    public Guid CorrelationId { get; init; }
    public JsonDocument Manifesto { get; init; } = null!;
    public string ManifestoSha256 { get; init; } = "";
    public JsonDocument Fontes { get; init; } = null!;
    public string PoliticaVersao { get; init; } = "";
    public string Modelo { get; init; } = "";
    public string Estado { get; set; } = "NaFila";
    public int FonteIndice { get; set; }
    public int AposOrdem { get; set; } = -1;
    public int TokensFinaisReservados { get; set; }
    public Guid? SolicitacaoFinalId { get; set; }
    public JsonDocument? Resumo { get; set; }
    public string? CodigoErro { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseAte { get; set; }
    public DateTime CriadaEm { get; init; }
    public DateTime AtualizadaEm { get; set; }
    public DateTime PrazoEm { get; init; }
}

public sealed class AssistenteAnaliseLote
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid AnaliseId { get; init; }
    public int Ordem { get; init; }
    public string Tipo { get; init; } = "Leitura";
    public string Estado { get; set; } = "Preparado";
    public JsonDocument Entradas { get; init; } = null!;
    public JsonDocument Referencias { get; init; } = null!;
    public JsonDocument? Registros { get; set; }
    public Guid? ConsolidadoPorId { get; set; }
    public Guid? DocumentoId { get; init; }
    public int? UltimaOrdem { get; init; }
    public int TokensReservados { get; set; }
    public int TokensLiquidados { get; set; }
    public int Chamadas { get; set; }
    public DateTime CriadaEm { get; init; }
}
