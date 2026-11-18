using System.Globalization;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using SkiaSharp;

namespace JuridicoAI.Api.Application.Services;

public sealed class PecaDocxGenerator
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace PIC = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    private static string V(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);
    private static int Twips(double mm) => (int)Math.Ceiling(mm * 1440 / 25.4);
    private static XElement E(string name, params object?[] children) => new(W + name, children);
    private static XElement Val(string name, object value) => E(name, new XAttribute(W + "val", value));
    private static RequisicaoInvalidaException Invalid(string message) => new(message);
    private static string Str(JsonElement node, string name, string fallback = "") => node.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() ?? fallback : fallback;
    private static double Num(JsonElement node, string name, double fallback) => node.TryGetProperty(name, out var value) ? value.GetDouble() : fallback;
    private static JsonElement Attrs(JsonElement node) => node.TryGetProperty("attrs", out var value) && value.ValueKind == JsonValueKind.Object ? value : Empty;
    private static readonly JsonElement Empty = JsonSerializer.SerializeToElement(new { });
    private static IEnumerable<JsonElement> Children(JsonElement node) => node.TryGetProperty("content", out var content) ? content.EnumerateArray() : [];

    public byte[] Generate(Peca peca, CancellationToken ct = default)
    {
        try
        {
            using var stream = new MemoryStream();
            using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
                new Builder(package, peca, ct).Build();
            return stream.ToArray();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or XmlException or OverflowException)
        { throw Invalid("A peça contém conteúdo ou configurações inválidas para exportação."); }
    }

    private sealed class Builder
    {
        private readonly WordprocessingDocument package;
        private readonly MainDocumentPart main;
        private readonly Peca peca;
        private readonly CancellationToken ct;
        private readonly string font;
        private readonly double size, lineHeight, widthMm, left, right, top, bottom;
        private readonly string alignment;
        private readonly XElement numbering = E("numbering");
        private int numberId, visited;

        public Builder(WordprocessingDocument package, Peca peca, CancellationToken ct)
        {
            this.package = package; this.peca = peca; this.ct = ct;
            main = package.AddMainDocumentPart();
            var styles = peca.Styles.RootElement;
            font = Str(styles, "fontFamily", "Times New Roman");
            if (string.IsNullOrWhiteSpace(font) || font.Length > 100) throw Invalid("Fonte da peça inválida.");
            size = Range(Num(styles, "fontSize", 12), 7, 72, "Tamanho da fonte");
            lineHeight = Range(Num(styles, "lineHeight", 1.5), 1, 3, "Entrelinha");
            alignment = Align(Str(styles, "textAlign", "justify"));
            var page = peca.PageConfig.RootElement;
            if (Str(page, "format", "A4") != "A4") throw Invalid("A exportação suporta apenas páginas A4.");
            left = Range(Num(page, "marginLeft", 30), 0, 200, "Margem esquerda");
            right = Range(Num(page, "marginRight", 20), 0, 200, "Margem direita");
            top = Range(Num(page, "marginTop", 30), 0, 290, "Margem superior");
            bottom = Range(Num(page, "marginBottom", 20), 0, 290, "Margem inferior");
            widthMm = 210 - left - right;
            if (widthMm < 20) throw Invalid("As margens não deixam largura suficiente para exportar a peça.");
        }

        public void Build()
        {
            var body = E("body");
            if (Str(peca.ConteudoJson.RootElement, "type") != "doc") throw Invalid("O conteúdo da peça não é um documento válido.");
            Blocks(Children(peca.ConteudoJson.RootElement), body, 0, 0);
            if (!body.HasElements) body.Add(Paragraph([], 0, 0));
            var section = E("sectPr");
            var headerHeight = Header(section, out var headerOffset);
            var footerHeight = Footer(section, out var footerOffset);
            var reservedTop = Math.Max(top, headerHeight > 0 ? headerOffset + headerHeight : 0);
            var reservedBottom = Math.Max(bottom, footerHeight > 0 ? footerOffset + footerHeight + 4 : 0);
            if (297 - reservedTop - reservedBottom < 25) throw Invalid("Cabeçalho, rodapé e margens não deixam espaço suficiente para o corpo da peça.");
            section.Add(E("pgSz", new XAttribute(W + "w", Twips(210)), new XAttribute(W + "h", Twips(297))),
                E("pgMar", new XAttribute(W + "top", Twips(reservedTop)), new XAttribute(W + "right", Twips(right)),
                    new XAttribute(W + "bottom", Twips(reservedBottom)), new XAttribute(W + "left", Twips(left)),
                    new XAttribute(W + "header", Twips(headerOffset)), new XAttribute(W + "footer", Twips(footerOffset)), new XAttribute(W + "gutter", 0)));
            body.Add(section);
            Write(main, E("document", new XAttribute(XNamespace.Xmlns + "r", R), body));
            Write(main.AddNewPart<NumberingDefinitionsPart>(), numbering);
            Styles();
            Write(main.AddNewPart<DocumentSettingsPart>(), E("settings", E("compat", E("compatSetting", new XAttribute(W + "name", "compatibilityMode"), new XAttribute(W + "uri", "http://schemas.microsoft.com/office/word"), new XAttribute(W + "val", "15")))));
            package.PackageProperties.Title = peca.Titulo;
            package.PackageProperties.Creator = "JuridicoAI";
        }

        private void Check(int depth)
        {
            ct.ThrowIfCancellationRequested();
            if (depth > 24 || ++visited > 30000) throw Invalid("O documento excede o limite de complexidade para exportação.");
        }

        private void Blocks(IEnumerable<JsonElement> nodes, XElement target, int depth, int quote, int? numId = null, int listLevel = 0)
        {
            foreach (var node in nodes)
            {
                Check(depth);
                var type = Str(node, "type");
                switch (type)
                {
                    case "paragraph":
                    case "heading":
                    case "codeBlock":
                        target.Add(Paragraph(node, quote, numId, listLevel));
                        numId = null; // Somente o primeiro parágrafo do item recebe marcador.
                        break;
                    case "blockquote": Blocks(Children(node), target, depth + 1, quote + 1, numId, listLevel); numId = null; break;
                    case "bulletList":
                    case "orderedList":
                        if (listLevel > 8) throw Invalid("A lista excede os nove níveis suportados na exportação.");
                        var id = AddNumbering(type == "orderedList", listLevel, (int)Range(Num(Attrs(node), "start", 1), 1, 100000, "Início da lista"));
                        foreach (var item in Children(node))
                        {
                            Check(depth + 1);
                            if (Str(item, "type") != "listItem") throw Invalid("A lista contém um item inválido.");
                            // Listas aninhadas avançam um nível; parágrafos continuam no nível do item.
                            var first = true;
                            foreach (var child in Children(item))
                            {
                                var nested = Str(child, "type") is "orderedList" or "bulletList";
                                Blocks([child], target, depth + 2, quote, nested ? null : first ? id : -id, nested ? listLevel + 1 : listLevel);
                                if (!nested) first = false;
                            }
                        }
                        numId = null;
                        break;
                    case "horizontalRule":
                        target.Add(E("p", E("pPr", E("pBdr", Border("bottom", "66717D"))))); break;
                    default: throw Invalid($"O elemento '{type}' ainda não é suportado na exportação.");
                }
            }
        }

        private XElement Paragraph(JsonElement node, int quote, int? numId, int listLevel = 0)
        {
            var type = Str(node, "type"); var attrs = Attrs(node);
            var level = type == "heading" ? (int)Range(Num(attrs, "level", 2), 1, 6, "Nível do título") : 0;
            var props = E("pPr", Val("pStyle", level > 0 ? "Heading" + level : "Normal"));
            if (numId is > 0) props.Add(E("numPr", Val("ilvl", listLevel), Val("numId", numId)));
            var indent = quote * 600 + (numId is not null ? (listLevel + 1) * 720 : 0);
            if (indent > 0) props.Add(E("ind", new XAttribute(W + "left", indent), numId is > 0 ? new XAttribute(W + "hanging", 360) : null));
            var align = Str(attrs, "textAlign");
            if (align.Length > 0) props.Add(Val("jc", Align(align)));
            if (type == "codeBlock") { props.Elements(W + "jc").Remove(); props.Add(Val("jc", "left"), E("shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "fill", "F4F6F8"))); }
            var paragraph = E("p", props);
            foreach (var child in Children(node)) Inline(child, paragraph, 0, type == "codeBlock", quote > 0);
            return paragraph;
        }
        private XElement Paragraph(IEnumerable<XElement> runs, double fontSize, double after) => E("p", E("pPr", E("spacing", new XAttribute(W + "before", 0), new XAttribute(W + "after", Twips(after)), new XAttribute(W + "line", V((fontSize > 0 ? fontSize : size) * 1.3 * 20)), new XAttribute(W + "lineRule", "atLeast"))), runs);

        private void Inline(JsonElement node, XElement paragraph, int depth, bool code, bool quote)
        {
            Check(depth);
            var type = Str(node, "type");
            if (type == "hardBreak") { paragraph.Add(E("r", E("br"))); return; }
            if (type != "text") throw Invalid($"O elemento de texto '{type}' ainda não é suportado na exportação.");
            var text = Str(node, "text");
            if (text.Length > 1_000_000) throw Invalid("Um trecho da peça excede o limite de texto para exportação.");
            XmlConvert.VerifyXmlChars(text);
            var properties = E("rPr");
            string? link = null;
            if (node.TryGetProperty("marks", out var marks)) foreach (var mark in marks.EnumerateArray())
            {
                switch (Str(mark, "type"))
                {
                    case "bold": properties.Add(E("b")); break;
                    case "italic": properties.Add(E("i")); break;
                    case "underline": properties.Add(Val("u", "single")); break;
                    case "strike": properties.Add(E("strike")); break;
                    case "code": code = true; break;
                    case "link": link = Str(Attrs(mark), "href"); break;
                    default: throw Invalid($"A formatação '{Str(mark, "type")}' ainda não é suportada na exportação.");
                }
            }
            if (code) properties.AddFirst(E("rFonts", new XAttribute(W + "ascii", "Courier New"), new XAttribute(W + "hAnsi", "Courier New")));
            if (quote) properties.Add(Val("color", "596675"));
            if (link is not null)
            {
                if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "mailto" or "tel")) throw Invalid("A peça contém um link inválido para exportação.");
                properties.Add(Val("color", "156B8D"));
                if (properties.Element(W + "u") is null) properties.Add(Val("u", "single"));
                var relation = main.AddHyperlinkRelationship(uri, true);
                paragraph.Add(new XElement(W + "hyperlink", new XAttribute(R + "id", relation.Id), Run(text, properties)));
            }
            else paragraph.Add(Run(text, properties));
        }

        private static XElement Run(string text, XElement? properties = null)
        {
            var run = E("r", properties);
            // Textos legados podem guardar quebras dentro de um nó de texto.
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++) { if (i > 0) run.Add(E("br")); run.Add(E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), lines[i])); }
            return run;
        }

        private int AddNumbering(bool ordered, int level, int start)
        {
            var id = ++numberId;
            var definition = E("abstractNum", new XAttribute(W + "abstractNumId", id), Val("multiLevelType", "multilevel"));
            for (var i = 0; i < 9; i++) definition.Add(E("lvl", new XAttribute(W + "ilvl", i), Val("start", start), Val("numFmt", ordered ? "decimal" : "bullet"), Val("lvlText", ordered ? $"%{i + 1}." : "•"), Val("lvlJc", "left"), E("pPr", E("tabs", E("tab", new XAttribute(W + "val", "num"), new XAttribute(W + "pos", (i + 1) * 720))), E("ind", new XAttribute(W + "left", (i + 1) * 720), new XAttribute(W + "hanging", 360)))));
            // Todas as definições abstratas precedem as instâncias de numeração.
            var firstNumber = numbering.Element(W + "num"); if (firstNumber is null) numbering.Add(definition); else firstNumber.AddBeforeSelf(definition);
            numbering.Add(E("num", new XAttribute(W + "numId", id), Val("abstractNumId", id)));
            return id;
        }

        private double Header(XElement section, out double offset)
        {
            offset = 5;
            if (peca.HeaderJson is null) return 0;
            var node = peca.HeaderJson.RootElement;
            DocumentHeaderValidator.Validate(node);
            var text = PlainText(node, false);
            var root = E("hdr");
            var height = 0d;
            if (node.TryGetProperty("presentation", out var settings) && settings.ValueKind == JsonValueKind.Object && Str(settings, "mode") is "banner" or "logo")
            {
                var source = Str(settings, "imageDataUrl");
                var bytes = Convert.FromBase64String(source[(source.IndexOf(',') + 1)..]);
                if (source.StartsWith("data:image/png;", StringComparison.Ordinal)) ValidatePng(bytes);
                using var data = SKData.CreateCopy(bytes); using var codec = SKCodec.Create(data);
                if (codec is null) throw Invalid("A imagem de cabeçalho não pôde ser aberta.");
                using var decoded = new SKBitmap(codec.Info);
                if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success) throw Invalid("A imagem de cabeçalho está corrompida. Envie novamente uma imagem válida no modelo.");
                var width = Math.Min(widthMm, Num(settings, "widthMm", 150));
                var imageHeight = width * codec.Info.Height / codec.Info.Width;
                var max = Num(settings, "maxHeightMm", 30);
                if (imageHeight > max) { width *= max / imageHeight; imageHeight = max; }
                offset = Num(settings, "offsetTopMm", 5);
                var part = main.AddNewPart<HeaderPart>();
                var image = part.AddImagePart(source.StartsWith("data:image/png;", StringComparison.Ordinal) ? ImagePartType.Png : ImagePartType.Jpeg);
                using (var input = new MemoryStream(bytes)) image.FeedData(input);
                var paragraph = E("p", E("pPr", E("spacing", new XAttribute(W + "before", 0), new XAttribute(W + "after", 0)), Val("jc", Align(Str(settings, "align", "center")))), E("r", Drawing(part.GetIdOfPart(image), width, imageHeight)));
                root.Add(paragraph);
                height = imageHeight + 1.5;
                if (!string.IsNullOrWhiteSpace(text)) { root.Add(BandParagraph(text, 7.5, "66717D", Str(settings, "align", "center"))); height += TextHeight(text, 7.5); }
                height += Num(settings, "gapMm", 5);
                Write(part, root); section.Add(E("headerReference", new XAttribute(W + "type", "default"), new XAttribute(R + "id", main.GetIdOfPart(part))));
                return height;
            }
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var legacy = main.AddNewPart<HeaderPart>(); root.Add(BandParagraph(text, 7.5, "66717D", "center"));
            Write(legacy, root); section.Add(E("headerReference", new XAttribute(W + "type", "default"), new XAttribute(R + "id", main.GetIdOfPart(legacy))));
            return TextHeight(text, 7.5) + 5;
        }

        private double Footer(XElement section, out double offset)
        {
            offset = 5;
            if (peca.FooterJson is null) return 0;
            var node = peca.FooterJson.RootElement; DocumentFooterValidator.Validate(node);
            var text = PlainText(node, true); if (string.IsNullOrWhiteSpace(text)) return 0;
            var configured = node.TryGetProperty("presentation", out var raw) && raw.ValueKind == JsonValueKind.Object;
            var settings = configured ? raw : Empty;
            var fontSize = Num(settings, "fontSize", configured ? 9 : 7.5);
            offset = Num(settings, "offsetBottomMm", configured ? 5 : 20 * 25.4 / 96);
            var separator = settings.TryGetProperty("separator", out var divider) && divider.GetBoolean();
            var paragraph = BandParagraph(text, fontSize, Str(settings, "color", "#66717d").TrimStart('#'), Str(settings, "align", "center"));
            if (separator) paragraph.Element(W + "pPr")!.AddFirst(E("pBdr", Border("top", Str(settings, "color", "#66717d").TrimStart('#'), 6)));
            var part = main.AddNewPart<FooterPart>(); Write(part, E("ftr", paragraph));
            // Referências de footer vêm depois das referências de header.
            section.Add(E("footerReference", new XAttribute(W + "type", "default"), new XAttribute(R + "id", main.GetIdOfPart(part))));
            return TextHeight(text, fontSize) + (separator ? 3 : 0);
        }

        private XElement BandParagraph(string text, double fontSize, string color, string align)
        {
            var paragraph = Paragraph([Run(text, E("rPr", Val("color", color.ToUpperInvariant()), Val("sz", V(fontSize * 2))))], fontSize, 0);
            paragraph.Element(W + "pPr")!.Add(Val("jc", Align(align)));
            return paragraph;
        }

        private double TextHeight(string text, double fontSize)
        {
            using var typeface = SKTypeface.FromFamilyName(font);
            using var measure = new SKFont(typeface, (float)fontSize);
            var maxWidthPt = widthMm * 72 / 25.4;
            var lines = 0;
            foreach (var source in text.Replace("\r\n", "\n").Split('\n'))
            {
                var current = new StringBuilder(); lines++;
                foreach (var rune in source.EnumerateRunes())
                {
                    current.Append(rune.ToString());
                    // Margem conservadora para diferenças de kerning e quebra por palavras.
                    if (measure.MeasureText(current.ToString()) > maxWidthPt * .88) { lines++; current.Clear(); current.Append(rune.ToString()); }
                }
            }
            var linePoints = Math.Max(fontSize * 1.3, measure.Metrics.Descent - measure.Metrics.Ascent);
            return (lines * linePoints + 2) * 25.4 / 72;
        }

        private static XElement Drawing(string id, double width, double height)
        {
            var cx = V(width * 36000); var cy = V(height * 36000);
            return E("drawing", new XElement(WP + "inline", new XAttribute("distT", 0), new XAttribute("distB", 0), new XAttribute("distL", 0), new XAttribute("distR", 0),
                new XElement(WP + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
                new XElement(WP + "docPr", new XAttribute("id", 1), new XAttribute("name", "Cabeçalho"), new XAttribute("descr", "Imagem de cabeçalho do modelo")),
                new XElement(A + "graphic", new XElement(A + "graphicData", new XAttribute("uri", PIC.NamespaceName),
                    new XElement(PIC + "pic", new XElement(PIC + "nvPicPr", new XElement(PIC + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "Cabeçalho")), new XElement(PIC + "cNvPicPr")),
                        new XElement(PIC + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", id)), new XElement(A + "stretch", new XElement(A + "fillRect"))),
                        new XElement(PIC + "spPr", new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))),
                            new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst"))))))));
        }

        private void Styles()
        {
            var root = E("styles", E("docDefaults", E("rPrDefault", E("rPr", E("rFonts", new XAttribute(W + "ascii", font), new XAttribute(W + "hAnsi", font)), Val("color", "20242A"), Val("sz", V(size * 2)), Val("lang", "pt-BR")))));
            root.Add(E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "default", "1"), new XAttribute(W + "styleId", "Normal"), Val("name", "Normal"),
                E("pPr", E("widowControl"), E("spacing", new XAttribute(W + "before", V(size * .45 * 20)), new XAttribute(W + "after", V(size * .45 * 20)), new XAttribute(W + "line", V(lineHeight * 240)), new XAttribute(W + "lineRule", "auto")), Val("jc", alignment))));
            for (var level = 1; level <= 6; level++) root.Add(E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", "Heading" + level), Val("name", "heading " + level), Val("basedOn", "Normal"), Val("next", "Normal"), E("qFormat"),
                E("pPr", E("keepNext"), E("keepLines"), E("spacing", new XAttribute(W + "before", V(size * 1.5 * 20)), new XAttribute(W + "after", V(size * .7 * 20))), Val("jc", level == 2 ? "center" : alignment), Val("outlineLvl", level - 1)),
                E("rPr", E("b"), level == 2 ? E("caps") : null, Val("color", "244F63"), Val("sz", V(size * (level == 2 ? 1.2 : 1.05) * 2)))));
            Write(main.AddNewPart<StyleDefinitionsPart>(), root);
        }

        private static XElement Border(string edge, string color, int space = 0) => E(edge, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 6), new XAttribute(W + "space", space), new XAttribute(W + "color", color));
        private static void ValidatePng(byte[] bytes)
        {
            for (var offset = 8; offset < bytes.Length;)
            {
                if (offset + 12 > bytes.Length) throw Invalid("Imagem PNG de cabeçalho corrompida.");
                var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
                if (length < 0 || (long)offset + length + 12 > bytes.Length) throw Invalid("Imagem PNG de cabeçalho corrompida.");
                uint crc = uint.MaxValue;
                foreach (var b in bytes.AsSpan(offset + 4, length + 4))
                { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0); }
                if ((crc ^ uint.MaxValue) != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + length, 4))) throw Invalid("Imagem PNG de cabeçalho corrompida. Envie novamente uma imagem válida no modelo.");
                offset += length + 12;
            }
        }
        private static string Align(string value) => value switch { "justify" => "both", "left" or "right" or "center" => value, _ => throw Invalid("Alinhamento inválido na peça.") };
        private static double Range(double value, double min, double max, string label) => double.IsFinite(value) && value >= min && value <= max ? value : throw Invalid($"{label}: valor inválido para exportação.");
        private static void Write(OpenXmlPart part, XElement root)
        {
            // OOXML exige uma sequência específica para as propriedades.
            Order(root, "pPr", "pStyle keepNext keepLines pageBreakBefore framePr widowControl numPr suppressLineNumbers pBdr shd tabs suppressAutoHyphens kinsoku wordWrap overflowPunct topLinePunct autoSpaceDE autoSpaceDN bidi adjustRightInd snapToGrid spacing ind contextualSpacing mirrorIndents suppressOverlap jc textDirection textAlignment textboxTightWrap outlineLvl divId cnfStyle rPr sectPr pPrChange");
            Order(root, "rPr", "rStyle rFonts b bCs i iCs caps smallCaps strike dstrike outline shadow emboss imprint noProof snapToGrid vanish webHidden color spacing w kern position sz szCs highlight u effect bdr shd fitText vertAlign rtl cs em lang eastAsianLayout specVanish oMath rPrChange");
            using var data = new MemoryStream(Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting)));
            part.FeedData(data);
        }
        private static void Order(XElement root, string property, string names)
        {
            var sequence = names.Split(' ');
            foreach (var element in root.Descendants(W + property))
                element.ReplaceNodes(element.Elements().GroupBy(x => x.Name).Select(g => g.Last()).OrderBy(x => Array.IndexOf(sequence, x.Name.LocalName)).ToArray());
        }
        private static string PlainText(JsonElement node, bool multiline, int depth = 0)
        {
            if (depth > 24) throw Invalid("Cabeçalho ou rodapé excede o limite de complexidade.");
            var type = Str(node, "type");
            if (type.Length == 0 && !node.TryGetProperty("content", out _)) return "";
            if (type == "text") return Str(node, "text");
            if (type == "hardBreak") return "\n";
            if (type is not ("doc" or "paragraph")) throw Invalid($"O elemento '{type}' não é suportado no cabeçalho ou rodapé.");
            return string.Join(type == "doc" ? multiline ? "\n" : " " : "", Children(node).Select(child => PlainText(child, multiline, depth + 1)));
        }
    }
}
