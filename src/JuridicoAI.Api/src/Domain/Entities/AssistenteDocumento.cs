using System.Text.Json;
using NpgsqlTypes;

namespace JuridicoAI.Api.Domain.Entities;

// The source itself is the durable extraction job. Lease fencing prevents late
// workers from publishing over a retried attempt. Original files never expire.
public sealed class AssistenteDocumento
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid CasoId { get; init; }
    public Guid? ConversaId { get; init; }
    public Guid? DocumentoCasoId { get; init; }
    public Guid? ClientUploadId { get; init; }
    public string Nome { get; init; } = "";
    public string Formato { get; init; } = "";
    public string StorageId { get; init; } = "";
    public long TamanhoBytes { get; init; }
    public string? Sha256 { get; set; }
    public string Estado { get; set; } = "Recebido";
    public bool Vinculado { get; set; }
    public string? CodigoErro { get; set; }
    public int? PaginasTotal { get; set; }
    public int PaginasConcluidas { get; set; }
    public int Tentativas { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseAte { get; set; }
    public string ExtratorVersao { get; set; } = "documentos-v1";
    public string QualidadeVersao { get; set; } = "qualidade-v1";
    public DateTime CriadoEm { get; init; } = DateTime.UtcNow;
}

public sealed class AssistenteDocumentoTrecho
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid DocumentoId { get; init; }
    public int Ordem { get; init; }
    public int? Pagina { get; init; }
    public string Localizador { get; init; } = "";
    public string Texto { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string Qualidade { get; init; } = "Duvidoso";
    public string? Motivo { get; init; }
    public JsonDocument Metricas { get; init; } = JsonDocument.Parse("{}");
    public NpgsqlTsVector Busca { get; private set; } = null!;
}
