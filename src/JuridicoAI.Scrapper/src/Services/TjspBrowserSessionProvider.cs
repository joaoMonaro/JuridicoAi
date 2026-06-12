using System.Net;
using JuridicoAI.Scrapper.Models;
using Microsoft.Playwright;

namespace JuridicoAI.Scrapper.Services;

public sealed class TjspBrowserSessionProvider
{
    private const string BaseUrl = "https://esaj.tjsp.jus.br";
    private const string ConsultaCompletaUrl = BaseUrl + "/cjsg/consultaCompleta.do";
    private const string RecaptchaSiteKey = "6LcXJIAbAAAAAOwprTGEEYwRSe-HMYD-Ys0pSR6f";
    private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 " +
                                     "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private const float TimeoutMs = 30000;

    public async Task<TjspBrowserSession> CriarAsync(CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync().WaitAsync(cancellationToken);
        await using var browser = await playwright.Chromium.LaunchAsync(CriarBrowserOptions()).WaitAsync(cancellationToken);
        var context = await browser.NewContextAsync(new()
        {
            Locale = "pt-BR",
            UserAgent = UserAgent
        }).WaitAsync(cancellationToken);

        try
        {
            var page = await context.NewPageAsync().WaitAsync(cancellationToken);
            page.SetDefaultTimeout(TimeoutMs);
            page.SetDefaultNavigationTimeout(TimeoutMs);

            await page.GotoAsync(ConsultaCompletaUrl, new()
            {
                Timeout = TimeoutMs,
                WaitUntil = WaitUntilState.DOMContentLoaded
            }).WaitAsync(cancellationToken);

            await page.WaitForSelectorAsync("#uuidCaptcha", new()
            {
                Timeout = TimeoutMs,
                State = WaitForSelectorState.Attached
            }).WaitAsync(cancellationToken);

            await page.WaitForFunctionAsync(
                "() => window.grecaptcha && typeof window.grecaptcha.execute === 'function'",
                null,
                new() { Timeout = TimeoutMs }).WaitAsync(cancellationToken);

            var uuidCaptcha = await page.Locator("#uuidCaptcha")
                .GetAttributeAsync("value")
                .WaitAsync(cancellationToken) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(uuidCaptcha))
            {
                throw new InvalidOperationException("Nao foi possivel obter uuidCaptcha na pagina do TJSP.");
            }

            var recaptchaToken = await page.EvaluateAsync<string>(
                "async ([siteKey]) => await window.grecaptcha.execute(siteKey, { action: 'consulta' })",
                new[] { RecaptchaSiteKey }).WaitAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(recaptchaToken))
            {
                throw new InvalidOperationException("Nao foi possivel obter recaptcha_response_token na pagina do TJSP.");
            }

            var cookies = await context.CookiesAsync(BaseUrl).WaitAsync(cancellationToken);
            return new TjspBrowserSession
            {
                Cookies = CriarCookieContainer(cookies),
                UuidCaptcha = uuidCaptcha,
                RecaptchaToken = recaptchaToken
            };
        }
        finally
        {
            await context.CloseAsync().WaitAsync(cancellationToken);
        }
    }

    private static CookieContainer CriarCookieContainer(IEnumerable<BrowserContextCookiesResult> cookies)
    {
        var container = new CookieContainer();

        foreach (var cookie in cookies)
        {
            var domain = cookie.Domain.TrimStart('.');
            var uri = new Uri($"https://{domain}");
            container.Add(uri, new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
        }

        return container;
    }

    private static BrowserTypeLaunchOptions CriarBrowserOptions()
    {
        var options = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Timeout = TimeoutMs
        };

        var executablePath = ObterChromiumInstalado();
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            options.ExecutablePath = executablePath;
        }

        return options;
    }

    private static string? ObterChromiumInstalado()
    {
        var candidates = new[]
        {
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
            "/snap/bin/chromium",
            "/usr/bin/google-chrome",
            "/usr/bin/google-chrome-stable"
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
