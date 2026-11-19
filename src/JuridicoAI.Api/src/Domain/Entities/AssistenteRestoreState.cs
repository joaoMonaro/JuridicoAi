namespace JuridicoAI.Api.Domain.Entities;

public sealed class AssistenteRestoreState
{
    public int Id { get; init; } = 1;
    public Guid RunId { get; set; }
    public bool InProgress { get; set; }
    public string JournalSha256 { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
