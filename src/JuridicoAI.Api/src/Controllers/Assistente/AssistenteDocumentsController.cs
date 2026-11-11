using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Assistente;

[ApiController, Authorize, Route("assistente/v2/conversas/{conversaId:guid}/anexos")]
public sealed class AssistenteDocumentsController(AssistenteDocumentService documents) : ControllerBase
{
    [HttpPost, RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<ActionResult<AssistenteDocumentoDto>> Upload(Guid conversaId, [FromForm] IFormFile arquivo,
        [FromForm] Guid clientUploadId, CancellationToken ct)
    {
        await using var stream = arquivo.OpenReadStream();
        var item = await documents.UploadAsync(conversaId, clientUploadId, arquivo.FileName, stream, ct);
        return AcceptedAtAction(nameof(List), new { conversaId }, item);
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssistenteDocumentoDto>>> List(Guid conversaId, CancellationToken ct) => Ok(await documents.ListAsync(conversaId, ct));
    [HttpGet("{id:guid}/arquivo")]
    public async Task<IActionResult> Download(Guid conversaId, Guid id, CancellationToken ct)
    {
        var file = await documents.DownloadAsync(conversaId, id, ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Stream, file.ContentType, file.Name);
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssistenteDocumentoDto>> Get(Guid conversaId, Guid id, CancellationToken ct) => Ok(await documents.GetAsync(conversaId, id, ct));
    [HttpGet("/assistente/v2/conversas/{conversaId:guid}/fontes/busca")]
    public async Task<ActionResult<IReadOnlyList<AssistenteDocumentoBuscaResultado>>> Search(Guid conversaId, [FromQuery] string consulta,
        [FromQuery] int limite = 12, CancellationToken ct = default) => Ok(await documents.SearchAsync(conversaId, consulta, limite, ct));
    [HttpPost("{id:guid}/reprocessar")]
    public async Task<IActionResult> Retry(Guid conversaId, Guid id, CancellationToken ct)
    { await documents.RetryAsync(conversaId, id, ct); return Accepted(); }
}
