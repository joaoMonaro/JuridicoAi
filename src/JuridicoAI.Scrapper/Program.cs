using System.Text.Encodings.Web;
using System.Text.Json;
using JuridicoAI.Scrapper.Models;
using JuridicoAI.Scrapper.Parsing;
using JuridicoAI.Scrapper.Services;

var parseResult = ParseArguments(args);
if (!parseResult.Success)
{
    if (!string.IsNullOrWhiteSpace(parseResult.ErrorMessage))
    {
        Console.Error.WriteLine(parseResult.ErrorMessage);
    }

    PrintUsage();
    return args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
           args.Contains("-h", StringComparer.OrdinalIgnoreCase)
        ? 0
        : 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

try
{
    var request = parseResult.Request!;
    Console.Error.WriteLine($"Consultando TJSP. Termo: '{request.Termo}'. Max: {request.QuantidadeMaxima}.");

    var scraper = new TjspScraperService(new TjspBrowserSessionProvider(), new TjspResultadoParser());
    var resultado = await scraper.PesquisarAsync(request, cts.Token);
    var json = JsonSerializer.Serialize(resultado, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    if (string.IsNullOrWhiteSpace(request.OutputPath))
    {
        Console.WriteLine(json);
    }
    else
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllTextAsync(request.OutputPath, json, cts.Token);
        Console.Error.WriteLine($"Resultado salvo em: {request.OutputPath}");
    }

    Console.Error.WriteLine($"Total informado pelo TJSP: {resultado.TotalEncontrado}. Itens retornados: {resultado.QuantidadeRetornada}.");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Execucao cancelada.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Falha ao executar scraper do TJSP.");
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static CliParseResult ParseArguments(string[] args)
{
    if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
    {
        return CliParseResult.Fail(args.Length == 0 ? "Informe os argumentos obrigatorios." : null);
    }

    string? termo = null;
    var quantidadeMaxima = 20;
    var tipoDecisao = "A";
    var incluirSinonimos = true;
    var salvarHtmlBruto = false;
    string? outputPath = null;

    try
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--termo":
                case "-t":
                    termo = ReadValue(args, ref i, arg);
                    break;
                case "--max":
                case "-m":
                    var maxValue = ReadValue(args, ref i, arg);
                    if (!int.TryParse(maxValue, out quantidadeMaxima) || quantidadeMaxima <= 0)
                    {
                        return CliParseResult.Fail("O argumento --max deve ser um inteiro maior que zero.");
                    }
                    break;
                case "--tipo-decisao":
                    tipoDecisao = ReadValue(args, ref i, arg).ToUpperInvariant();
                    break;
                case "--sem-sinonimos":
                    incluirSinonimos = false;
                    break;
                case "--raw-html":
                    salvarHtmlBruto = true;
                    break;
                case "--output":
                case "-o":
                    outputPath = ReadValue(args, ref i, arg);
                    break;
                default:
                    return CliParseResult.Fail($"Argumento desconhecido: {arg}");
            }
        }
    }
    catch (ArgumentException ex)
    {
        return CliParseResult.Fail(ex.Message);
    }

    if (string.IsNullOrWhiteSpace(termo))
    {
        return CliParseResult.Fail("O argumento --termo e obrigatorio.");
    }

    if (tipoDecisao is not ("A" or "H" or "D"))
    {
        return CliParseResult.Fail("O argumento --tipo-decisao deve ser A, H ou D.");
    }

    return CliParseResult.Ok(new TjspSearchRequest
    {
        Termo = termo,
        QuantidadeMaxima = quantidadeMaxima,
        TipoDecisao = tipoDecisao,
        IncluirSinonimos = incluirSinonimos,
        SalvarHtmlBruto = salvarHtmlBruto,
        OutputPath = outputPath
    });
}

static string ReadValue(string[] args, ref int index, string argumentName)
{
    if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
    {
        throw new ArgumentException($"O argumento {argumentName} exige um valor.");
    }

    index++;
    return args[index];
}

static void PrintUsage()
{
    Console.Error.WriteLine("Uso:");
    Console.Error.WriteLine("  dotnet run --project src/JuridicoAI.Scrapper -- --termo \"uso indevido\" --max 20 --output resultado.json");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Opcoes:");
    Console.Error.WriteLine("  --termo, -t          Termo de pesquisa livre. Obrigatorio.");
    Console.Error.WriteLine("  --max, -m            Quantidade maxima de resultados. Padrao: 20. Limite interno: 200.");
    Console.Error.WriteLine("  --tipo-decisao       A=acordaos, H=homologacoes, D=decisoes monocraticas. Padrao: A.");
    Console.Error.WriteLine("  --sem-sinonimos      Desativa pesquisa por sinonimos.");
    Console.Error.WriteLine("  --raw-html           Inclui HTML bruto da primeira pagina no JSON.");
    Console.Error.WriteLine("  --output, -o         Caminho para salvar o JSON. Se omitido, escreve no stdout.");
}

internal sealed record CliParseResult(bool Success, TjspSearchRequest? Request, string? ErrorMessage)
{
    public static CliParseResult Ok(TjspSearchRequest request) => new(true, request, null);
    public static CliParseResult Fail(string? errorMessage) => new(false, null, errorMessage);
}
