using System.Net;
using System.Text.RegularExpressions;
using JuridicoAI.Models;
using Microsoft.Playwright;

namespace JuridicoAI.Service;

public sealed class TJSPJurisprudenciaService : IJurisprudenciaProvider
{
    private const string TribunalNome = "TJSP";
    private const string ConsultaCompletaUrl = "https://esaj.tjsp.jus.br/cjsg/consultaCompleta.do";
    private const string BaseUrl = "https://esaj.tjsp.jus.br";
    private const float TimeoutMs = 30000;

    private readonly ILogger<TJSPJurisprudenciaService> _logger;

    public TJSPJurisprudenciaService(ILogger<TJSPJurisprudenciaService> logger)
    {
        _logger = logger;
    }

    public string Tribunal => TribunalNome;

    public async Task<IReadOnlyList<JurisprudenciaResultado>> PesquisarAsync(
        string termo,
        int quantidadeMaxima = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            throw new ArgumentException("O termo de pesquisa deve ser informado.", nameof(termo));
        }

        if (quantidadeMaxima <= 0)
        {
            return [];
        }

        _logger.LogInformation("Iniciando pesquisa de jurisprudencia no TJSP. Termo: {Termo}", termo);

        try
        {
            using var playwright = await Playwright.CreateAsync().WaitAsync(cancellationToken);

            await using var browser = await playwright.Chromium.LaunchAsync(new()
            {
                Headless = true,
                ExecutablePath = GetChromiumExecutablePath(),
                Timeout = TimeoutMs
            }).WaitAsync(cancellationToken);

            var page = await browser.NewPageAsync(new()
            {
                Locale = "pt-BR",
                UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 " +
                            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            }).WaitAsync(cancellationToken);

            page.SetDefaultTimeout(TimeoutMs);
            page.SetDefaultNavigationTimeout(TimeoutMs);

            await AbrirPortalAsync(page, cancellationToken);
            await ExecutarPesquisaAsync(page, termo, cancellationToken);

            var resultados = await ExtrairResultadosAsync(page, quantidadeMaxima, cancellationToken);

            _logger.LogInformation(
                "Pesquisa de jurisprudencia no TJSP finalizada. Termo: {Termo}. Resultados: {Quantidade}",
                termo,
                resultados.Count);

            return resultados;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout ao pesquisar jurisprudencia no TJSP. Termo: {Termo}", termo);
            return [];
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Timeout ao pesquisar jurisprudencia no TJSP. Termo: {Termo}", termo);
            return [];
        }
    }

    private static async Task AbrirPortalAsync(IPage page, CancellationToken cancellationToken)
    {
         await page.GotoAsync(ConsultaCompletaUrl, new()
        {
            Timeout = TimeoutMs,
            WaitUntil = WaitUntilState.DOMContentLoaded
        }).WaitAsync(cancellationToken);

        await page.WaitForSelectorAsync("input[name='dados.buscaInteiroTeor']", new()
        {
            Timeout = TimeoutMs,
            State = WaitForSelectorState.Visible
        }).WaitAsync(cancellationToken);
    }

    private async Task ExecutarPesquisaAsync(IPage page, string termo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Preenchendo formulario de pesquisa do TJSP.");

        var campoPesquisaLivre = page.Locator("input[name='dados.buscaInteiroTeor']").First;
        await campoPesquisaLivre.FillAsync(termo).WaitAsync(cancellationToken);

        await SelecionarAcordaosQuandoDisponivelAsync(page, cancellationToken);

        _logger.LogInformation("Enviando pesquisa ao TJSP.");

        var botaoPesquisar = page
            .Locator("input[type='submit'][value*='Pesquisar'], input[value='Pesquisar'], button[type='submit']")
            .First;

        if (await botaoPesquisar.CountAsync().WaitAsync(cancellationToken) > 0)
        {
            await botaoPesquisar.ClickAsync().WaitAsync(cancellationToken);
        }
        else
        {
            await campoPesquisaLivre.PressAsync("Enter").WaitAsync(cancellationToken);
        }

        await AguardarResultadosAsync(page, cancellationToken);
    }

    private static async Task SelecionarAcordaosQuandoDisponivelAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var checkboxAcordaos = page.Locator("input[name='tipoDeDecisao'][value='A']").First;

        if (await checkboxAcordaos.CountAsync().WaitAsync(cancellationToken) == 0)
        {
            return;
        }

        if (!await checkboxAcordaos.IsCheckedAsync().WaitAsync(cancellationToken))
        {
            await checkboxAcordaos.CheckAsync().WaitAsync(cancellationToken);
        }
    }

    private static async Task AguardarResultadosAsync(IPage page, CancellationToken cancellationToken)
    {
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new()
        {
            Timeout = TimeoutMs
        }).WaitAsync(cancellationToken);

        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('.fundocinza1').length > 0 " +
            "|| document.querySelector('#nomeAba-A') !== null " +
            "|| document.body.innerText.includes('Não existem informações disponíveis') " +
            "|| document.body.innerText.includes('Nenhum resultado')",
            null,
            new() { Timeout = TimeoutMs }).WaitAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<JurisprudenciaResultado>> ExtrairResultadosAsync(
        IPage page,
        int quantidadeMaxima,
        CancellationToken cancellationToken)
    {
        var itens = page.Locator(".fundocinza1");
        var total = await itens.CountAsync().WaitAsync(cancellationToken);
        var limite = Math.Min(total, quantidadeMaxima);
        var resultados = new List<JurisprudenciaResultado>(limite);

        for (var i = 0; i < limite; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = itens.Nth(i);
            var resultado = await ExtrairResultadoAsync(item, cancellationToken);

            if (!string.IsNullOrWhiteSpace(resultado.NumeroProcesso) ||
                !string.IsNullOrWhiteSpace(resultado.Ementa))
            {
                resultados.Add(resultado);
            }
        }

        return resultados;
    }

    private static async Task<JurisprudenciaResultado> ExtrairResultadoAsync(
        ILocator item,
        CancellationToken cancellationToken)
    {
        var linkDownload = item.Locator(".downloadEmenta").First;
        var numeroProcesso = await ObterTextoAsync(linkDownload, cancellationToken);
        var cdAcordao = await ObterAtributoAsync(linkDownload, "cdacordao", cancellationToken);
        var href = await ObterAtributoAsync(linkDownload, "href", cancellationToken);
        var informacoes = await ExtrairInformacoesAsync(item, cancellationToken);
        
        var ementaCompleta = item
            .Locator("div[align='justify'][style='display: none;']")
            .First;

        var ementa = await ementaCompleta.InnerTextAsync();

        return new JurisprudenciaResultado
        {
            Tribunal = TribunalNome,
            NumeroProcesso = LimparTexto(numeroProcesso),
            Ementa = LimparTexto(WebUtility.HtmlDecode(ementa)),
            Link = MontarLink(href, cdAcordao),
            Relator = ObterValor(informacoes, "relator(a)", "relator", "relatora"),
            DataJulgamento = ObterValor(informacoes, "data de julgamento", "data do julgamento")
        };
    }

    private static async Task<Dictionary<string, string>> ExtrairInformacoesAsync(
        ILocator item,
        CancellationToken cancellationToken)
    {
        var nodes = item.Locator(".ementaClass2");
        var total = await nodes.CountAsync().WaitAsync(cancellationToken);
        var informacoes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < total; i++)
        {
            var texto = await ObterTextoAsync(nodes.Nth(i), cancellationToken);
            var partes = texto.Split(':', 2, StringSplitOptions.TrimEntries);

            if (partes.Length != 2 || string.IsNullOrWhiteSpace(partes[0]))
            {
                continue;
            }

            informacoes[NormalizarChave(partes[0])] = LimparTexto(partes[1]);
        }

        return informacoes;
    }

    private static string? ObterValor(
        IReadOnlyDictionary<string, string> informacoes,
        params string[] chaves)
    {
        foreach (var chave in chaves)
        {
            if (informacoes.TryGetValue(NormalizarChave(chave), out var valor) &&
                !string.IsNullOrWhiteSpace(valor))
            {
                return valor;
            }
        }

        return null;
    }

    private static async Task<string> ObterTextoAsync(ILocator locator, CancellationToken cancellationToken)
    {
        if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
        {
            return string.Empty;
        }

        return await locator.InnerTextAsync().WaitAsync(cancellationToken);
    }

    private static async Task<string?> ObterAtributoAsync(
        ILocator locator,
        string atributo,
        CancellationToken cancellationToken)
    {
        if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
        {
            return null;
        }

        return await locator.GetAttributeAsync(atributo).WaitAsync(cancellationToken);
    }

    private static string MontarLink(string? href, string? cdAcordao)
    {
        if (!string.IsNullOrWhiteSpace(href) && Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        if (!string.IsNullOrWhiteSpace(href) && Uri.TryCreate(new Uri(BaseUrl), href, out var relativeUri))
        {
            return relativeUri.ToString();
        }

        if (!string.IsNullOrWhiteSpace(cdAcordao))
        {
            return $"{BaseUrl}/cjsg/getArquivo.do?cdAcordao={Uri.EscapeDataString(cdAcordao)}&cdForo=0";
        }

        return string.Empty;
    }

    private static string LimparTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        return Regex.Replace(texto, @"\s+", " ").Trim();
    }

    private static string NormalizarChave(string chave)
    {
        return chave
            .Trim()
            .ToLowerInvariant()
            .Replace("á", "a")
            .Replace("à", "a")
            .Replace("ã", "a")
            .Replace("â", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("õ", "o")
            .Replace("ô", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");
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
