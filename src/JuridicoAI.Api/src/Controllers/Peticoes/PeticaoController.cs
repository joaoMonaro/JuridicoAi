using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Peticoes;

[ApiController]
[Route("peticoes")]
public class PeticaoController(PeticaoApplicationService peticaoApplicationService) : ControllerBase
{
    [HttpPost("gerar")]
    public async Task<IActionResult> Gerar(CancellationToken cancellationToken)
    {
        var peticao = await peticaoApplicationService.GerarAsync(cancellationToken);

        return Ok(peticao);
    }
}
