using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Pecas;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class PecasController(PecaApplicationService service) : ControllerBase
{
    [HttpPost("casos/{casoId:guid}/pecas")]
    public async Task<ActionResult<Peca>> Criar(Guid casoId, CriarPecaRequest request, CancellationToken ct)
    {
        var item = await service.CriarAsync(casoId, request, ct);
        return CreatedAtAction(nameof(Obter), new { pecaId = item.Id }, item);
    }

    [HttpGet("casos/{casoId:guid}/pecas")]
    public async Task<ActionResult<IReadOnlyList<PecaListaDto>>> Listar(Guid casoId, CancellationToken ct) =>
        Ok(await service.ListarAsync(casoId, ct));

    [HttpGet("pecas/{pecaId:guid}")]
    public async Task<ActionResult<Peca>> Obter(Guid pecaId, CancellationToken ct) => Ok(await service.ObterAsync(pecaId, ct));

    [HttpPatch("pecas/{pecaId:guid}")]
    public async Task<ActionResult<PecaAtualizadaDto>> Atualizar(Guid pecaId, AtualizarPecaRequest request, CancellationToken ct) =>
        Ok(await service.AtualizarAsync(pecaId, request, ct));

    [HttpDelete("pecas/{pecaId:guid}")]
    public async Task<IActionResult> Excluir(Guid pecaId, [FromQuery] int versao, CancellationToken ct)
    { await service.ExcluirAsync(pecaId, versao, ct); return NoContent(); }
}
