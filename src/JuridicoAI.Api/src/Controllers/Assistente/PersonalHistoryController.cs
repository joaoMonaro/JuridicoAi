using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace JuridicoAI.Api.Controllers.Assistente;
[ApiController, Route("assistente/v2/pessoal"), Authorize(AuthenticationSchemes = PersonalHistoryAuthentication.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PersonalHistoryController(PersonalHistoryService service) : ControllerBase
{
    [HttpGet("conversas")]
    public Task<PersonalConversationPage> List([FromQuery] string? antes, [FromQuery] int limite = 30, CancellationToken ct = default) => service.ListAsync(antes, limite, ct);
    [HttpGet("conversas/{id:guid}")]
    public Task<PersonalConversationDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpGet("conversas/{id:guid}/mensagens")]
    public Task<IReadOnlyList<PersonalMessageDto>> Messages(Guid id, [FromQuery] long? antes, [FromQuery] int limite = 50, CancellationToken ct = default) => service.MessagesAsync(id, antes, limite, ct);
    [HttpGet("conversas/{id:guid}/anexos")]
    public Task<IReadOnlyList<PersonalAttachmentDto>> Attachments(Guid id, CancellationToken ct) => service.AttachmentsAsync(id, ct);
    [HttpGet("conversas/{id:guid}/anexos/{anexoId:guid}/arquivo")]
    public async Task<IActionResult> Download(Guid id, Guid anexoId, CancellationToken ct)
    { var result = await service.DownloadAsync(id, anexoId, ct); Response.Headers.XContentTypeOptions = "nosniff"; return File(result.Stream, result.ContentType, result.Name); }
    [HttpGet("propostas/{id:guid}")]
    public Task<PersonalProposalDto> Proposal(Guid id, CancellationToken ct) => service.ProposalAsync(id, ct);
    [HttpDelete("conversas/{id:guid}"), RequestSizeLimit(4096)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteConversationRequest request, CancellationToken ct) => Accepted(await service.DeleteAsync(id, request, ct));
    [HttpGet("exclusoes/{requestId:guid}")]
    public Task<AssistantDeletionDto> Deletion(Guid requestId, CancellationToken ct) => service.DeletionAsync(requestId, ct);
    [HttpGet("exclusoes")]
    public Task<IReadOnlyList<AssistantDeletionDto>> Deletions(CancellationToken ct) => service.DeletionsAsync(ct);
}
