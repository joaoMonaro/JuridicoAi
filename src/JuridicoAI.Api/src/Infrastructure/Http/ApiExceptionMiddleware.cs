using JuridicoAI.Api.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace JuridicoAI.Api.Infrastructure.Http;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (ApiException ex)
        {
            await WriteProblemAsync(context, ex.StatusCode, ex.Message,
                ex switch
                {
                    ConflitoDeVersaoException conflito => conflito.Code,
                    ConflitoException conflito => conflito.Code,
                    AssistenteIaException assistente => assistente.Code,
                    _ => null
                },
                ex is AssistenteIaException ai ? ai.Retryable : null);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            var isAiRequest = context.Request.Path.Value?.Contains(
                "/assistente/solicitacoes",
                StringComparison.OrdinalIgnoreCase) == true;
            await WriteProblemAsync(
                context,
                ex.StatusCode,
                isAiRequest ? "A solicitação de IA excede o limite permitido." : "O arquivo excede o limite permitido.",
                isAiRequest ? "AI_REQUEST_TOO_LARGE" : null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro inesperado ao processar {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Ocorreu um erro inesperado.");
        }
    }

    private static Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string detail,
        string? code = null,
        bool? retryable = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = detail,
            Instance = context.Request.Path
        };
        if (code is not null) problem.Extensions["code"] = code;
        if (retryable.HasValue) problem.Extensions["retryable"] = retryable.Value;
        return context.Response.WriteAsJsonAsync(problem);
    }
}
