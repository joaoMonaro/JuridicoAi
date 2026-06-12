using JuridicoAI.Scrapper.Models;
using JuridicoAI.Scrapper.Parsing;

namespace JuridicoAI.Scrapper.Services;

public sealed class TjspScraperService
{
    private const int ItensPorPagina = 20;

    private readonly TjspBrowserSessionProvider _sessionProvider;
    private readonly TjspResultadoParser _parser;

    public TjspScraperService(TjspBrowserSessionProvider sessionProvider, TjspResultadoParser parser)
    {
        _sessionProvider = sessionProvider;
        _parser = parser;
    }

    public async Task<TjspSearchResult> PesquisarAsync(
        TjspSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Termo))
        {
            throw new ArgumentException("Informe o termo de pesquisa.", nameof(request));
        }

        if (request.QuantidadeMaxima <= 0)
        {
            throw new ArgumentException("A quantidade maxima deve ser maior que zero.", nameof(request));
        }

        var quantidadeMaxima = Math.Min(request.QuantidadeMaxima, 200);
        var session = await _sessionProvider.CriarAsync(cancellationToken);

        using var client = new TjspJurisprudenciaClient(session.Cookies);
        var uuidCaptcha = await client.AtualizarUuidCaptchaAsync(session.UuidCaptcha, cancellationToken);
        var htmlPrimeiraPagina = await client.PesquisarAsync(session, uuidCaptcha, request, cancellationToken);
        var primeiraPagina = _parser.Parse(htmlPrimeiraPagina, quantidadeMaxima);

        if (primeiraPagina.Resultados.Count == 0)
        {
            return new TjspSearchResult
            {
                Termo = request.Termo,
                TotalEncontrado = primeiraPagina.Total,
                QuantidadeRetornada = 0,
                Resultados = [],
                HtmlPrimeiraPagina = request.SalvarHtmlBruto ? htmlPrimeiraPagina : null
            };
        }

        var resultados = primeiraPagina.Resultados.ToList();
        var totalPaginasDisponiveis = primeiraPagina.Total > 0
            ? (int)Math.Ceiling(primeiraPagina.Total / (double)ItensPorPagina)
            : 1;
        var paginasNecessarias = (int)Math.Ceiling(quantidadeMaxima / (double)ItensPorPagina);
        var ultimaPagina = Math.Min(totalPaginasDisponiveis, paginasNecessarias);

        for (var pagina = 2; pagina <= ultimaPagina && resultados.Count < quantidadeMaxima; pagina++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var htmlPagina = await client.ObterPaginaAsync(request.TipoDecisao, pagina, cancellationToken);
            var resultadosPagina = _parser.Parse(htmlPagina, quantidadeMaxima - resultados.Count);
            resultados.AddRange(resultadosPagina.Resultados);
        }

        return new TjspSearchResult
        {
            Termo = request.Termo,
            TotalEncontrado = primeiraPagina.Total,
            QuantidadeRetornada = resultados.Count,
            Resultados = resultados.Take(quantidadeMaxima).ToList(),
            HtmlPrimeiraPagina = request.SalvarHtmlBruto ? htmlPrimeiraPagina : null
        };
    }
}
