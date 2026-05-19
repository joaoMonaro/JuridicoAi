using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JuridicoAI.Models;

namespace JuridicoAI.Service;

public sealed class PeticaoService
{
    private const string ResponsesEndpoint = "https://api.openai.com/v1/responses";

    private readonly HttpClient _httpClient;
    private readonly IJurisprudenciaProvider _jurisprudenciaProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PeticaoService> _logger;

    public PeticaoService(
        HttpClient httpClient,
        IJurisprudenciaProvider jurisprudenciaProvider,
        IConfiguration configuration,
        ILogger<PeticaoService> logger)
    {
        _httpClient = httpClient;
        _jurisprudenciaProvider = jurisprudenciaProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GerarPeticaoResponse> GerarAsync(
        GerarPeticaoRequest request,
        CancellationToken cancellationToken)
    {
        var coletaJurisprudencias = await ObterJurisprudenciasAsync(request, cancellationToken);
        var validacao = ValidarJurisprudencias(
            coletaJurisprudencias.Jurisprudencias,
            coletaJurisprudencias.Alertas);

        var prompt = MontarPrompt(request.DescricaoCaso, validacao.JurisprudenciasValidas, validacao.Alertas);
        var peticao = await EnviarParaOpenAIAsync(prompt, cancellationToken);

        return new GerarPeticaoResponse
        {
            Peticao = peticao,
            Alertas = validacao.Alertas,
            JurisprudenciasUtilizadas = validacao.JurisprudenciasValidas
                .Select(j => new JurisprudenciaUtilizada
                {
                    Tribunal = j.Tribunal,
                    NumeroProcesso = j.NumeroProcesso,
                    Link = j.Link
                })
                .ToList()
        };
    }

    private async Task<JurisprudenciasColetadas> ObterJurisprudenciasAsync(
        GerarPeticaoRequest request,
        CancellationToken cancellationToken)
    {
        var alertas = new List<string>();
        var jurisprudencias = new List<JurisprudenciaResultado>();

        if (request.Jurisprudencias is { Count: > 0 })
        {
            jurisprudencias.AddRange(request.Jurisprudencias);
        }

        if (string.IsNullOrWhiteSpace(request.TermoPesquisaJurisprudencia))
        {
            return new JurisprudenciasColetadas(RemoverJurisprudenciasDuplicadas(jurisprudencias), alertas);
        }

        var quantidadeMaxima = NormalizarQuantidadeMaxima(request.QuantidadeMaximaJurisprudencias);

        try
        {
            var jurisprudenciasPesquisadas = await _jurisprudenciaProvider.PesquisarAsync(
                request.TermoPesquisaJurisprudencia,
                quantidadeMaxima,
                cancellationToken);

            jurisprudencias.AddRange(jurisprudenciasPesquisadas);

            if (jurisprudenciasPesquisadas.Count == 0)
            {
                alertas.Add(
                    $"A pesquisa de jurisprudência no {_jurisprudenciaProvider.Tribunal} não retornou resultados para o termo informado.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falha ao pesquisar jurisprudencias para gerar peticao. Tribunal: {Tribunal}. Termo: {Termo}",
                _jurisprudenciaProvider.Tribunal,
                request.TermoPesquisaJurisprudencia);

            throw new PeticaoGenerationException(
                $"Não foi possível pesquisar jurisprudências no {_jurisprudenciaProvider.Tribunal}.",
                ex);
        }

        return new JurisprudenciasColetadas(RemoverJurisprudenciasDuplicadas(jurisprudencias), alertas);
    }

    private static int NormalizarQuantidadeMaxima(int? quantidadeMaxima)
    {
        return quantidadeMaxima is > 0 and <= 50
            ? quantidadeMaxima.Value
            : 10;
    }

    private static IReadOnlyList<JurisprudenciaResultado> RemoverJurisprudenciasDuplicadas(
        IEnumerable<JurisprudenciaResultado> jurisprudencias)
    {
        var resultado = new List<JurisprudenciaResultado>();
        var chaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var jurisprudencia in jurisprudencias)
        {
            var chave = $"{jurisprudencia.Tribunal}|{jurisprudencia.NumeroProcesso}|{jurisprudencia.Link}";

            if (chaves.Add(chave))
            {
                resultado.Add(jurisprudencia);
            }
        }

        return resultado;
    }

    private async Task<string> EnviarParaOpenAIAsync(string prompt, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new PeticaoGenerationException("A chave da OpenAI não está configurada.");
        }

        var model = _configuration["OpenAI:Model"];
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "gpt-4.1";
        }

        var request = new
        {
            model,
            input = prompt
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ResponsesEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenAI retornou erro ao gerar petição. StatusCode: {StatusCode}. Body: {Body}",
                    (int)response.StatusCode,
                    json);

