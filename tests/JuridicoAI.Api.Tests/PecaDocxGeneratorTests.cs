using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using Xunit;
using SkiaSharp;
using System.Text.Json.Nodes;

namespace JuridicoAI.Api.Tests;

public sealed class PecaDocxGeneratorTests
{
    internal static Peca Fixture(bool image = true)
    {
        var content = new List<object>
        {
            new { type = "heading", attrs = new { level = 2 }, content = new[] { new { type = "text", text = "Petição fictícia para validação da exportação" } } },
            new { type = "paragraph", content = new object[] {
                new { type = "text", text = "Texto com acentuação: ação, obrigação e restituição. ", marks = new[] { new { type = "bold" }, new { type = "italic" }, new { type = "underline" }, new { type = "strike" } } },
                new { type = "text", text = "Referência oficial", marks = new[] { new { type = "link", attrs = new { href = "https://www.cnj.jus.br/" } } } }, new { type = "hardBreak" }, new { type = "text", text = "Segunda linha preservada." } } },
            new { type = "blockquote", content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = "Citação com conteúdo ilustrativo para verificar recuo e apresentação." } } } } },
            new { type = "orderedList", attrs = new { start = 3 }, content = new object[] {
                new { type = "listItem", content = new object[] { new { type = "paragraph", content = new[] { new { type = "text", text = "Primeiro pedido" } } }, new { type = "bulletList", content = new[] { new { type = "listItem", content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = "Subitem com marcador" } } } } } } }, new { type = "paragraph", content = new[] { new { type = "text", text = "Continuação do primeiro item sem novo marcador." } } } } },
                new { type = "listItem", content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = "Segundo pedido" } } } } } } },
            new { type = "codeBlock", content = new[] { new { type = "text", text = "protocolo = TESTE-001\nvalor = 149,90" } } },
            new { type = "horizontalRule" },
        };
        for (var i = 1; i <= 35; i++) content.Add(new { type = "paragraph", content = new[] { new { type = "text", text = $"Parágrafo {i:00}. A autora apresenta os fatos exclusivamente fictícios deste documento de teste. A exportação deve preservar todos os trechos, os acentos e a sequência dos pedidos, sem sobreposição com a identidade visual do escritório. O documento permite conferir a continuidade do texto entre páginas." } } });
        return new Peca
        {
            Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Titulo = "Peça fictícia de exportação", Versao = 1,
            ConteudoJson = JsonSerializer.SerializeToDocument(new { type = "doc", content }),
            PageConfig = JsonDocument.Parse("""{"format":"A4","marginTop":30,"marginBottom":20,"marginLeft":30,"marginRight":20}"""),
            Styles = JsonDocument.Parse("""{"fontFamily":"Times New Roman","fontSize":12,"lineHeight":1.5,"textAlign":"justify"}"""),
            HeaderJson = image ? JsonSerializer.SerializeToDocument(new { type = "doc", content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = "ESCRITÓRIO DE DEMONSTRAÇÃO" } } } }, presentation = new { mode = "banner", imageDataUrl = Image(), widthMm = 150, maxHeightMm = 25, align = "center", offsetTopMm = 5, gapMm = 5 } }) : JsonDocument.Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"ESCRITÓRIO DE DEMONSTRAÇÃO"}]}]}"""),
            FooterJson = JsonDocument.Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Escritório fictício · OAB/SP 000000"}]},{"type":"paragraph","content":[{"type":"text","text":"Rua Exemplo, 100 · contato@example.test"}]}],"presentation":{"fontSize":9,"color":"#163255","align":"center","separator":true,"offsetBottomMm":5}}"""),
        };
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GeraPacoteValidoComTextoEditavelFormatacaoENumeracao(bool image)
    {
        var peca = Fixture(image); var original = peca.ConteudoJson.RootElement.GetRawText();
        var bytes = new PecaDocxGenerator().Generate(peca);
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var errors = new OpenXmlValidator().Validate(doc).Select(x => x.Description + " " + x.Path?.XPath).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        var main = doc.MainDocumentPart!;
        Assert.Contains("ação, obrigação", main.Document!.InnerText);
        Assert.Contains("Parágrafo 35", main.Document.InnerText);
        Assert.Single(main.HeaderParts); Assert.Single(main.FooterParts);
        Assert.Equal(image, main.HeaderParts.Single().ImageParts.Any());
        Assert.Contains("contato@example.test", main.FooterParts.Single().Footer!.InnerText);
        Assert.Equal(2, main.NumberingDefinitionsPart!.Numbering!.Elements<DocumentFormat.OpenXml.Wordprocessing.NumberingInstance>().Count());
        Assert.Single(main.HyperlinkRelationships);
        Assert.Equal(original, peca.ConteudoJson.RootElement.GetRawText()); Assert.Equal(1, peca.Versao);
        if (Environment.GetEnvironmentVariable("JURIDICOAI_EXPORT_QA_DIR") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, image ? "imagem.docx" : "textual.docx"), bytes);
            File.WriteAllText(Path.Combine(directory, image ? "imagem.json" : "textual.json"), JsonSerializer.Serialize(peca, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }

    private static string Image(bool square = false)
    {
        using var bitmap = new SKBitmap(square ? 300 : 900, square ? 300 : 130);
        using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColor.Parse("#163255"));
        using var font = new SKFont(SKTypeface.FromFamilyName("Liberation Serif"), 48);
        using var paint = new SKPaint { Color = SKColor.Parse("#D0B06B"), IsAntialias = true };
        canvas.DrawText(square ? "AH" : "ESCRITÓRIO DE DEMONSTRAÇÃO", 25, square ? 160 : 80, font, paint);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }

    [Fact]
    public void ReservaAreaParaCabecalhoERodapeProximosDosLimitesPermitidos()
    {
        var peca = Fixture();
        peca.HeaderJson = JsonSerializer.SerializeToDocument(new { type = "doc", content = Array.Empty<object>(), presentation = new { mode = "logo", imageDataUrl = Image(true), widthMm = 210, maxHeightMm = 50, align = "right", offsetTopMm = 30, gapMm = 20 } });
        peca.FooterJson = JsonSerializer.SerializeToDocument(new { type = "doc", content = Enumerable.Range(1, 14).Select(i => new { type = "paragraph", content = new[] { new { type = "text", text = $"Rodapé de teste — linha {i:00} — contato@example.test" } } }), presentation = new { fontSize = 16, color = "#163255", align = "left", separator = true, offsetBottomMm = 30 } });
        var content = JsonNode.Parse(peca.ConteudoJson.RootElement.GetRawText())!;
        var blocks = content["content"]!.AsArray(); while (blocks.Count > 2) blocks.RemoveAt(blocks.Count - 1);
        peca.ConteudoJson = JsonDocument.Parse(content.ToJsonString());
        var bytes = new PecaDocxGenerator().Generate(peca);
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Empty(new OpenXmlValidator().Validate(doc));
        var margins = doc.MainDocumentPart!.Document!.Descendants<DocumentFormat.OpenXml.Wordprocessing.PageMargin>().Single();
        Assert.True(margins.Top!.Value > 5000); Assert.True(margins.Bottom!.Value > 7000);
        if (Environment.GetEnvironmentVariable("JURIDICOAI_EXPORT_QA_DIR") is { Length: > 0 } directory)
        { Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "limites.docx"), bytes); }
    }

    [Fact]
    public void RecusaNoOuMarcaNaoSuportadosSemDescartarConteudo()
    {
        var peca = Fixture();
        peca.ConteudoJson = JsonDocument.Parse("""{"type":"doc","content":[{"type":"table"}]}""");
        Assert.Throws<RequisicaoInvalidaException>(() => new PecaDocxGenerator().Generate(peca));
        peca.ConteudoJson = JsonDocument.Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Teste","marks":[{"type":"textStyle"}]}]}]}""");
        Assert.Throws<RequisicaoInvalidaException>(() => new PecaDocxGenerator().Generate(peca));
    }

    [Fact]
    public void RecusaPngComChecksumInvalidoEmVezDeGerarArquivoSemImagem()
    {
        var peca = Fixture();
        peca.HeaderJson = JsonSerializer.SerializeToDocument(new { type = "doc", content = Array.Empty<object>(), presentation = new { mode = "logo", imageDataUrl = DocumentHeaderValidatorTests.Image } });
        Assert.Throws<RequisicaoInvalidaException>(() => new PecaDocxGenerator().Generate(peca));
    }

    [Fact]
    public void RecusaPaginaSemEspacoEExportaSemCabecalhoRodape()
    {
        var peca = Fixture(); peca.HeaderJson = null; peca.FooterJson = null;
        using var doc = WordprocessingDocument.Open(new MemoryStream(new PecaDocxGenerator().Generate(peca)), false);
        Assert.Empty(doc.MainDocumentPart!.HeaderParts); Assert.Empty(doc.MainDocumentPart.FooterParts);
        peca.HeaderJson = JsonDocument.Parse("{}"); peca.FooterJson = JsonDocument.Parse("{}");
        Assert.NotEmpty(new PecaDocxGenerator().Generate(peca));
        peca.PageConfig = JsonDocument.Parse("""{"marginTop":200,"marginBottom":200,"marginLeft":30,"marginRight":20}""");
        Assert.Throws<RequisicaoInvalidaException>(() => new PecaDocxGenerator().Generate(peca));
    }
}
