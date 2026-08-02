using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.Scrapper.Models;
using JuridicoAI.Scrapper.Parsing;
using JuridicoAI.Scrapper.Services;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class ScraperService(IOptions<ScraperSettings> options, ILogger<ScraperService> logger)
{
    private readonly ScraperSettings _settings = options.Value;

    public async Task<ScraperResult> ColetarAsync(
        string termo,
        int? maxResultados,
        CancellationToken cancellationToken)
    {
        var max = Math.Clamp(maxResultados ?? _settings.MaxResults, 1, 200);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.TimeoutSeconds)));

        try
        {
            var scraper = new TjspScraperService(new TjspBrowserSessionProvider(), new TjspResultadoParser());
            var resultado = await scraper.PesquisarAsync(new TjspSearchRequest
            {
                Termo = termo.Trim(),
                QuantidadeMaxima = max,
                TipoDecisao = "A",
                IncluirSinonimos = true,
                SalvarHtmlBruto = true
            }, timeout.Token);

            var coletados = resultado.Resultados.Select(item => new JurisprudenciaRaw
            {
                Fonte = item.Tribunal,
                NumeroProcesso = item.NumeroProcesso,
                Relator = item.Relator,
                OrgaoJulgador = item.OrgaoJulgador,
                DataJulgamento = item.DataJulgamento,
                DataPublicacao = item.DataPublicacao,
                EmentaBruta = item.Ementa,
                Link = item.LinkPdf,
                HtmlBruto = item.ConteudoBruto,
                TermoBuscado = termo.Trim(),
                DataColeta = DateTime.UtcNow
            }).ToList();

            return new ScraperResult(termo.Trim(), coletados);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Timeout ao coletar jurisprudencias no TJSP para o termo {Termo}", termo);
            throw new TimeoutException($"O TJSP nao respondeu em {_settings.TimeoutSeconds} segundos.");
        }
    }
}
