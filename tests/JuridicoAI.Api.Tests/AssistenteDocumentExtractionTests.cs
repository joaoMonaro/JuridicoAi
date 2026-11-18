using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using JuridicoAI.Api.Infrastructure.Documents;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class DocumentPdfFactAttribute : FactAttribute
{
    public DocumentPdfFactAttribute()
    { if (!File.Exists("/usr/bin/pdftotext")) Skip = "Requer ferramentas Poppler locais."; }
}
public sealed class DocumentOcrTheoryAttribute : TheoryAttribute
{
    public DocumentOcrTheoryAttribute()
    { if (!File.Exists(Environment.GetEnvironmentVariable("JURIDICOAI_TEST_TESSERACT"))) Skip = "Requer Tesseract local de teste e modelos por+eng."; }
}
public sealed class AssistenteDocumentExtractionTests
{
    [Fact]
    public void Qualidade_OcrReprovaUnidadeComValorDuvidosoSemRecortarPalavra()
    {
        var tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
            "5\t1\t1\t1\t1\t1\t0\t0\t20\t10\t99\tValor\n" +
            "5\t1\t1\t1\t1\t2\t21\t0\t20\t10\t96\t123,45\n";
        var part = Assert.Single(DocumentQuality.OcrTsv(tsv, 2, "página 2"));
        Assert.Equal("Duvidoso", part.Quality); Assert.Equal("Valor 123,45", part.Text);
        Assert.Contains("confidence", part.Metrics); Assert.Single(DocumentQuality.Chunks(part));
    }
    [Fact]
    public void Qualidade_OcrAceitaSomenteQuandoTodosOsCriteriosPassam()
    {
        var tsv = "header\n5\t1\t1\t1\t1\t1\t0\t0\t20\t10\t99\tBeatriz\n5\t1\t1\t1\t1\t2\t21\t0\t20\t10\t99\t123,45\n";
        Assert.Equal("Confiavel", Assert.Single(DocumentQuality.OcrTsv(tsv, 1, "imagem")).Quality);
        Assert.Equal("Ilegivel", Assert.Single(DocumentQuality.OcrTsv("header\n", 1, "imagem")).Quality);
    }
    [Fact]
    public async Task Txt_CodificacaoInvalidaNaoSubstituiCaracteresSilenciosamente()
    {
        using var temp = new Temp(); var path = Path.Combine(temp.Path, "invalid.txt");
        await File.WriteAllBytesAsync(path, [0xC3, 0x28]);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => Read(path, "txt"));
    }
    [Fact]
    public async Task Docx_ExtraiParagrafosETabelasComLocalizadoresSemInventarPaginas()
    {
        using var temp = new Temp(); var path = Path.Combine(temp.Path, "test.docx");
        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text("Beatriz pagou."))),
                new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("Ana")))),
                    new TableCell(new Paragraph(new Run(new Text("R$ 123,45"))))))));
        }
        var parts = await Read(path, "docx");
        Assert.Equal(2, parts.Count); Assert.All(parts, p => { Assert.Null(p.Page); Assert.Equal("Confiavel", p.Quality); });
        Assert.Equal("Ana | R$ 123,45", parts[1].Text); Assert.Contains("tabela", parts[1].Locator);
    }
    [DocumentPdfFact]
    public async Task Pdf_TextoNativoMantemCamposMateriaisELocalizacao()
    {
        var parts = await Read(Fixture("native.pdf"), "pdf");
        Assert.Contains(parts, p => p.Quality == "Confiavel" && p.Text.Contains("123,45") && p.Text.Contains("11/11/2026"));
        Assert.All(parts, p => Assert.Equal(1, p.Page));
    }
    [DocumentPdfFact]
    public async Task Pdf_MulticolunaNaoInventaAssociacoes()
    {
        var parts = await Read(Fixture("columns.pdf"), "pdf");
        Assert.NotEmpty(parts); Assert.All(parts, p => Assert.Equal("Duvidoso", p.Quality));
        Assert.All(parts, p => Assert.Equal("READING_ORDER_UNCERTAIN", p.Reason));
    }
    [DocumentPdfFact]
    public async Task Pdf_MistoNaoAfirmaLeituraIntegralQuandoOcrEstaDesligado()
    {
        var parts = await Read(Fixture("mixed.pdf"), "pdf");
        Assert.Contains(parts, p => p.Quality == "Confiavel" && p.Text.Contains("Texto nativo"));
        Assert.Contains(parts, p => p.Reason == "OCR_NOT_CALIBRATED");
        Assert.DoesNotContain(parts, p => p.Quality == "Confiavel" && p.Text.Contains("123,45"));
    }
    [DocumentPdfFact]
    public async Task Pdf_ProtegidoRetornaFalhaSemExporConteudo()
    {
        await Assert.ThrowsAsync<JuridicoAI.Api.Application.Exceptions.AssistenteIaException>(() => Read(Fixture("protected.pdf"), "pdf"));
    }
    [Fact]
    public async Task Imagem_SemCalibracaoSinalizaFaltaDeLeitura()
    {
        var part = Assert.Single(await Read(Fixture("scan.png"), "png"));
        Assert.Equal("OCR_NOT_CALIBRATED", part.Reason); Assert.Equal("", part.Text);
    }
    [DocumentOcrTheory]
    [InlineData("scan.png")]
    [InlineData("blurred.png")]
    [InlineData("rotated.png")]
    public async Task OcrLocal_ExecutaPipelineRealESinalizaBaixaConfianca(string name)
    {
        var parts = await Read(Fixture(name), "png", new AssistenteDocumentSettings
        {
            OcrEnabled = true, OcrCalibrationVersion = "synthetic-smoke-not-approved",
            TesseractPath = Environment.GetEnvironmentVariable("JURIDICOAI_TEST_TESSERACT")!,
            TessdataPath = Environment.GetEnvironmentVariable("JURIDICOAI_TEST_TESSDATA")!
        });
        Assert.NotEmpty(parts); Assert.All(parts, part => Assert.NotEqual("Confiavel", part.Quality));
        Assert.Contains(parts, part => part.Reason is "OCR_CONFIDENCE_INSUFFICIENT" or "NO_READABLE_TEXT");
    }
    private static string Fixture(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "Documents", name);
    private static async Task<List<ExtractedPart>> Read(string input, string format, AssistenteDocumentSettings? settings = null)
    {
        using var temp = new Temp(); var path = System.IO.Path.Combine(temp.Path, "source." + format); File.Copy(input, path);
        var extractor = new LocalDocumentExtractor(new DocumentProcess(), Options.Create(settings ?? new AssistenteDocumentSettings()));
        var result = new List<ExtractedPart>(); await foreach (var part in extractor.ExtractAsync(path, format, default)) result.Add(part); return result;
    }
    private sealed class Temp : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "juridicoai-fixture-" + Guid.NewGuid().ToString("N"));
        public Temp() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
