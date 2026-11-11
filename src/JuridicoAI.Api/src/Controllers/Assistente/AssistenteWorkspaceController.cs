using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Assistente;

[ApiController, Authorize, Route("assistente/v2")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssistenteWorkspaceController(AssistenteWorkspaceService service) : ControllerBase
{
    [HttpPost("pecas/{pecaId:guid}/conversa")]
    public async Task<AssistenteConversaDto> Conversa(Guid pecaId, CancellationToken ct) => await service.ObterOuCriarAsync(pecaId, ct);
    [HttpGet("conversas/{id:guid}/mensagens")]
    public async Task<IReadOnlyList<AssistenteMensagemDto>> Mensagens(Guid id, [FromQuery] long? antes, [FromQuery] int limite = 50, CancellationToken ct = default) => await service.MensagensAsync(id, antes, limite, ct);
    [HttpGet("conversas/{id:guid}/propostas")]
    public async Task<IReadOnlyList<AssistentePropostaDto>> Propostas(Guid id, CancellationToken ct) => await service.PropostasAsync(id, ct);
    [HttpPost("conversas/{id:guid}/propostas")]
    public async Task<AssistentePropostaDto> Registrar(Guid id, RegistrarPropostaRequest request, CancellationToken ct) => await service.RegistrarAsync(id, request, ct);
    [HttpGet("propostas/{id:guid}")]
    public async Task<AssistentePropostaDto> Proposta(Guid id, CancellationToken ct) => await service.PropostaAsync(id, ct);
    [HttpPost("propostas/{id:guid}/aplicacoes")]
    public async Task<AssistenteReciboDto> Aplicar(Guid id, AplicarGruposRequest request, CancellationToken ct) => await service.AplicarAsync(id, request, ct);
    [HttpGet("conversas/{id:guid}/aplicacoes/{requestId:guid}")]
    public async Task<AssistenteReciboDto> Recibo(Guid id, Guid requestId, CancellationToken ct) => await service.ReciboAsync(id, requestId, ct);
    [HttpPost("propostas/{id:guid}/rejeicoes")]
    public async Task<AssistentePropostaDto> Rejeitar(Guid id, RejeitarGruposRequest request, CancellationToken ct) => await service.RejeitarAsync(id, request, ct);
}
