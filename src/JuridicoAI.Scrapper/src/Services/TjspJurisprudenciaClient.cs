using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using JuridicoAI.Scrapper.Models;

namespace JuridicoAI.Scrapper.Services;

public sealed class TjspJurisprudenciaClient : IDisposable
{
    private const string BaseUrl = "https://esaj.tjsp.jus.br";
    private const string ConsultaCompletaUrl = BaseUrl + "/cjsg/consultaCompleta.do";
    private const string ResultadoCompletaUrl = BaseUrl + "/cjsg/resultadoCompleta.do";
    private const string CaptchaControleUrl = BaseUrl + "/cjsg/captchaControleAcesso.do";
    private const string TrocaDePaginaUrl = BaseUrl + "/cjsg/trocaDePagina.do";
    private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 " +
                                     "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;

    public TjspJurisprudenciaClient(CookieContainer cookies)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = cookies,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        _httpClient = new HttpClient(handler, disposeHandler: true);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
    }

    public async Task<string> AtualizarUuidCaptchaAsync(string uuidCaptcha, CancellationToken cancellationToken)
    {
        using var request = CriarRequest(HttpMethod.Post, CaptchaControleUrl, ConsultaCompletaUrl);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("uuidCaptcha", uuidCaptcha),
            new KeyValuePair<string, string>("conversationId", string.Empty)
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return uuidCaptcha;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("uuidCaptcha", out var novoUuidCaptcha))
            {
                return novoUuidCaptcha.GetString() ?? uuidCaptcha;
            }
        }
        catch (JsonException)
        {
            return uuidCaptcha;
        }

        return uuidCaptcha;
    }

    public async Task<string> PesquisarAsync(
        TjspBrowserSession session,
        string uuidCaptcha,
        TjspSearchRequest searchRequest,
        CancellationToken cancellationToken)
    {
        using var request = CriarRequest(HttpMethod.Post, ResultadoCompletaUrl, ConsultaCompletaUrl);
        request.Content = new FormUrlEncodedContent(TjspPayloadBuilder.CriarPesquisaLivre(
            searchRequest.Termo,
            session.RecaptchaToken,
            uuidCaptcha,
            searchRequest.IncluirSinonimos,
            searchRequest.TipoDecisao));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"TJSP retornou HTTP {(int)response.StatusCode} ao consultar jurisprudencias.");
        }

        return html;
    }

    public async Task<string> ObterPaginaAsync(
        string tipoDecisao,
        int pagina,
        CancellationToken cancellationToken)
    {
        var url = $"{TrocaDePaginaUrl}?tipoDeDecisao={Uri.EscapeDataString(tipoDecisao)}&pagina={pagina}";
        using var request = CriarRequest(HttpMethod.Get, url, ResultadoCompletaUrl);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"TJSP retornou HTTP {(int)response.StatusCode} ao consultar pagina {pagina}.");
        }

        return html;
    }

    private static HttpRequestMessage CriarRequest(HttpMethod method, string url, string referer)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Origin", BaseUrl);
        request.Headers.Referrer = new Uri(referer);
        return request;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
