using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Casos;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("casos")]
public sealed class CasosController(CasoApplicationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Caso>> CriarCaso([FromBody] CriarCasoRequest request, CancellationToken ct)
    {
        var caso = await service.CriarCasoAsync(request, ct);
        return CreatedAtAction(nameof(ObterCaso), new { id = caso.Id }, caso);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Caso>>> ListarCasos([FromQuery] CasoStatus? status, CancellationToken ct) => Ok(await service.ListarCasosAsync(status, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Caso>> ObterCaso(Guid id, CancellationToken ct) => Ok(await service.ObterCasoAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Caso>> AtualizarCaso(Guid id, [FromBody] AtualizarCasoRequest request, CancellationToken ct) => Ok(await service.AtualizarCasoAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> ArquivarCaso(Guid id, CancellationToken ct) { await service.ArquivarCasoAsync(id, ct); return NoContent(); }

    [HttpPost("{casoId:guid}/envolvidos")]
    public async Task<ActionResult<EnvolvidoCaso>> CriarEnvolvido(Guid casoId, [FromBody] CriarEnvolvidoCasoRequest request, CancellationToken ct)
    {
        var item = await service.CriarEnvolvidoAsync(casoId, request, ct);
        return CreatedAtAction(nameof(ObterEnvolvido), new { casoId, id = item.Id }, item);
    }

    [HttpGet("{casoId:guid}/envolvidos")]
    public async Task<ActionResult<IReadOnlyList<EnvolvidoCaso>>> ListarEnvolvidos(Guid casoId, CancellationToken ct) => Ok(await service.ListarEnvolvidosAsync(casoId, ct));

    [HttpGet("{casoId:guid}/envolvidos/{id:guid}")]
    public async Task<ActionResult<EnvolvidoCaso>> ObterEnvolvido(Guid casoId, Guid id, CancellationToken ct) => Ok(await service.ObterEnvolvidoAsync(casoId, id, ct));

    [HttpPut("{casoId:guid}/envolvidos/{id:guid}")]
    public async Task<ActionResult<EnvolvidoCaso>> AtualizarEnvolvido(Guid casoId, Guid id, [FromBody] AtualizarEnvolvidoCasoRequest request, CancellationToken ct) => Ok(await service.AtualizarEnvolvidoAsync(casoId, id, request, ct));

    [HttpDelete("{casoId:guid}/envolvidos/{id:guid}")]
    public async Task<IActionResult> RemoverEnvolvido(Guid casoId, Guid id, CancellationToken ct) { await service.RemoverEnvolvidoAsync(casoId, id, ct); return NoContent(); }

    [HttpPost("{casoId:guid}/contextos")]
    public async Task<ActionResult<ContextoCaso>> CriarContexto(Guid casoId, [FromBody] CriarContextoCasoRequest request, CancellationToken ct)
    {
        var item = await service.CriarContextoAsync(casoId, request, ct);
        return CreatedAtAction(nameof(ObterContexto), new { casoId, id = item.Id }, item);
    }

    [HttpGet("{casoId:guid}/contextos")]
    public async Task<ActionResult<IReadOnlyList<ContextoCaso>>> ListarContextos(Guid casoId, CancellationToken ct) => Ok(await service.ListarContextosAsync(casoId, ct));

    [HttpGet("{casoId:guid}/contextos/{id:guid}")]
    public async Task<ActionResult<ContextoCaso>> ObterContexto(Guid casoId, Guid id, CancellationToken ct) => Ok(await service.ObterContextoAsync(casoId, id, ct));

    [HttpPut("{casoId:guid}/contextos/{id:guid}")]
    public async Task<ActionResult<ContextoCaso>> AtualizarContexto(Guid casoId, Guid id, [FromBody] AtualizarContextoCasoRequest request, CancellationToken ct) => Ok(await service.AtualizarContextoAsync(casoId, id, request, ct));

    [HttpDelete("{casoId:guid}/contextos/{id:guid}")]
    public async Task<IActionResult> RemoverContexto(Guid casoId, Guid id, CancellationToken ct) { await service.RemoverContextoAsync(casoId, id, ct); return NoContent(); }

    [HttpPost("{casoId:guid}/documentos")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentoCaso>> CriarDocumento(Guid casoId, [FromForm] IFormFile? arquivo, [FromForm] string? tipo, CancellationToken ct)
    {
        if (arquivo is null) throw new RequisicaoInvalidaException("O campo arquivo e obrigatorio.");
        await using var stream = arquivo.OpenReadStream();
        var item = await service.CriarDocumentoAsync(casoId, arquivo.FileName, arquivo.ContentType, arquivo.Length, tipo, stream, ct);
        return CreatedAtAction(nameof(ObterDocumento), new { casoId, id = item.Id }, item);
    }

    [HttpGet("{casoId:guid}/documentos")]
    public async Task<ActionResult<IReadOnlyList<DocumentoCaso>>> ListarDocumentos(Guid casoId, CancellationToken ct) => Ok(await service.ListarDocumentosAsync(casoId, ct));

    [HttpGet("{casoId:guid}/documentos/{id:guid}")]
    public async Task<ActionResult<DocumentoCaso>> ObterDocumento(Guid casoId, Guid id, CancellationToken ct) => Ok(await service.ObterDocumentoAsync(casoId, id, ct));

    [HttpGet("{casoId:guid}/documentos/{id:guid}/arquivo")]
    public async Task<IActionResult> ObterArquivo(Guid casoId, Guid id, CancellationToken ct)
    {
        var arquivo = await service.ObterArquivoAsync(casoId, id, ct);
        return File(arquivo.Conteudo, arquivo.Documento.MimeType, arquivo.Documento.NomeOriginal, enableRangeProcessing: true);
    }

    [HttpDelete("{casoId:guid}/documentos/{id:guid}")]
    public async Task<IActionResult> RemoverDocumento(Guid casoId, Guid id, CancellationToken ct) { await service.RemoverDocumentoAsync(casoId, id, ct); return NoContent(); }

    [HttpGet("{id:guid}/contexto-completo")]
    public async Task<ActionResult<CasoContextoCompletoDto>> ObterContextoCompleto(Guid id, CancellationToken ct) => Ok(await service.ObterContextoCompletoAsync(id, ct));
}
