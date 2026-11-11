using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Assistente;

[ApiController, Authorize, Route("assistente/v2")]
public sealed class AssistenteChatController(AssistenteChatService chat, AssistenteIaApplicationService generator, AssistenteWorkspaceService workspace,
    ILogger<AssistenteChatController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    [HttpPost("conversas/{conversaId:guid}/mensagens/{mensagemId:guid}/rejeicao")]
    public async Task<IActionResult> Rejeitar(Guid conversaId, Guid mensagemId, CancellationToken ct)
    { await chat.RejeitarRespostaAsync(conversaId, mensagemId, ct); return NoContent(); }

    [HttpPost("conversas/{conversaId:guid}/mensagens")]
    public Task Enviar(Guid conversaId, EnviarMensagemChatRequest body, CancellationToken ct) => Executar(conversaId, body.Solicitar(), false, ct);

    [HttpPost("conversas/{conversaId:guid}/interacoes")]
    public Task Conversar(Guid conversaId, EnviarInteracaoLivreRequest body, CancellationToken ct) => Executar(conversaId, body.Solicitar(), true, ct);

    private async Task Executar(Guid conversaId, CriarSolicitacaoAssistenteRequest request, bool livre, CancellationToken ct)
    {
        generator.ValidarEntrada(conversaId, request);
        var start = await chat.IniciarAsync(conversaId, request, ct);
        if (!start.Novo && start.Resposta.Estado != "Concluida")
            throw new AssistenteIaException(409, start.Resposta.Estado == "Gerando" ? "AI_CONVERSATION_BUSY" : "AI_REQUEST_TERMINAL",
                start.Resposta.Estado == "Gerando" ? "Este pedido ainda está em andamento. Consulte o histórico." : "Este pedido foi interrompido ou falhou. O histórico foi preservado; um novo envio cria outro pedido.");
        AssistenteIaExecucao? execution = null;
        var sequence = 0;
        try
        {
            AssistenteSolicitacaoConcluidaDto? result = AssistenteChatService.Read<AssistenteSolicitacaoConcluidaDto>(start.Resposta.Resultado);
            if (start.Novo) execution = await generator.PrepararAsync(start.PecaId, request, ct, start.Contexto, conversaLivre: livre);
            else if (result?.Proposta is { } stored) result = result with { Proposta = await workspace.PropostaAsync(stored.Id, ct) };
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers.CacheControl = "no-store";
            Response.Headers["X-Accel-Buffering"] = "no";
            await Event("solicitacao.iniciada", new AssistenteSolicitacaoIniciadaDto(start.Resposta.Id, request.ClientRequestId,
                execution?.CorrelationId ?? Guid.NewGuid()));
            await Response.Body.FlushAsync(ct);
            if (execution is not null)
            {
                result = await generator.GerarAsync(execution, ct);
                result = result with { Sugestao = result.Sugestao with { MensagemUsuarioId = start.Pedido.Id } };
                // Publish to the database before acknowledging completion to the browser.
                if (livre) result = await chat.ConcluirLivreAsync(start, result, ct);
                else await chat.ConcluirAsync(start, result, ct);
            }
            if (result is null) throw new InvalidOperationException("Resultado persistido indisponível.");
            if (!string.IsNullOrEmpty(result.Sugestao.Texto)) await Event("conteudo.delta", new { delta = result.Sugestao.Texto });
            await Event("solicitacao.concluida", result);
            await Response.Body.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            var cancelled = ex is OperationCanceledException && ct.IsCancellationRequested;
            var code = cancelled ? "AI_CANCELLED" : ex is AssistenteIaException ai ? ai.Code : "AI_PROVIDER_ERROR";
            if (start.Novo)
            {
                try { await chat.FalharAsync(start, code, cancelled); }
                catch (Exception) { logger.LogError("Não foi possível registrar estado terminal do chat. MensagemId={MensagemId}", start.Pedido.Id); }
            }
            if (cancelled) return;
            if (!Response.HasStarted) throw;
            if (!ct.IsCancellationRequested)
            {
                await Event("solicitacao.falhou", new AssistenteSolicitacaoFalhouDto(code,
                    ex is AssistenteIaException error ? error.Message : "Não foi possível concluir o pedido. A mensagem permanece no histórico.",
                    ex is AssistenteIaException failure ? failure.Retryable : true, HttpContext.TraceIdentifier));
                await Response.Body.FlushAsync(ct);
            }
        }
        finally { if (execution is not null) await generator.FinalizarAsync(execution); }

        Task Event(string name, object payload) => Response.WriteAsync(
            $"id: {start.Resposta.Id:D}:{++sequence}\nevent: {name}\ndata: {JsonSerializer.Serialize(payload, JsonOptions)}\n\n", ct);
    }
}
