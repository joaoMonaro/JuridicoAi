using JuridicoAI.Models;
using JuridicoAI.Service;
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

    /// <summary>
    /// Gera um rascunho técnico de petição inicial usando jurisprudências enviadas na requisição
    /// e, opcionalmente, jurisprudências pesquisadas no TJSP.
    /// </summary>
    /// <remarks>
    /// Exemplo de request:
    ///
    ///     POST /peticoes/gerar
    ///     {
    ///       "descricaoCaso": "Meu nome foi negativado por uma dívida que eu não reconheço.",
    ///       "termoPesquisaJurisprudencia": "negativação indevida dívida não reconhecida dano moral",
    ///       "quantidadeMaximaJurisprudencias": 10,
    ///       "jurisprudencias": [
    ///         {
    ///           "tribunal": "TJSP",
    ///           "numeroProcesso": "1004176-10.2025.8.26.0590",
    ///           "ementa": "Cobrança. Dano moral não caracterizado. Recurso não provido.",
    ///           "link": "https://esaj.tjsp.jus.br/cjsg/getArquivo.do?cdAcordao=20496515&amp;cdForo=0",
    ///           "relator": "Pedro Baccarat",
    ///           "dataJulgamento": "11/05/2026"
    ///         }
    ///       ]
    ///     }
    /// </remarks>
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
