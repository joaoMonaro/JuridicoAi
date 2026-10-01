using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Assistente;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class AssistenteController(
    AssistenteIaApplicationService service,
    IAssistenteTransparencyService transparencyService,
    ILogger<AssistenteController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [HttpGet("assistente/capacidades")]
    public async Task<ActionResult<AssistenteCapacidadesDto>> ObterCapacidades(CancellationToken cancellationToken) =>
        Ok(await service.ObterCapacidadesAsync(cancellationToken));

    [HttpGet("assistente/transparencia")]
    public async Task<ActionResult<AssistenteTransparenciaDto>> ObterTransparencia(
        CancellationToken cancellationToken) =>
        Ok(await transparencyService.ObterAsync(cancellationToken));

    [HttpPost("assistente/transparencia/confirmacoes")]
    public async Task<ActionResult<AssistenteTransparenciaDto>> ConfirmarTransparencia(
        ConfirmarAssistenteTransparenciaRequest request,
        CancellationToken cancellationToken) =>
        Ok(await transparencyService.ConfirmarAsync(request, cancellationToken));

    [HttpPost("pecas/{pecaId:guid}/assistente/solicitacoes")]
    public async Task Solicitar(
        Guid pecaId,
        CriarSolicitacaoAssistenteRequest request,
        CancellationToken cancellationToken)
    {
        var execution = await service.PrepararAsync(pecaId, request, cancellationToken);
        var sequence = 0;
        try
        {
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers.CacheControl = "no-cache, no-store";
            Response.Headers["X-Accel-Buffering"] = "no";
            await WriteEventAsync(
                "solicitacao.iniciada",
                new AssistenteSolicitacaoIniciadaDto(
                    execution.SolicitacaoId,
                    execution.ClientRequestId,
                    execution.CorrelationId),
                execution.SolicitacaoId,
                ++sequence,
                cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            var result = await service.GerarAsync(execution, cancellationToken);
            if (!string.IsNullOrEmpty(result.Sugestao.Texto))
                await WriteEventAsync(
                    "conteudo.delta",
                    new { delta = result.Sugestao.Texto },
                    execution.SolicitacaoId,
                    ++sequence,
                    cancellationToken);
            await WriteEventAsync(
                "solicitacao.concluida",
                result,
                execution.SolicitacaoId,
                ++sequence,
                cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Solicitação de IA cancelada pelo cliente. SolicitacaoId={SolicitacaoId} PecaId={PecaId}",
                execution.SolicitacaoId,
                execution.PecaId);
        }
        catch (AssistenteIaException ex)
        {
            logger.LogWarning(
                "Solicitação de IA falhou após início do stream. SolicitacaoId={SolicitacaoId} PecaId={PecaId} Code={Code} Retryable={Retryable}",
                execution.SolicitacaoId,
                execution.PecaId,
                ex.Code,
                ex.Retryable);
            await WriteFailureAsync(
                ex.Code,
                ex.Message,
                ex.Retryable,
                execution.SolicitacaoId,
                ++sequence,
                cancellationToken);
        }
        catch (Exception)
        {
            logger.LogError(
                "Erro inesperado após início do stream de IA. SolicitacaoId={SolicitacaoId} PecaId={PecaId}",
                execution.SolicitacaoId,
                execution.PecaId);
            await WriteFailureAsync(
                "AI_PROVIDER_ERROR",
                "Não foi possível concluir a solicitação de IA.",
                retryable: true,
                execution.SolicitacaoId,
                ++sequence,
                cancellationToken);
        }
        finally
        {
            await service.FinalizarAsync(execution);
        }
    }

    private async Task WriteFailureAsync(
        string code,
        string message,
        bool retryable,
        Guid solicitacaoId,
        int sequence,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;
        await WriteEventAsync(
            "solicitacao.falhou",
            new AssistenteSolicitacaoFalhouDto(code, message, retryable, HttpContext.TraceIdentifier),
            solicitacaoId,
            sequence,
            cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteEventAsync(
        string eventName,
        object data,
        Guid solicitacaoId,
        int sequence,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await Response.WriteAsync(
            $"id: {solicitacaoId:D}:{sequence}\nevent: {eventName}\ndata: {json}\n\n",
            cancellationToken);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
