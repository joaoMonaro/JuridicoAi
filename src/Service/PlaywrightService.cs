using Microsoft.Playwright;

namespace JuridicoAI.Service;

public class PlaywrightService
{
    public async Task<string> PlaywrightCheck(CancellationToken cancellationToken = default)
    {
        var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            ExecutablePath = GetChromiumExecutablePath(),
            Timeout = 15000
        });

        var page = await browser.NewPageAsync();

        await page.GotoAsync("https://google.com", new()
        {
            Timeout = 15000,
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        return "Playwright funcionando!";
    }

    private static string GetChromiumExecutablePath()
    {
        var candidates = new[]
        {
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
            "/snap/bin/chromium"
        };

        var executablePath = candidates.FirstOrDefault(File.Exists);

        if (executablePath is null)
        {
            throw new InvalidOperationException(
                "Chromium não encontrado. Instale os browsers do Playwright ou configure um ExecutablePath válido.");
        }

        return executablePath;
    }
}
