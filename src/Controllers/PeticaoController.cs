using JuridicoAI.Application.DTOs;
using JuridicoAI.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Controllers;

[ApiController]
[Route("peticoes")]
public class PeticaoController : ControllerBase
{
    private readonly PeticaoService _peticaoService;
    private readonly ILogger<PeticaoController> _logger;

    public PeticaoController(
        PeticaoService peticaoService,
        ILogger<PeticaoController> logger)
    {
        _peticaoService = peticaoService;
        _logger = logger;
    }

    [HttpPost("gerar")]
    [ProducesResponseType(typeof(GerarPeticaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GerarPeticaoResponse>> Gerar(
        [FromBody] GerarPeticaoRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DescricaoCaso))
        {
            ModelState.AddModelError(nameof(request.DescricaoCaso), "Informe a descrição do caso.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var resultado = await _peticaoService.GerarAsync(request, cancellationToken);
            return Ok(resultado);
        }
        catch (OperationCanceledException)
        {
            throw;
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
