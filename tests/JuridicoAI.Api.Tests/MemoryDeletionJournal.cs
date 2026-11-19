using JuridicoAI.Api.Application.Abstractions;
namespace JuridicoAI.Api.Tests;
public sealed class MemoryDeletionJournal : IAssistenteDeletionJournal
{
    public bool Configured => true;
    public bool Fail { get; set; }
    public List<AssistenteDeletionAuthorization> Records { get; } = [];
    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
    public Task AppendAsync(AssistenteDeletionAuthorization record, CancellationToken ct)
    {
        if (Fail) throw new IOException("Falha simulada do registro independente.");
        lock (Records)
        {
            var existing = Records.FirstOrDefault(r => r.RequestId == record.RequestId || r.ConversationId == record.ConversationId);
            if (existing is not null && existing != record) throw new InvalidOperationException("Conflito simulado de autorização.");
            if (existing is null) Records.Add(record);
        }
        return Task.CompletedTask;
    }
    public Task<AssistenteDeletionJournalSnapshot> ReadAsync(CancellationToken ct)
    { lock (Records) return Task.FromResult(new AssistenteDeletionJournalSnapshot(new string('a',64), Records.ToArray())); }
}
