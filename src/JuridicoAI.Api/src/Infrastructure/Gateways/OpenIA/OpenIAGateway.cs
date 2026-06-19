using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JuridicoAI.Api.Infrastructure.Gateways.OpenIA;

public class OpenIAGateway(HttpClient httpClient, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _apiKey = configuration["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException("OpenAI:ApiKey nao configurada.");

    private readonly string _model = configuration["OpenAI:Model"] ?? "gpt-4.1";

    public async Task<string> GerarTextoAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var requestBody = new OpenIAResponseRequest(_model, prompt);

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Erro ao chamar API da OpenAI: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseContent}");
        }

        var openIAResponse = JsonSerializer.Deserialize<OpenIAResponse>(responseContent, JsonOptions);

        return openIAResponse?.OutputText ?? responseContent;
    }

    private sealed record OpenIAResponseRequest(string Model, string Input);

    private sealed record OpenIAResponse(
        [property: JsonPropertyName("output_text")] string? OutputText);
}
