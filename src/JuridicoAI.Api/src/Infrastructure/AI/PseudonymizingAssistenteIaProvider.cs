using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.AI;

public sealed class PseudonymizingAssistenteIaProvider(
    IAssistenteIaProvider innerProvider,
    IAssistenteTokenCounter innerCounter,
    IOptionsMonitor<AssistenteIaSettings> options) : IAssistenteIaProvider, IAssistenteTokenCounter
{
    public Task<int> ContarTokensEntradaAsync(
        AssistenteIaProviderRequest request,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.PiiPseudonymizationEnabled)
            return innerCounter.ContarTokensEntradaAsync(request, cancellationToken);

        var session = new AssistentePiiPseudonymizationSession();
        var sanitized = PrepareRequest(request, session);
        return innerCounter.ContarTokensEntradaAsync(sanitized, cancellationToken);
    }

    public async Task<AssistenteIaProviderResult> GerarAsync(
        AssistenteIaProviderRequest request,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.PiiPseudonymizationEnabled)
            return await innerProvider.GerarAsync(request, cancellationToken);

        var session = new AssistentePiiPseudonymizationSession();
        var sanitized = PrepareRequest(request, session);
        var tools = sanitized.FerramentasDocumentais;

        using var scope = tools?.UseExecutionInterceptor(async (next, name, arguments, ct) =>
        {
            var rehydratedArgs = session.Rehydrate(arguments);
            var rawOutput = await next(name, rehydratedArgs, ct);
            return session.Pseudonymize(rawOutput);
        });

        var result = await innerProvider.GerarAsync(sanitized, cancellationToken);
        return RehydrateResult(result, session);
    }

    private static AssistenteIaProviderRequest PrepareRequest(
        AssistenteIaProviderRequest request,
        AssistentePiiPseudonymizationSession session)
    {
        var pseudonymizedInput = session.Pseudonymize(request.Input);
        var instructions = string.IsNullOrWhiteSpace(request.Instructions)
            ? AssistentePiiPseudonymizationSession.ProviderInstruction
            : request.Instructions + "\n" + AssistentePiiPseudonymizationSession.ProviderInstruction;

        return request with
        {
            Instructions = instructions,
            Input = pseudonymizedInput
        };
    }

    private static AssistenteIaProviderResult RehydrateResult(
        AssistenteIaProviderResult result,
        AssistentePiiPseudonymizationSession session)
    {
        if (!session.HasReplacements) return result;

        var alertas = result.Alertas
            .Select(a => a with { Mensagem = session.Rehydrate(a.Mensagem) })
            .ToArray();

        var achados = result.Achados
            .Select(a => a with
            {
                Titulo = session.Rehydrate(a.Titulo),
                Descricao = session.Rehydrate(a.Descricao),
                Alertas = a.Alertas.Select(al => al with { Mensagem = session.Rehydrate(al.Mensagem) }).ToArray()
            })
            .ToArray();

        var rascunho = result.RascunhoAutomatico is null
            ? null
            : result.RascunhoAutomatico with
            {
                Grupos = result.RascunhoAutomatico.Grupos
                    .Select(g => g with
                    {
                        Explicacao = session.Rehydrate(g.Explicacao),
                        Operacoes = g.Operacoes
                            .Select(op => op with
                            {
                                NovosBlocos = op.NovosBlocos.Select(session.RehydrateJson).ToArray()
                            })
                            .ToArray()
                    })
                    .ToArray()
            };

        return result with
        {
            Titulo = session.Rehydrate(result.Titulo),
            Texto = session.Rehydrate(result.Texto),
            Resumo = session.Rehydrate(result.Resumo),
            Alertas = alertas,
            Achados = achados,
            RascunhoAutomatico = rascunho
        };
    }
}
