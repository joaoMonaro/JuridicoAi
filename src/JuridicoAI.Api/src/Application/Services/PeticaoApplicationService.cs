using JuridicoAI.Api.Infrastructure.Gateways.OpenIA;

namespace JuridicoAI.Api.Application.Services;

public class PeticaoApplicationService(OpenIAGateway openIAGateway)
{
    public async Task<string> GerarAsync(CancellationToken cancellationToken = default)
    {
        return await openIAGateway.GerarTextoAsync(
            "gere um texto de uma linha lorem ipsum",
            cancellationToken);
    }
}
