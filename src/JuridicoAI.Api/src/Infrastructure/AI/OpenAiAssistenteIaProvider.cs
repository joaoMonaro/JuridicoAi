using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.AI;

public sealed class OpenAiAssistenteIaProvider(
    HttpClient httpClient,
    IOptions<AssistenteIaSettings> options) : IAssistenteIaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly JsonElement OutputSchema = CreateOutputSchema();
    private readonly AssistenteIaSettings _settings = options.Value;

    public async Task<AssistenteIaProviderResult> GerarAsync(
        AssistenteIaProviderRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "responses");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Content = JsonContent.Create(new Dictionary<string, object?>
        {
            ["model"] = _settings.Model,
            ["instructions"] = request.Instructions,
            ["input"] = request.Input,
            ["stream"] = true,
            ["store"] = false,
            ["max_output_tokens"] = request.MaxOutputTokens,
            ["truncation"] = "disabled",
            ["text"] = new Dictionary<string, object?>
            {
                ["format"] = new Dictionary<string, object?>
                {
                    ["type"] = "json_schema",
                    ["name"] = "assistente_juridico_response",
                    ["strict"] = true,
                    ["schema"] = OutputSchema
                }
            }
        });

        try
        {
            using var response = await httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw CreateHttpException(response.StatusCode);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var output = new StringBuilder();
            var completed = false;
            var model = _settings.Model;
            int? inputTokens = null;
            int? cachedInputTokens = null;
            int? outputTokens = null;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                var data = line[5..].TrimStart();
                if (data.Length == 0 || data == "[DONE]") continue;

                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                switch (type)
                {
                    case "response.output_text.delta":
                        if (root.TryGetProperty("delta", out var delta))
                        {
                            output.Append(delta.GetString());
                            if (output.Length > _settings.MaxOutputCharacters * 3)
                                throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: false);
                        }
                        break;
                    case "response.completed":
                        completed = true;
                        if (root.TryGetProperty("response", out var responseElement))
                        {
                            if (responseElement.TryGetProperty("model", out var modelElement))
                                model = modelElement.GetString() ?? model;
                            if (responseElement.TryGetProperty("usage", out var usage))
                            {
                                inputTokens = ReadNullableInt(usage, "input_tokens");
                                if (usage.TryGetProperty("input_tokens_details", out var inputDetails))
                                    cachedInputTokens = ReadNullableInt(inputDetails, "cached_tokens");
                                outputTokens = ReadNullableInt(usage, "output_tokens");
                            }
                        }
                        break;
                    case "response.failed":
                    case "response.incomplete":
                    case "error":
                        throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: type != "response.incomplete");
                }
            }

            if (!completed || output.Length == 0)
                throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: true);

            var structured = JsonSerializer.Deserialize<OpenAiStructuredOutput>(output.ToString(), JsonOptions)
                ?? throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: false);
            return new AssistenteIaProviderResult(
                structured.Titulo,
                structured.Texto,
                structured.Resumo,
                structured.OperacaoSugerida,
                structured.Alertas,
                structured.Achados,
                inputTokens,
                cachedInputTokens,
                outputTokens,
                model);
        }
        catch (AssistenteIaProviderException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new AssistenteIaProviderException("AI_UNAVAILABLE", retryable: true, innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: false, innerException: ex);
        }
    }

    private static AssistenteIaProviderException CreateHttpException(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.TooManyRequests => new("AI_RATE_LIMITED", retryable: true, (int)statusCode),
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
            new("AI_PROVIDER_TIMEOUT", retryable: true, (int)statusCode),
        >= HttpStatusCode.InternalServerError => new("AI_UNAVAILABLE", retryable: true, (int)statusCode),
        _ => new("AI_PROVIDER_ERROR", retryable: false, (int)statusCode)
    };

    private static int? ReadNullableInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static JsonElement CreateOutputSchema()
    {
        using var document = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "titulo": { "type": "string" },
                "texto": { "type": "string" },
                "resumo": { "type": "string" },
                "operacaoSugerida": {
                  "type": "string",
                  "enum": ["ResponderNoPainel", "InserirNoCursor", "InserirAposSelecao", "SubstituirSelecao", "CriarSecao"]
                },
                "alertas": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "codigo": { "type": "string" },
                      "mensagem": { "type": "string" }
                    },
                    "required": ["codigo", "mensagem"],
                    "additionalProperties": false
                  }
                },
                "achados": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "id": { "type": "string" },
                      "categoria": {
                        "type": "string",
                        "enum": ["Lacuna", "Contradicao", "Clareza", "Estrutura", "Verificacao"]
                      },
                      "titulo": { "type": "string" },
                      "descricao": { "type": "string" },
                      "alertas": {
                        "type": "array",
                        "items": {
                          "type": "object",
                          "properties": {
                            "codigo": { "type": "string" },
                            "mensagem": { "type": "string" }
                          },
                          "required": ["codigo", "mensagem"],
                          "additionalProperties": false
                        }
                      }
                    },
                    "required": ["id", "categoria", "titulo", "descricao", "alertas"],
                    "additionalProperties": false
                  }
                }
              },
              "required": ["titulo", "texto", "resumo", "operacaoSugerida", "alertas", "achados"],
              "additionalProperties": false
            }
            """);
        return document.RootElement.Clone();
    }

    private sealed record OpenAiStructuredOutput(
        string Titulo,
        string Texto,
        string Resumo,
        AssistenteOperacao OperacaoSugerida,
        IReadOnlyList<AssistenteAlertaDto> Alertas,
        IReadOnlyList<AssistenteAchadoDto> Achados);
}
