using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class ReverseProxySettings
{
    public string[] KnownProxies { get; init; } = [];
    public string[] KnownNetworks { get; init; } = [];
    public int ForwardLimit { get; init; } = 1;

    public void ConfigureForwardedHeaders(ForwardedHeadersOptions options)
    {
        if (ForwardLimit < 1)
            throw new InvalidOperationException("ReverseProxy:ForwardLimit deve ser maior que zero.");

        // Substitui os padrões do framework; loopback também exige autorização.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        for (var i = 0; i < KnownProxies.Length; i++)
        {
            if (!IPAddress.TryParse(KnownProxies[i], out var address) ||
                address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException($"ReverseProxy:KnownProxies[{i}] deve ser um endereço IP específico.");
            options.KnownProxies.Add(address);
        }

        for (var i = 0; i < KnownNetworks.Length; i++)
        {
            if (!System.Net.IPNetwork.TryParse(KnownNetworks[i], out var network) || network.PrefixLength == 0)
                throw new InvalidOperationException($"ReverseProxy:KnownNetworks[{i}] deve ser uma rede CIDR específica, com prefixo maior que zero.");
            options.KnownIPNetworks.Add(network);
        }

        options.ForwardLimit = ForwardLimit;
        options.RequireHeaderSymmetry = true;
        // Com ambas as listas vazias, o framework confiaria em qualquer origem
        // se os cabeçalhos fossem habilitados. None mantém o encaminhamento inativo.
        options.ForwardedHeaders = options.KnownProxies.Count + options.KnownIPNetworks.Count == 0
            ? ForwardedHeaders.None
            : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    }
}