                throw new PeticaoGenerationException("Não foi possível gerar a petição no momento.");
            }

            return ExtrairTextoResposta(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PeticaoGenerationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao chamar a OpenAI.");
            throw new PeticaoGenerationException("Não foi possível gerar a petição no momento.", ex);
        }
    }

    private static string ExtrairTextoResposta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("output_text", out var outputText))
            {
                return outputText.GetString() ?? string.Empty;
            }

            var output = doc.RootElement.GetProperty("output");
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content))
                {
                    continue;
                }

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new PeticaoGenerationException("A resposta da OpenAI veio em formato inesperado.", ex);
        }

        throw new PeticaoGenerationException("A resposta da OpenAI não trouxe texto para a petição.");
    }

    private static ValidacaoJurisprudencias ValidarJurisprudencias(
        IReadOnlyList<JurisprudenciaResultado>? jurisprudencias,
        IReadOnlyList<string>? alertasIniciais = null)
    {
        var alertas = alertasIniciais is null ? [] : alertasIniciais.ToList();
        var jurisprudenciasValidas = new List<JurisprudenciaResultado>();

        if (jurisprudencias is null || jurisprudencias.Count == 0)
        {
            alertas.Add("Nenhuma jurisprudência foi fornecida. A fundamentação jurisprudencial é insuficiente.");
            return new ValidacaoJurisprudencias(jurisprudenciasValidas, alertas);
        }

        foreach (var jurisprudencia in jurisprudencias)
        {
            var camposObrigatoriosAusentes =
                string.IsNullOrWhiteSpace(jurisprudencia.Tribunal) ||
                string.IsNullOrWhiteSpace(jurisprudencia.NumeroProcesso) ||
                string.IsNullOrWhiteSpace(jurisprudencia.Ementa) ||
                string.IsNullOrWhiteSpace(jurisprudencia.Link);

            if (camposObrigatoriosAusentes)
            {
                alertas.Add("Uma jurisprudência foi ignorada por não conter tribunal, número do processo, ementa ou link.");
                continue;
            }

            jurisprudenciasValidas.Add(jurisprudencia);

            if (EmentaIndicaPossivelContrariedade(jurisprudencia.Ementa))
            {
                alertas.Add(
                    $"A jurisprudência {jurisprudencia.NumeroProcesso} pode ser contrária ou fraca para o pedido do autor. Avalie antes de usar como fundamento favorável.");
            }
        }

        if (jurisprudenciasValidas.Count == 0)
        {
            alertas.Add("Nenhuma jurisprudência útil foi fornecida. A peça deve deixar clara a insuficiência da base jurisprudencial.");
        }
        else if (jurisprudenciasValidas.Count < 2)
        {
            alertas.Add("A base jurisprudencial fornecida parece limitada por conter poucas decisões.");
        }

        return new ValidacaoJurisprudencias(jurisprudenciasValidas, alertas);
    }

    private static bool EmentaIndicaPossivelContrariedade(string ementa)
    {
        var termos = new[]
        {
            "recurso não provido",
            "dano moral não caracterizado",
            "pedido improcedente",
            "improcedência",
            "não caracterizado",
            "não configurado"
        };

        return termos.Any(termo => ementa.Contains(termo, StringComparison.OrdinalIgnoreCase));
    }

    private static string MontarPrompt(
        string descricaoCaso,
        IReadOnlyList<JurisprudenciaResultado> jurisprudencias,
        IReadOnlyList<string> alertas)
    {
        var jurisprudenciasJson = JsonSerializer.Serialize(
            jurisprudencias,
            new JsonSerializerOptions { WriteIndented = true });

        var alertasJson = JsonSerializer.Serialize(
            alertas,
            new JsonSerializerOptions { WriteIndented = true });

        return $"""
            Atue como advogado especialista em direito do consumidor.

            Gere uma petição inicial em português jurídico claro, como rascunho técnico, sem prometer sucesso judicial.

            REGRAS OBRIGATÓRIAS:
            - Use apenas os fatos informados na descrição do caso.
            - Não invente fatos, documentos, leis, jurisprudência, tribunal, número de processo, relator, data ou link.
            - Use apenas as jurisprudências fornecidas no JSON abaixo.
            - Não cite decisões que não estejam na lista fornecida.
            - Toda jurisprudência citada deve conter tribunal, número do processo, relator se disponível, data do julgamento se disponível e link.
            - Quando citar jurisprudência, reproduza o número do processo e o link.
            - Se algum dado estiver ausente, use o marcador [informar].
            - Se a base jurisprudencial for insuficiente, fraca ou contraditória, deixe isso claro na seção de jurisprudência aplicável.
            - Se houver jurisprudência contrária ao pedido do autor, não a utilize como fundamento favorável.

            ESTRUTURA MÍNIMA:
            1. Endereçamento genérico
            2. Qualificação genérica das partes
            3. Dos fatos
            4. Do direito
            5. Da jurisprudência aplicável
            6. Dos pedidos
            7. Valor da causa genérico
            8. Fechamento

            DESCRIÇÃO DO CASO:
            {descricaoCaso}

            JURISPRUDÊNCIAS FORNECIDAS:
            {jurisprudenciasJson}

            ALERTAS JÁ IDENTIFICADOS PELO SISTEMA:
            {alertasJson}
            """;
    }

    private sealed record ValidacaoJurisprudencias(
        IReadOnlyList<JurisprudenciaResultado> JurisprudenciasValidas,
        IReadOnlyList<string> Alertas);

    private sealed record JurisprudenciasColetadas(
        IReadOnlyList<JurisprudenciaResultado> Jurisprudencias,
        IReadOnlyList<string> Alertas);
}
