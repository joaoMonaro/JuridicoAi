using System.Text.Encodings.Web;
using System.Text.Json;
using JuridicoAI.Scrapper.Models;
using JuridicoAI.Scrapper.Services;

namespace JuridicoAI.Scrapper.Cli;

internal sealed class ScraperCliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly TjspScraperService _scraper;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public ScraperCliRunner(
        TjspScraperService scraper,
        TextWriter output,
        TextWriter error)
    {
        _scraper = scraper;
        _output = output;
        _error = error;
    }

    public async Task RunAsync(TjspSearchRequest request, CancellationToken cancellationToken)
    {
        _error.WriteLine($"Consultando TJSP. Termo: '{request.Termo}'. Max: {request.QuantidadeMaxima}.");

        var resultado = await _scraper.PesquisarAsync(request, cancellationToken);
        var json = JsonSerializer.Serialize(resultado, JsonOptions);

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            _output.WriteLine(json);
        }
        else
        {
            var outputPath = Path.GetFullPath(request.OutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
            _error.WriteLine($"Resultado salvo em: {outputPath}");
        }

        _error.WriteLine($"Total informado pelo TJSP: {resultado.TotalEncontrado}. Itens retornados: {resultado.QuantidadeRetornada}.");
    }
}
