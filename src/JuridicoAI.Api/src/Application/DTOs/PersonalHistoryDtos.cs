using System.Text.Json;
namespace JuridicoAI.Api.Application.DTOs;
public sealed record PersonalConversationDto(Guid Id, string Titulo, DateTime CriadaEm, DateTime AtualizadaEm);
public sealed record PersonalConversationPage(IReadOnlyList<PersonalConversationDto> Conversas, string? ProximoCursor);
public sealed record PersonalAttachmentDto(Guid Id, string Nome, long TamanhoBytes, DateTime CriadoEm);
public sealed record PersonalMessageDto(Guid Id, long Sequencia, string Autor, string Texto, DateTime CriadaEm,
    string Estado, string? Titulo, string? ContextoFornecido, Guid? PropostaId, IReadOnlyList<AssistenteAlertaDto> Alertas);
public sealed record PersonalProposalGroup(Guid Id, string Estado, string Explicacao, IReadOnlyList<JsonElement> Original, IReadOnlyList<JsonElement> Proposto);
public sealed record PersonalProposalDto(Guid Id, Guid ConversaId, IReadOnlyList<PersonalProposalGroup> Grupos);
public sealed record DeleteConversationRequest(Guid RequestId, bool ConfirmarExclusao);
public sealed record AssistantDeletionDto(Guid RequestId, Guid ConversaId, string Estado, DateTime AutorizadaEm,
    DateTime? ConcluidaEm, string? CodigoErro, bool EliminadaDasCopias = false);
