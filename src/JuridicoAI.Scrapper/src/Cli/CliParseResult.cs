using JuridicoAI.Scrapper.Models;

namespace JuridicoAI.Scrapper.Cli;

internal sealed record CliParseResult(bool Success, TjspSearchRequest? Request, string? ErrorMessage)
{
    public static CliParseResult Ok(TjspSearchRequest request) => new(true, request, null);
    public static CliParseResult Fail(string? errorMessage) => new(false, null, errorMessage);
}
