using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JuridicoAI.Api.Infrastructure.Gateways.OpenIA;

public class OpenIAGateway(HttpClient httpClient)
{
    private const string ApiKey = "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";
    private const string Model = "gpt-4.1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GerarTextoAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var requestBody = new OpenIAResponseRequest(Model, prompt);

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

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
