using System.Buffers.Binary;
using System.Text.Json;
using JuridicoAI.Api.Application.Exceptions;

namespace JuridicoAI.Api.Application.Services;

public static class DocumentHeaderValidator
{
    public const int MaxImageBytes = 1024 * 1024;

    private sealed record Presentation(string Mode = "text", string? ImageDataUrl = null,
        double WidthMm = 150, double MaxHeightMm = 30, string Align = "center",
        double OffsetTopMm = 5, double GapMm = 5);

    public static void Validate(JsonElement header)
    {
        if (!header.TryGetProperty("presentation", out var value) || value.ValueKind == JsonValueKind.Null) return;
        Presentation settings;
        try { settings = value.Deserialize<Presentation>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new JsonException(); }
        catch (JsonException) { throw Invalid("Configuração da imagem de cabeçalho inválida."); }
        if (settings.Mode is not ("text" or "banner" or "logo") || settings.Align is not ("left" or "center" or "right"))
            throw Invalid("Tipo ou alinhamento do cabeçalho inválido.");
        Range(settings.WidthMm, 5, 210, "Largura"); Range(settings.MaxHeightMm, 5, 50, "Altura máxima");
        Range(settings.OffsetTopMm, 0, 30, "Distância do topo"); Range(settings.GapMm, 0, 20, "Distância do texto");
        if (settings.Mode == "text" && settings.ImageDataUrl is null) return;
        var src = settings.ImageDataUrl ?? throw Invalid("Selecione a imagem do cabeçalho.");
        const string png = "data:image/png;base64,";
        const string jpeg = "data:image/jpeg;base64,";
        var prefix = src.StartsWith(png, StringComparison.Ordinal) ? png : src.StartsWith(jpeg, StringComparison.Ordinal) ? jpeg : null;
        if (prefix is null) throw Invalid("Utilize uma imagem PNG ou JPEG.");
        if (src.Length - prefix.Length > 4 * ((MaxImageBytes + 2) / 3)) throw Invalid("A imagem do cabeçalho deve ter até 1 MB.");
        byte[] data;
        try { data = Convert.FromBase64String(src[prefix.Length..]); }
        catch (FormatException) { throw Invalid("Arquivo de imagem inválido."); }
        if (data.Length == 0 || data.Length > MaxImageBytes) throw Invalid("A imagem do cabeçalho deve ter até 1 MB.");
        var (width, height) = Dimensions(data, prefix == png);
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192 || (long)width * height > 16_000_000)
            throw Invalid("Imagem inválida ou com dimensões acima de 16 megapixels.");
    }

    private static (int Width, int Height) Dimensions(byte[] data, bool png)
    {
        if (png && data.Length >= 45 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && data.AsSpan(12, 4).SequenceEqual("IHDR"u8) && data.AsSpan(data.Length - 8, 4).SequenceEqual("IEND"u8))
            return (BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4)));
        if (!png && data.Length > 4 && data[0] == 255 && data[1] == 216 && data[^2] == 255 && data[^1] == 217)
        {
            var pos = 2;
            while (pos + 8 < data.Length)
            {
                if (data[pos++] != 255) break;
                while (pos < data.Length && data[pos] == 255) pos++;
                if (pos >= data.Length) break;
                var marker = data[pos++];
                if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) continue;
                if (pos + 2 > data.Length) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos, 2));
                if (length < 2 || pos + length > data.Length) break;
                if (marker is 0xC0 or 0xC1 or 0xC2 && length >= 7)
                    return (BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos + 5, 2)), BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos + 3, 2)));
                pos += length;
            }
        }
        throw Invalid("O arquivo não corresponde a uma imagem PNG ou JPEG válida.");
    }

    private static void Range(double value, double min, double max, string label)
    {
        if (!double.IsFinite(value) || value < min || value > max) throw Invalid($"{label}: informe um valor entre {min} e {max} mm.");
    }
    private static RequisicaoInvalidaException Invalid(string message) => new(message);
}
