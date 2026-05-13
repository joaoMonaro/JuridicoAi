using JuridicoAI.Models;
using JuridicoAI.Service;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class JurisprudenciaController : ControllerBase
{
    private readonly TJSPJurisprudenciaService _tjspJurisprudenciaService;

    public JurisprudenciaController(TJSPJurisprudenciaService tjspJurisprudenciaService)
    {
        _tjspJurisprudenciaService = tjspJurisprudenciaService;
    }

    [HttpPost("tjsp")]
    [ProducesResponseType(typeof(IReadOnlyList<JurisprudenciaResultado>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<JurisprudenciaResultado>>> PesquisarTJSP(
        [FromBody] PesquisaJurisprudenciaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Termo))
        {
            ModelState.AddModelError(nameof(request.Termo), "Informe um termo de pesquisa.");
            return ValidationProblem(ModelState);
        }

        var quantidadeMaxima = request.QuantidadeMaxima is > 0 and <= 50
            ? request.QuantidadeMaxima.Value
            : 10;

        var resultados = await _tjspJurisprudenciaService.PesquisarAsync(
            request.Termo,
            quantidadeMaxima,
            cancellationToken);

        return Ok(resultados);
    }
}

public sealed class PesquisaJurisprudenciaRequest
{
    public string Termo { get; init; } = string.Empty;
    public int? QuantidadeMaxima { get; init; }
}
