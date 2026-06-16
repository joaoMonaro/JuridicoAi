using JuridicoAI.Application.DTOs;
using JuridicoAI.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Controllers;

[ApiController]
[Route("peticoes")]
public class PeticaoController_old : ControllerBase
{
    private readonly PeticaoService_old _peticaoServiceOld;
    private readonly ILogger<PeticaoController_old> _logger;

    public PeticaoController_old(
        PeticaoService_old peticaoServiceOld,
        ILogger<PeticaoController_old> logger)
    {
        _peticaoServiceOld = peticaoServiceOld;
        _logger = logger;
    }

    [HttpPost("gerar")]
    [ProducesResponseType(typeof(GerarPeticaoResponse_old), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GerarPeticaoResponse_old>> Gerar(
        [FromBody] GerarPeticaoRequest_old requestOld,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestOld.DescricaoCaso))
        {
            ModelState.AddModelError(nameof(requestOld.DescricaoCaso), "Informe a descrição do caso.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var resultado = await _peticaoServiceOld.GerarAsync(requestOld, cancellationToken);
            return Ok(resultado);
        }
        catch (PeticaoGenerationException ex)
        {
            _logger.LogWarning(ex, "Falha controlada ao gerar petição.");

            return Problem(
                title: "Não foi possível gerar a petição.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
