using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace JuridicoAI.Api.Infrastructure.Documents;

public sealed class LocalDocumentExtractor(DocumentProcess process, IOptions<AssistenteDocumentSettings> options)
{
    private AssistenteDocumentSettings Settings => options.Value;
    public async IAsyncEnumerable<ExtractedPart> ExtractAsync(string path, string format,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var count = 0;
        await foreach (var part in ReadAsync(path, format, ct))
        {
            count += part.Text.Length;
            if (count > Settings.MaxExtractedCharacters) throw DocumentProcess.Error("AI_DOCUMENT_TEXT_LIMIT");
            foreach (var chunk in DocumentQuality.Chunks(part)) yield return chunk;
        }
    }
    private async IAsyncEnumerable<ExtractedPart> ReadAsync(string path, string format,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (format == "txt")
        {
            var bytes = await File.ReadAllBytesAsync(path, ct);
            var start = bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? 3 : 0;
            var text = new UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start);
            var lines = Regex.Split(text, @"\r?\n\s*\r?\n");
            foreach (var (line, index) in lines.Select((s, i) => (s, i)))
                yield return DocumentQuality.Native(line, null, $"parágrafo {index + 1}");
            yield break;
        }
        if (format == "docx")
        {
            ValidateZip(path);
            using var document = WordprocessingDocument.Open(path, false, new OpenSettings { MaxCharactersInPart = Settings.MaxExtractedCharacters });
            var main = document.MainDocumentPart ?? throw DocumentProcess.Error("AI_DOCUMENT_PARSE_FAILED");
            var body = main.Document?.Body ?? throw DocumentProcess.Error("AI_DOCUMENT_PARSE_FAILED");
            var index = 0;
            foreach (var element in body.ChildElements)
            {
                ct.ThrowIfCancellationRequested(); index++;
                if (element is Paragraph paragraph)
                    yield return DocumentQuality.Native(ParagraphText(paragraph), null, $"parágrafo {index}",
                        paragraph.Descendants<FieldCode>().Any() || paragraph.Descendants<SimpleField>().Any() ? "DYNAMIC_FIELD_NOT_EVALUATED" : null);
                else if (element is Table table)
                {
                    var uncertain = table.Descendants<GridSpan>().Any() || table.Descendants<VerticalMerge>().Any() || table.Descendants<Table>().Any();
                    var row = 0;
                    foreach (var r in table.Elements<TableRow>())
                        yield return DocumentQuality.Native(string.Join(" | ", r.Elements<TableCell>().Select(c => string.Join(" / ", c.Elements<Paragraph>().Select(ParagraphText)))),
                            null, $"tabela {index}, linha {++row}", uncertain ? "TABLE_CELL_ASSOCIATION_UNCERTAIN" : null);
                }
            }
            var imageIndex = 0;
            foreach (var image in main.ImageParts)
            {
                var imagePath = Path.Combine(dir, $"embedded-{++imageIndex}.image");
                await using (var output = File.Create(imagePath))
                await using (var input = image.GetStream()) await input.CopyToAsync(output, ct);
                foreach (var part in await OcrAsync(imagePath, null, $"imagem incorporada {imageIndex}", ct)) yield return part;
            }
            // Headers/footers are separate sources of text, with explicit locations.
            foreach (var p in main.HeaderParts.SelectMany(h => h.Header?.Descendants<Paragraph>() ?? []))
                yield return DocumentQuality.Native(ParagraphText(p), null, "cabeçalho");
            foreach (var p in main.FooterParts.SelectMany(h => h.Footer?.Descendants<Paragraph>() ?? []))
                yield return DocumentQuality.Native(ParagraphText(p), null, "rodapé");
            if (main.FootnotesPart is not null || main.EndnotesPart is not null)
                yield return new(null, "notas", "", "Duvidoso", "FOOTNOTES_NOT_COVERED");
            yield break;
        }
        if (format is "png" or "jpg" or "jpeg")
        {
            foreach (var part in await OcrAsync(path, 1, "imagem", ct)) yield return part;
            yield break;
        }
        if (format != "pdf") throw DocumentProcess.Error("AI_DOCUMENT_FORMAT_UNSUPPORTED");
        var info = await process.RunAsync(Settings.PdfInfoPath, [path], dir, Settings.ProcessTimeoutSeconds, ct);
        if (Regex.IsMatch(info, @"Encrypted:\s+yes", RegexOptions.IgnoreCase)) throw DocumentProcess.Error("AI_DOCUMENT_ENCRYPTED");
        var pageMatch = Regex.Match(info, @"Pages:\s+(\d+)");
        if (!pageMatch.Success || !int.TryParse(pageMatch.Groups[1].Value, out var pages)) throw DocumentProcess.Error("AI_DOCUMENT_PARSE_FAILED");
        if (pages > Settings.MaxPages) throw DocumentProcess.Error("AI_DOCUMENT_PAGE_LIMIT");
        var images = await process.RunAsync(Settings.PdfImagesPath, ["-list", path], dir, Settings.ProcessTimeoutSeconds, ct);
        var imagePages = images.Split('\n').Select(l => Regex.Match(l, @"^\s*(\d+)\s+\d+\s+"))
            .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();
        for (var page = 1; page <= pages; page++)
        {
            // bbox-layout records line/word coordinates; DTD is never resolved.
            var xml = await process.RunAsync(Settings.PdfTextPath, ["-f", page.ToString(), "-l", page.ToString(), "-bbox-layout", "-enc", "UTF-8", path, "-"], dir, Settings.ProcessTimeoutSeconds, ct);
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
            var doc = XDocument.Load(reader);
            var lines = doc.Descendants().Where(x => x.Name.LocalName == "line").ToArray();
            if (lines.Length > 5000) throw DocumentProcess.Error("AI_DOCUMENT_LAYOUT_LIMIT");
            var hasText = lines.Any(l => l.Descendants().Any(w => w.Name.LocalName == "word" && !string.IsNullOrWhiteSpace(w.Value)));
            if (imagePages.Contains(page) || !hasText)
            {
                if (!Settings.OcrEnabled)
                {
                    // A mixed page cannot be claimed fully read from its text layer.
                    foreach (var part in NativePage(lines, page)) yield return part with { TotalPages = pages };
                    yield return new(page, $"página {page}, região gráfica", "", "Ilegivel", "OCR_NOT_CALIBRATED", TotalPages: pages);
                    continue;
                }
                var prefix = Path.Combine(dir, $"page-{page}");
                await process.RunAsync(Settings.PdfRenderPath, ["-f", page.ToString(), "-l", page.ToString(), "-singlefile", "-r", "300", "-scale-to", "5000", "-png", path, prefix], dir, Settings.ProcessTimeoutSeconds, ct);
                foreach (var part in await OcrAsync(prefix + ".png", page, $"página {page}", ct)) yield return part with { TotalPages = pages };
                File.Delete(prefix + ".png");
            }
            else foreach (var part in NativePage(lines, page)) yield return part with { TotalPages = pages };
        }
    }
    private static IEnumerable<ExtractedPart> NativePage(XElement[] lines, int page)
    {
        static double Number(XElement l, string attr) => double.TryParse(l.Attribute(attr)?.Value,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
        var ambiguous = lines.Any(a => lines.Any(b => a != b && Math.Abs(Number(a, "yMin") - Number(b, "yMin")) < 3 &&
            Number(a, "xMax") + 12 < Number(b, "xMin")));
        // Group by block: preserve sentence context instead of accepting words
        // independently, and reject parallel columns/tables conservatively.
        foreach (var group in lines.GroupBy(l => l.Parent))
        {
            var text = string.Join('\n', group.Select(l => string.Join(' ', l.Descendants().Where(w => w.Name.LocalName == "word").Select(w => w.Value))));
            yield return DocumentQuality.Native(text, page, $"página {page}, y={Number(group.First(), "yMin"):0.##}", ambiguous ? "READING_ORDER_UNCERTAIN" : null);
        }
    }
    private async Task<IReadOnlyList<ExtractedPart>> OcrAsync(string image, int? page, string locator, CancellationToken ct)
    {
        using var codec = SKCodec.Create(image);
        if (codec is null) throw DocumentProcess.Error("AI_DOCUMENT_IMAGE_INVALID");
        if ((long)codec.Info.Width * codec.Info.Height > Settings.MaxPixels) throw DocumentProcess.Error("AI_DOCUMENT_IMAGE_LIMIT");
        if (!Settings.OcrEnabled) return [new(page, locator, "", "Ilegivel", "OCR_NOT_CALIBRATED")];
        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null) throw DocumentProcess.Error("AI_DOCUMENT_IMAGE_INVALID");
        var origin = codec.EncodedOrigin;
        var swap = (int)origin >= 5;
        using var oriented = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
        using (var canvas = new SKCanvas(oriented))
        {
            canvas.Clear(SKColors.White);
            switch (origin)
            {
                case SKEncodedOrigin.TopRight: canvas.Translate(bitmap.Width, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: canvas.Translate(bitmap.Width, bitmap.Height); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Translate(0, bitmap.Height); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(bitmap.Height, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(bitmap.Height, bitmap.Width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, bitmap.Width); canvas.RotateDegrees(270); break;
            }
            canvas.DrawBitmap(bitmap, 0, 0);
        }
        var normalized = image + ".normalized.png";
        using (var data = oriented.Encode(SKEncodedImageFormat.Png, 100))
        await using (var output = File.Create(normalized)) data.SaveTo(output);
        var args = new List<string> { normalized, "stdout", "-l", "por+eng", "--psm", "3" };
        if (!string.IsNullOrEmpty(Settings.TessdataPath)) args.AddRange(["--tessdata-dir", Settings.TessdataPath]);
        args.Add("tsv");
        var tsv = await process.RunAsync(Settings.TesseractPath, args, Path.GetDirectoryName(image)!, Settings.ProcessTimeoutSeconds, ct);
        return DocumentQuality.OcrTsv(tsv, page, locator);
    }
    private static string ParagraphText(Paragraph p) => string.Concat(p.Descendants().Select(e => e switch
        { Text t => t.Text, TabChar => "\t", Break => "\n", _ => "" }));
    private static void ValidateZip(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > 10_000 || zip.Entries.Sum(e => e.Length) > 100L * 1024 * 1024)
            throw DocumentProcess.Error("AI_DOCUMENT_EXPANSION_LIMIT");
        if (zip.GetEntry("word/document.xml") is null || zip.Entries.Any(e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
            throw DocumentProcess.Error("AI_DOCUMENT_FORMAT_UNSUPPORTED");
    }
}
