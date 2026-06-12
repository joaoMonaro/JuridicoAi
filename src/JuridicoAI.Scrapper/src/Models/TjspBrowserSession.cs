using System.Net;

namespace JuridicoAI.Scrapper.Models;

public sealed class TjspBrowserSession
{
    public required CookieContainer Cookies { get; init; }
    public required string UuidCaptcha { get; init; }
    public required string RecaptchaToken { get; init; }
}
