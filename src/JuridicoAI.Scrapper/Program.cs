using JuridicoAI.Scrapper.Cli;
using JuridicoAI.Scrapper.Parsing;
using JuridicoAI.Scrapper.Services;

var parseResult = ScraperCliParser.Parse(args);
if (!parseResult.Success)
{
    if (!string.IsNullOrWhiteSpace(parseResult.ErrorMessage))
    {
        Console.Error.WriteLine(parseResult.ErrorMessage);
    }

    ScraperUsageWriter.Write(Console.Error);
    return ScraperCliParser.IsHelp(args) ? 0 : 1;
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
    var scraper = new TjspScraperService(new TjspBrowserSessionProvider(), new TjspResultadoParser());
    var runner = new ScraperCliRunner(scraper, Console.Out, Console.Error);
    await runner.RunAsync(request, cts.Token);
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
