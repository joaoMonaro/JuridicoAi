using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace JuridicoAI.Api.Controllers.Assistente;
[ApiController, Authorize, Route("assistente/v2/conversas/{conversaId:guid}/geracoes")]
public sealed class AssistenteGenerationController(AssistenteGenerationService generations) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssistenteGeracaoDto>> Get(Guid conversaId, Guid id, CancellationToken ct) => Ok(await generations.GetAsync(conversaId, id, ct));
    [HttpPost("{id:guid}/cancelamento")]
    public async Task<IActionResult> Cancel(Guid conversaId, Guid id, CancellationToken ct) { await generations.ActAsync(conversaId, id, false, ct); return NoContent(); }
    [HttpPost("{id:guid}/retomada")]
    public async Task<IActionResult> Retry(Guid conversaId, Guid id, CancellationToken ct) { await generations.ActAsync(conversaId, id, true, ct); return Accepted(); }
}
