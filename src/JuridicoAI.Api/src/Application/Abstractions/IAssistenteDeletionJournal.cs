namespace JuridicoAI.Api.Application.Abstractions;

// Authorization metadata only. No document names, storage paths or message content.
public sealed record AssistenteDeletionAuthorization(Guid RequestId, Guid UserId, Guid ConversationId, DateTime AuthorizedAt);
public sealed record AssistenteDeletionJournalSnapshot(string Sha256, IReadOnlyList<AssistenteDeletionAuthorization> Records);
public interface IAssistenteDeletionJournal
{
    bool Configured { get; }
    Task InitializeAsync(CancellationToken ct);
    Task AppendAsync(AssistenteDeletionAuthorization record, CancellationToken ct);
    Task<AssistenteDeletionJournalSnapshot> ReadAsync(CancellationToken ct);
}
public interface IPrivateConversationFiles
{
    Task<IReadOnlyList<string>> ListAsync(Guid conversationId, CancellationToken ct);
}
