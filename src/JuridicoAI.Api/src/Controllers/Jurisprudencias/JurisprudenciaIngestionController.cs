using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Jurisprudencias;

[ApiController]
[Route("api/jurisprudencias")]
public sealed class JurisprudenciaIngestionController(
    JurisprudenciaIngestionService ingestionService,
    JurisprudenciaRepository repository,
    JurisprudenciaRawRepository rawRepository) : ControllerBase
{
    [HttpPost("ingest")]
    public async Task<ActionResult<IngestionSummary>> Ingerir(
        [FromBody] IngestJurisprudenciasRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await ingestionService.IngerirAsync(request.Termo, request.MaxResultados, cancellationToken));
        }
        catch (TimeoutException ex)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { erro = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Consultar(
        [FromQuery] string? tema, [FromQuery] string? subtema, [FromQuery] string? resultado,
        [FromQuery] bool? danoMoral, [FromQuery] string? fonte, [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await repository.ConsultarAsync(
            new JurisprudenciaQuery(tema, subtema, resultado, danoMoral, fonte, limite), cancellationToken));

    [HttpGet("raw")]
    public async Task<IActionResult> ConsultarRaw([FromQuery] int limite = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await rawRepository.ListarAsync(limite, cancellationToken));

    [HttpPost("reprocess")]
    public async Task<ActionResult<ReprocessSummary>> Reprocessar([FromQuery] string? termo,
        [FromQuery] int limite = 500, CancellationToken cancellationToken = default) =>
        Ok(await ingestionService.ReprocessarAsync(termo, Math.Clamp(limite, 1, 1000), cancellationToken));
}
