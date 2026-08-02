using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Peticoes;

[ApiController]
[Route("peticoes")]
public class PeticaoController(PeticaoApplicationService peticaoApplicationService) : ControllerBase
{
    [HttpPost("gerar")]
    public async Task<IActionResult> Gerar([FromBody] GerarPeticaoRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.FatoSimples))
        {
            return BadRequest("O campo fatoSimples e obrigatorio.");
        }

        var peticao = await peticaoApplicationService.GerarAsync(request.FatoSimples, cancellationToken);

        return Ok(peticao);
    }
}
