using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JuridicoAI.Service;

public class OpenAIService
{
    private readonly HttpClient _httpClient;

    public OpenAIService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GerarPeticao(string descricao)
    {
        var request = new
        {
            model = "gpt-4.1",
            input = $@"Você é um advogado especialista em direito do consumidor.

                    Gere uma petição inicial com base no caso abaixo:

                    CASO:
                    {descricao}"
        };
        
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
             "application/json"
        );

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "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");
        
        var response = await _httpClient.PostAsync("https://api.openai.com/v1/responses",  content);
        
        var json = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(json);
        var texto = doc
            .RootElement
            .GetProperty("output")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return texto;
    }
    
}