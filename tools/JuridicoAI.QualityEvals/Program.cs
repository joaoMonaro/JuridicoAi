using System.Reflection;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.QualityEvals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "validate";
var repositoryRoot = FindRepositoryRoot();
var suitePath = GetArgument(args, "--suite") ??
    Path.Combine(repositoryRoot, "avaliacoes", "qualidade-juridica");
var suite = await EvaluationSuiteLoader.LoadAsync(suitePath);
var validationErrors = EvaluationSuiteLoader.Validate(suite);
if (validationErrors.Count > 0)
{
    foreach (var error in validationErrors) Console.Error.WriteLine($"ERRO: {error}");
    return 2;
}

if (command == "validate")
{
    Console.WriteLine($"Suíte {suite.Definition.SuiteVersion} válida: {suite.Cases.Count} casos, 5 por categoria.");
    return 0;
}

if (command != "run" || !args.Contains("--confirm-real-run", StringComparer.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Para executar chamadas reais, use: run --confirm-real-run [--case ID]");
    return 2;
}

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .Build();
var settings = configuration.GetSection("AssistenteIa").Get<AssistenteIaSettings>() ?? new();
if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model))
{
    Console.Error.WriteLine("Configure AssistenteIa:ApiKey e AssistenteIa:Model por user-secrets ou variável de ambiente.");
    return 2;
}

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/"),
    Timeout = Timeout.InfiniteTimeSpan
};
var provider = new OpenAiAssistenteIaProvider(httpClient, Options.Create(settings));
var runner = new EvaluationRunner(provider, new AssistentePromptPolicy(), settings);
var outputRoot = Path.Combine(suitePath, "execucoes");
var output = await runner.RunAsync(suite, outputRoot, GetArgument(args, "--case"), default);
Console.WriteLine($"Execução concluída. Artefatos: {output}");
return 0;

static string? GetArgument(string[] arguments, string name)
{
    var index = Array.FindIndex(arguments, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "JuridicoAI.sln"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Execute o avaliador dentro do repositório JuridicoAI ou informe --suite.");
}
