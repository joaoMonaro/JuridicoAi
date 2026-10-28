using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Exceptions;

namespace JuridicoAI.Api.Application.Services;

public static class DocumentFooterValidator
{
    private sealed record Presentation(double FontSize = 9, string Color = "#66717d",
        string Align = "center", bool Separator = false, double OffsetBottomMm = 5);

    public static void Validate(JsonElement footer)
    {
        if (!footer.TryGetProperty("presentation", out var value) || value.ValueKind == JsonValueKind.Null) return;
        Presentation settings;
        try { settings = value.Deserialize<Presentation>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.Strict }) ?? throw new JsonException(); }
        catch (JsonException) { throw Invalid("Configuração do rodapé inválida."); }
        if (!double.IsFinite(settings.FontSize) || settings.FontSize is < 7 or > 16)
            throw Invalid("O tamanho da fonte do rodapé deve estar entre 7 e 16 pt.");
        if (settings.Align is not ("left" or "center" or "right"))
            throw Invalid("Alinhamento do rodapé inválido.");
        if (settings.Color is not { Length: 7 } || settings.Color[0] != '#' || !settings.Color.Skip(1).All(Uri.IsHexDigit))
            throw Invalid("A cor do rodapé deve estar no formato #RRGGBB.");
        if (!double.IsFinite(settings.OffsetBottomMm) || settings.OffsetBottomMm is < 0 or > 30)
            throw Invalid("A distância da borda inferior deve estar entre 0 e 30 mm.");
    }

    private static RequisicaoInvalidaException Invalid(string message) => new(message);
}
