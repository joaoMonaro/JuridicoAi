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
    private readonly MongoScraperResultRepository _repository;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public ScraperCliRunner(
        TjspScraperService scraper,
        MongoScraperResultRepository repository,
        TextWriter output,
        TextWriter error)
    {
        _scraper = scraper;
        _repository = repository;
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
            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath));
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(request.OutputPath, json, cancellationToken);
            _error.WriteLine($"Resultado salvo em: {request.OutputPath}");
        }

        await _repository.SaveAsync(request, resultado, json, cancellationToken);
        _error.WriteLine("Resultado gravado no MongoDB.");

        _error.WriteLine($"Total informado pelo TJSP: {resultado.TotalEncontrado}. Itens retornados: {resultado.QuantidadeRetornada}.");
    }
}
