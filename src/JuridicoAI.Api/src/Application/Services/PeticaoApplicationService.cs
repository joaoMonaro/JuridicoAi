using JuridicoAI.Api.Infrastructure.Gateways.OpenIA;
using JuridicoAI.Api.Infrastructure.Repositories;

namespace JuridicoAI.Api.Application.Services;

public class PeticaoApplicationService(
    OpenIAGateway openIAGateway,
    TjspSearchResultRepository tjspSearchResultRepository)
{
    public async Task<string> GerarAsync(CancellationToken cancellationToken = default)
    {
        var ementas = await tjspSearchResultRepository.ObterEmentasAsync(cancellationToken);
        var contextoEmentas = string.Join(Environment.NewLine, ementas.Select(ementa => $"- {ementa}"));

        return await openIAGateway.GerarTextoAsync(
            $"""
            Sendo um especialista em defesa do consumidor, baseie sua resposta apenas em dados fornecidos.
            Gere uma peticao sousando como base as seguintes ementas do TJSP:
            {contextoEmentas}
            """,
            cancellationToken);
    }
}
