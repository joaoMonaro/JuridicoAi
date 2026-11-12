using System.Text.Json;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class AssistenteConversa
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public Guid PecaId { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public long ProximaSequencia { get; set; } = 1;
    public DateTime CriadaEm { get; init; }
    public DateTime AtualizadaEm { get; set; }
}

public sealed class AssistenteMensagemPersistida
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid ConversaId { get; init; }
    public long Sequencia { get; init; }
    public string Autor { get; init; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public Guid? PropostaId { get; set; }
    public Guid? ClientRequestId { get; init; }
    public string? RequestSha256 { get; init; }
    public Guid? RespostaAId { get; init; }
    public JsonDocument? Solicitacao { get; init; }
    public JsonDocument? Resultado { get; set; }
    public JsonDocument? FontesDocumentais { get; set; }
    public JsonDocument? CoberturaDocumental { get; set; }
    public Guid? AnaliseDocumentalId { get; set; }
    public JsonDocument? ContextoHistorico { get; set; }
    public string Estado { get; set; } = "Concluida";
    public string? CodigoErro { get; set; }
    public DateTime? ExpiraEm { get; set; }
    public DateTime CriadaEm { get; init; }
}

public sealed class AssistentePropostaPersistida
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid ConversaId { get; init; }
    public Guid ClientRequestId { get; init; }
    public string RequestSha256 { get; init; } = string.Empty;
    public Guid SnapshotId { get; set; }
    public int Revisao { get; init; } = 1;
    public int VersaoDecisoes { get; set; } = 1;
    public int VersaoPecaAplicavel { get; set; }
    public Guid? SubstituidaPorId { get; set; }
    public JsonDocument DocumentoBase { get; set; } = null!;
    public JsonDocument Grupos { get; set; } = null!;
    public DateTime CriadaEm { get; init; }
}

public sealed class AssistenteAplicacaoRecibo
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid ConversaId { get; init; }
    public Guid PropostaId { get; init; }
    public Guid RequestId { get; init; }
    public string RequestSha256 { get; init; } = string.Empty;
    public int VersaoAnterior { get; init; }
    public int VersaoResultante { get; init; }
    public JsonDocument ConteudoJson { get; init; } = null!;
    public JsonDocument GruposAplicados { get; init; } = null!;
    public DateTime CriadaEm { get; init; }
}

// Append-only derived snapshots. Original messages are never replaced by memory.
public sealed class AssistenteMemoria
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid ConversaId { get; init; }
    public Guid RespostaId { get; init; }
    public int Versao { get; init; }
    public long AteSequencia { get; init; }
    public string PoliticaVersao { get; init; } = string.Empty;
    public JsonDocument Registros { get; init; } = null!;
    public DateTime CriadaEm { get; init; }
}
