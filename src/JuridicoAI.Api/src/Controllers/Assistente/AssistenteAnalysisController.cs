using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Assistente;

[ApiController, Authorize, Route("assistente/v2/conversas/{conversaId:guid}/analises")]
public sealed class AssistenteAnalysisController(AssistenteDocumentAnalysisService analyses) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssistenteAnaliseDocumentalDto>> Get(Guid conversaId, Guid id, CancellationToken ct) =>
        Ok(await analyses.GetAsync(conversaId, id, ct));
    [HttpPost("{id:guid}/retomada")]
    public async Task<IActionResult> Retry(Guid conversaId, Guid id, CancellationToken ct)
    { await analyses.RetryAsync(conversaId, id, ct); return Accepted(); }
    [HttpPost("{id:guid}/cancelamento")]
    public async Task<IActionResult> Cancel(Guid conversaId, Guid id, CancellationToken ct)
    { await analyses.CancelAsync(conversaId, id, ct); return NoContent(); }
}
