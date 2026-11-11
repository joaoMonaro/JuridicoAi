using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Infrastructure.Documents;

public sealed record ExtractedPart(int? Page, string Locator, string Text, string Quality,
    string? Reason = null, string Metrics = "{}", int? TotalPages = null);

public static class DocumentQuality
{
    public const string Version = "qualidade-v1";
    public static string VersionFor(AssistenteDocumentSettings settings)
    {
        if (!settings.OcrEnabled) return Version + ":ocr-off";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var language in new[] { "por", "eng" })
            hash.AppendData(File.ReadAllBytes(Path.Combine(settings.TessdataPath, language + ".traineddata")));
        return Version + ":" + settings.OcrCalibrationVersion + ":" + Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public static bool ValidText(string value) => !value.Contains('\uFFFD') &&
        !value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') &&
        value.EnumerateRunes().All(r => Rune.GetUnicodeCategory(r) is not UnicodeCategory.PrivateUse and not UnicodeCategory.OtherNotAssigned);

    public static ExtractedPart Native(string text, int? page, string locator, string? reason = null) =>
        new(page, locator, text, reason is not null || !ValidText(text) ? "Duvidoso" : string.IsNullOrWhiteSpace(text) ? "Ilegivel" : "Confiavel",
            reason ?? (!ValidText(text) ? "CHARACTER_MAPPING_INVALID" : string.IsNullOrWhiteSpace(text) ? "NO_READABLE_TEXT" : null));

    // Preserve whole paragraph/line units: an uncertain word never disappears
    // from an otherwise "trusted" sentence. Confidence is not a probability.
    public static IReadOnlyList<ExtractedPart> OcrTsv(string tsv, int? page, string locator)
    {
        var words = tsv.Split('\n').Skip(1).Select(row => row.TrimEnd('\r').Split('\t', 12))
            .Where(c => c.Length == 12 && c[0] == "5" && !string.IsNullOrWhiteSpace(c[11]))
            .Select(c => new { Group = string.Join(':', c.Skip(1).Take(3)), Text = c[11],
                Confidence = double.TryParse(c[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var conf) ? conf : -1,
                Box = c.Skip(6).Take(4).ToArray() }).ToArray();
        if (words.Length == 0) return [new(page, locator, "", "Ilegivel", "NO_READABLE_TEXT")];
        return words.GroupBy(w => w.Group).Select((g, index) =>
        {
            var values = g.ToArray(); var text = string.Join(' ', values.Select(w => w.Text));
            var average = values.Sum(w => w.Confidence * w.Text.Length) / values.Sum(w => w.Text.Length);
            var trusted = ValidText(text) && average >= 97 && values.All(w => w.Confidence >= 90) &&
                values.Where(w => w.Text.Any(char.IsDigit) || w.Text.Contains('$')).All(w => w.Confidence >= 98);
            return new ExtractedPart(page, $"{locator}, região {index + 1}", text, trusted ? "Confiavel" : "Duvidoso",
                trusted ? null : "OCR_CONFIDENCE_INSUFFICIENT", JsonSerializer.Serialize(new { average,
                    words = values.Select(w => new { text = w.Text, confidence = w.Confidence, box = w.Box }) }));
        }).ToArray();
    }

    public static IEnumerable<ExtractedPart> Chunks(ExtractedPart part)
    {
        // Never split a rejected unit into apparently reliable fragments.
        if (part.Quality != "Confiavel" || part.Text.Length <= 3200) { yield return part; yield break; }
        var unbroken = 0;
        foreach (var character in part.Text)
        {
            unbroken = char.IsWhiteSpace(character) ? 0 : unbroken + 1;
            if (unbroken <= 3200) continue;
            yield return part with { Quality = "Duvidoso", Reason = "OVERSIZE_TEXT_UNIT" };
            yield break;
        }
        var start = 0;
        while (start < part.Text.Length)
        {
            var length = Math.Min(3200, part.Text.Length - start);
            if (start + length < part.Text.Length)
            {
                var boundary = part.Text.LastIndexOfAny([' ', '\n'], start + length - 1, length);
                if (boundary > start) length = boundary - start;
                else
                {
                    yield return part with { Quality = "Duvidoso", Reason = "OVERSIZE_TEXT_UNIT" };
                    yield break;
                }
            }
            yield return part with { Locator = $"{part.Locator}, caracteres {start + 1}–{start + length}", Text = part.Text.Substring(start, length) };
            start += length;
        }
    }
}
