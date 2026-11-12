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
    IOptions<AssistenteIaSettings> options) : IAssistenteIaProvider, IAssistenteTokenCounter
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly JsonElement OutputSchema = CreateOutputSchema();
    private readonly AssistenteIaSettings _settings = options.Value;

    public async Task<int> ContarTokensEntradaAsync(
        AssistenteIaProviderRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "responses/input_tokens");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        var payload = CreateInputPayload(request); payload.Remove("include");
        message.Content = JsonContent.Create(payload);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw CreateHttpException(response.StatusCode);

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("input_tokens", out var inputTokens) ||
                !inputTokens.TryGetInt32(out var count) || count < 0)
                throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: false);
            return count;
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

    public async Task<AssistenteIaProviderResult> GerarAsync(
        AssistenteIaProviderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FerramentasDocumentais is not null) return await GerarComFerramentasAsync(request, cancellationToken);
        return (await GerarTurnoAsync(request, CreateInputPayload(request), cancellationToken)).Result
            ?? throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
    }

    private async Task<ProviderTurn> GerarTurnoAsync(AssistenteIaProviderRequest request,
        Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "responses");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        payload["stream"] = true;
        payload["store"] = false;
        payload["max_output_tokens"] = request.MaxOutputTokens;
        payload["truncation"] = "disabled";
        message.Content = JsonContent.Create(payload);

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
            var outputItems = new List<JsonElement>();

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                var data = line[5..].TrimStart();
                if (data.Length == 0 || data == "[DONE]") continue;
                if (data.Length > 4 * 1024 * 1024) throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);

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
                            if (output.Length > (request.Conversacional ? 1024 * 1024 : _settings.MaxOutputCharacters * 3))
                                throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: false);
                        }
                        break;
                    case "response.completed":
                        completed = true;
                        if (root.TryGetProperty("response", out var responseElement))
                        {
                            if (responseElement.TryGetProperty("status", out var status) && status.GetString() != "completed")
                                throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
                            if (responseElement.TryGetProperty("output", out var items))
                            {
                                if (items.ValueKind != JsonValueKind.Array) throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
                                foreach (var item in items.EnumerateArray())
                                {
                                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("type", out var itemType) || itemType.ValueKind != JsonValueKind.String)
                                        throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
                                    outputItems.Add(item.Clone());
                                }
                            }
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

            if (!completed)
                throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: true);

            if (request.FerramentasDocumentais is not null && outputItems.Any(x => x.GetProperty("type").GetString() == "function_call"))
                return new(null, outputItems, model, inputTokens, cachedInputTokens, outputTokens);
            if (output.Length == 0) throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", true);

            if (request.Conversacional)
            {
                var automatic = JsonSerializer.Deserialize<OpenAiAutomaticOutput>(output.ToString(), JsonOptions)
                    ?? throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
                if (automatic.Grupos is null || automatic.Alertas is null || automatic.Titulo is null || automatic.Texto is null || automatic.Resumo is null)
                    throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
                if (request.FerramentasDocumentais?.HasDocumentTools == true && automatic.CoberturaPretendida is not ("Relevantes" or "Todos") ||
                    request.FerramentasDocumentais?.HasDocumentTools != true && automatic.CoberturaPretendida is not null)
                    throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
                var result = new AssistenteIaProviderResult(automatic.Titulo, automatic.Texto, automatic.Resumo,
                    automatic.Intencao == AssistenteIntencao.Responder ? AssistenteOperacao.ResponderNoPainel : AssistenteOperacao.ProporAlteracoes,
                    automatic.Alertas, [], inputTokens, cachedInputTokens, outputTokens, model,
                    new(automatic.Intencao, automatic.Grupos, automatic.SubstituirPropostaReferenciada), automatic.CoberturaPretendida);
                return new(result, outputItems, model, inputTokens, cachedInputTokens, outputTokens);
            }
            var structured = JsonSerializer.Deserialize<OpenAiStructuredOutput>(output.ToString(), JsonOptions)
                ?? throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", retryable: false);
            return new(new AssistenteIaProviderResult(
                structured.Titulo,
                structured.Texto,
                structured.Resumo,
                structured.OperacaoSugerida,
                structured.Alertas,
                structured.Achados,
                inputTokens,
                cachedInputTokens,
                outputTokens,
                model), outputItems, model, inputTokens, cachedInputTokens, outputTokens);
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

    private sealed record ProviderTurn(AssistenteIaProviderResult? Result, IReadOnlyList<JsonElement> Output,
        string Model, int? InputTokens, int? CachedInputTokens, int? OutputTokens);

    private async Task<AssistenteIaProviderResult> GerarComFerramentasAsync(AssistenteIaProviderRequest request, CancellationToken ct)
    {
        var tools = request.FerramentasDocumentais!;
        var input = new List<JsonElement> { JsonSerializer.SerializeToElement(new { role = "user", content = request.Input }) };
        var calls = new List<AssistenteProviderCall>(); var identifiers = new HashSet<string>();
        long spent = 0; var model = _settings.Model;
        for (var round = 0; round <= tools.MaxRounds; round++)
        {
            ct.ThrowIfCancellationRequested();
            var contextSize = request.Input.Length + input.Skip(1).Sum(item => item.TryGetProperty("output", out var content) && content.ValueKind == JsonValueKind.String
                ? content.GetString()!.Length : item.GetRawText().Length);
            if (contextSize > tools.MaxContextCharacters) return await LimitedAsync();
            var payload = CreateInputPayload(request); payload["input"] = input;
            var requiredTool = tools.RequiredTool?.Invoke();
            if (requiredTool is not null && round == tools.MaxRounds) return await LimitedAsync();
            payload["tool_choice"] = round == tools.MaxRounds ? "none" : requiredTool is null ? "auto" : new { type = "function", name = requiredTool };
            if (tools.BeforeCall is not null) await tools.BeforeCall(ct);
            var counted = round == 0 && tools.InitialInputTokens is { } initial ? initial : await CountPayloadAsync(payload, ct);
            if (counted > tools.MaxInputTokens || (long)spent + counted + request.MaxOutputTokens > tools.MaxTotalTokens)
                return await LimitedAsync();
            if (tools.BeforeCall is not null) await tools.BeforeCall(ct);
            if (tools.BeforeGeneration is not null) await tools.BeforeGeneration(counted, request.MaxOutputTokens, ct);
            var started = TimeProvider.System.GetTimestamp(); ProviderTurn turn;
            try { turn = await GerarTurnoAsync(request, payload, ct); }
            catch (Exception ex)
            {
                var code = ex is AssistenteIaProviderException providerError ? providerError.Code : ex is OperationCanceledException ? "AI_CANCELLED_OR_TIMEOUT" : "AI_PROVIDER_ERROR";
                await tools.RecordAsync(new(round + 1, model, null, null, null, "failed", code,
                    (long)TimeProvider.System.GetElapsedTime(started).TotalMilliseconds));
                throw;
            }
            model = turn.Model;
            var call = new AssistenteProviderCall(round + 1, model, turn.InputTokens, turn.CachedInputTokens, turn.OutputTokens,
                "succeeded", null, (long)TimeProvider.System.GetElapsedTime(started).TotalMilliseconds);
            calls.Add(call); await tools.RecordAsync(call);
            spent += (long)Math.Max(counted, turn.InputTokens ?? counted) + (turn.OutputTokens ?? request.MaxOutputTokens);
            if (turn.Result is { } result)
            {
                if (requiredTool is not null) throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
                tools.SetScope?.Invoke(result.CoberturaPretendida!);
                return Aggregate(result);
            }
            var pending = turn.Output.Where(x => x.GetProperty("type").GetString() == "function_call").ToArray();
            if (pending.Length != 1 || round == tools.MaxRounds) throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
            var function = pending[0];
            if (function.TryGetProperty("status", out var status) && (status.ValueKind != JsonValueKind.String || status.GetString() != "completed") ||
                !function.TryGetProperty("call_id", out var callId) || callId.GetString() is not { Length: > 0 and <= 256 } id || !identifiers.Add(id) ||
                !function.TryGetProperty("name", out var name) || name.GetString() is not { Length: > 0 } functionName ||
                !function.TryGetProperty("arguments", out var args) || args.ValueKind != JsonValueKind.String)
                throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
            // Replay canonical output, including encrypted reasoning, only in
            // this request. No reasoning/provider-internal items enter the UI.
            if (requiredTool is not null && functionName != requiredTool) throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
            input.AddRange(turn.Output);
            var resultText = await tools.ExecuteAsync(functionName, args.GetString()!, ct);
            if (tools.CompleteAfterTool?.Invoke() is { } terminal) return Aggregate(terminal);
            input.Add(JsonSerializer.SerializeToElement(new { type = "function_call_output", call_id = id, output = resultText }));
        }
        return await LimitedAsync();

        AssistenteIaProviderResult Aggregate(AssistenteIaProviderResult result) => result with
        {
            EntradaTokens = calls.All(c => c.InputTokens.HasValue) ? calls.Sum(c => c.InputTokens!.Value) : null,
            EntradaTokensEmCache = calls.All(c => c.CachedInputTokens.HasValue) ? calls.Sum(c => c.CachedInputTokens!.Value) : null,
            SaidaTokens = calls.All(c => c.OutputTokens.HasValue) ? calls.Sum(c => c.OutputTokens!.Value) : null
        };
        async Task<AssistenteIaProviderResult> LimitedAsync()
        {
            if (tools.OnLimit is not null) await tools.OnLimit(ct);
            return Aggregate(new("Consulta incompleta",
                "A consulta atingiu o limite desta etapa. O histórico permanece salvo; nenhuma alteração incompleta foi preparada.",
                "", AssistenteOperacao.ResponderNoPainel, [new(tools.HasDocumentTools ? "DOCUMENT_RETRIEVAL_LIMIT" : "HISTORY_RETRIEVAL_LIMIT", "A consulta foi interrompida por limite de contexto ou consumo.")],
                [], null, null, null, model, new(AssistenteIntencao.Responder, [], false), tools.HasDocumentTools ? tools.Coverage.EscopoSolicitado : null));
        }
    }

    private async Task<int> CountPayloadAsync(Dictionary<string, object?> payload, CancellationToken ct)
    {
        payload = new(payload); payload.Remove("include");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "responses/input_tokens");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = JsonContent.Create(payload);
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw CreateHttpException(response.StatusCode);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!json.RootElement.TryGetProperty("input_tokens", out var value) || !value.TryGetInt32(out var result) || result < 0)
                throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false);
            return result;
        }
        catch (HttpRequestException ex) { throw new AssistenteIaProviderException("AI_UNAVAILABLE", true, innerException: ex); }
        catch (JsonException ex) { throw new AssistenteIaProviderException("AI_PROVIDER_ERROR", false, innerException: ex); }
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
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result) && result >= 0
            ? result
            : null;

    private Dictionary<string, object?> CreateInputPayload(AssistenteIaProviderRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
        ["model"] = _settings.Model,
        ["instructions"] = request.Instructions,
        ["input"] = request.FerramentasDocumentais is null ? request.Input : new object[] { new { role = "user", content = request.Input } },
        ["text"] = new Dictionary<string, object?>
        {
            ["format"] = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["name"] = request.Conversacional ? "assistente_conversacional_response" : "assistente_juridico_response",
                ["strict"] = true,
                ["schema"] = request.Conversacional ? AssistenteAutomaticOutputSchema.Criar(
                    request.OperacoesPermitidas.Contains(AssistenteOperacao.ProporAlteracoes), request.PodeGerarMinuta, request.FerramentasDocumentais?.HasDocumentTools == true) : OutputSchema
            }
        }
        };
        if (request.FerramentasDocumentais is not null)
        {
            payload["tools"] = request.FerramentasDocumentais.Definitions;
            payload["tool_choice"] = request.FerramentasDocumentais.RequiredTool?.Invoke() is { } required
                ? new { type = "function", name = required } : (object)"auto";
            payload["parallel_tool_calls"] = false;
            payload["include"] = new[] { "reasoning.encrypted_content" };
        }
        return payload;
    }

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

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record OpenAiAutomaticOutput([property: JsonRequired] string Titulo, [property: JsonRequired] string Texto, [property: JsonRequired] string Resumo,
        [property: JsonRequired] AssistenteIntencao Intencao, [property: JsonRequired] IReadOnlyList<AssistenteAlertaDto> Alertas,
        [property: JsonRequired] IReadOnlyList<AssistenteGrupoRequest> Grupos, [property: JsonRequired] bool SubstituirPropostaReferenciada,
        string? CoberturaPretendida = null);
}
