using System.Text.Json;
namespace JuridicoAI.Api.Domain.Entities;

public sealed class PersonalHistorySession
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string RevocationTokenHash { get; init; } = "";
    public string RefreshTokenHash { get; set; } = "";
    public DateTime CriadaEm { get; init; }
    public DateTime AtualizadaEm { get; set; }
    public DateTime ExpiraEm { get; init; }
    public DateTime? RevogadaEm { get; set; }
}
// A protocol has no FK to content: it survives the authorized purge, without a copy of the chat.
public sealed class AssistenteExclusao
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid ConversaId { get; init; }
    public string Estado { get; set; } = "Autorizada";
    public JsonDocument Arquivos { get; set; } = JsonDocument.Parse("[]");
    public DateTime AutorizadaEm { get; init; }
    public DateTime? ConcluidaEm { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseAte { get; set; }
    public DateTime DisponivelEm { get; set; }
    public int Tentativas { get; set; }
    public string? CodigoErro { get; set; }
}
