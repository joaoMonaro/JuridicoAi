using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Pecas;

[ApiController]
[Authorize]
public sealed class PecaExportController(PecaExportApplicationService exporter) : ControllerBase
{
    [HttpPost("pecas/{pecaId:guid}/exportar")]
    public async Task<IActionResult> Exportar(Guid pecaId, ExportarPecaRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, private";
        var file = await exporter.ExportarAsync(pecaId, request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
