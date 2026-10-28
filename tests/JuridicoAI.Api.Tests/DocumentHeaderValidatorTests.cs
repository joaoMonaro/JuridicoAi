using System.Text.Json;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;

namespace JuridicoAI.Api.Tests;

public sealed class DocumentHeaderValidatorTests
{
    public const string Image = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aAvsAAAAASUVORK5CYII=";
    private static JsonElement Header(object presentation) => JsonSerializer.SerializeToElement(new { type = "doc", content = Array.Empty<object>(), presentation });

    [Theory]
    [InlineData("banner")]
    [InlineData("logo")]
    public void AceitaImagemCompletaOuLogotipo(string mode) =>
        DocumentHeaderValidator.Validate(Header(new { mode, imageDataUrl = Image, widthMm = 100, maxHeightMm = 25, align = "right", offsetTopMm = 3, gapMm = 4 }));

    [Fact]
    public void PreservaCabecalhoTextualLegado() =>
        DocumentHeaderValidator.Validate(JsonSerializer.SerializeToElement(new { type = "doc", content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = "Escritório" } } } } }));

    [Theory]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("https://example.com/logo.png")]
    [InlineData("data:image/png;base64,aW52YWxpZG8=")]
    [InlineData("data:image/jpeg;base64,aW52YWxpZG8=")]
    [InlineData("data:image/png;base64,!!!")]
    public void RecusaArquivoInvalidoOuEnderecoExterno(string src) =>
        Assert.Throws<RequisicaoInvalidaException>(() => DocumentHeaderValidator.Validate(Header(new { mode = "logo", imageDataUrl = src })));

    [Fact]
    public void RecusaImagemAusenteExcessoDeBytesEGeometriaInvalida()
    {
        Assert.Throws<RequisicaoInvalidaException>(() => DocumentHeaderValidator.Validate(Header(new { mode = "logo" })));
        Assert.Throws<RequisicaoInvalidaException>(() => DocumentHeaderValidator.Validate(Header(new { mode = "logo", imageDataUrl = Image, widthMm = -1 })));
        Assert.Throws<RequisicaoInvalidaException>(() => DocumentHeaderValidator.Validate(Header(new { mode = "logo", imageDataUrl = Image, align = "absolute" })));
        var huge = "data:image/png;base64," + Convert.ToBase64String(new byte[DocumentHeaderValidator.MaxImageBytes + 1]);
        Assert.Throws<RequisicaoInvalidaException>(() => DocumentHeaderValidator.Validate(Header(new { mode = "banner", imageDataUrl = huge })));
    }
}
