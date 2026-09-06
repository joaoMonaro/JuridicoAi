using System.Text.Json;

namespace JuridicoAI.Api.Application.Services;

internal static class PecaDefaults
{
    public const string Conteudo = """{"type":"doc","content":[]}""";
    public const string Pagina = """{"format":"A4","unit":"mm","marginTop":30,"marginBottom":20,"marginLeft":30,"marginRight":20}""";
    public const string Estilos = """{"fontFamily":"Times New Roman","fontSize":12,"lineHeight":1.5,"textAlign":"justify"}""";

    public static JsonDocument NovoConteudo() => JsonDocument.Parse(Conteudo);
    public static JsonDocument NovaPagina() => JsonDocument.Parse(Pagina);
    public static JsonDocument NovosEstilos() => JsonDocument.Parse(Estilos);
    public static JsonDocument Copiar(JsonDocument value) => JsonDocument.Parse(value.RootElement.GetRawText());
}
