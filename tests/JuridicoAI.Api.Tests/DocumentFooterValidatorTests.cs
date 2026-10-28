using System.Text.Json;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;

namespace JuridicoAI.Api.Tests;

public sealed class DocumentFooterValidatorTests
{
    [Fact]
    public void AceitaRodapeLegadoEConfiguracaoTextual()
    {
        DocumentFooterValidator.Validate(JsonSerializer.SerializeToElement(new { type = "doc", content = Array.Empty<object>() }));
        DocumentFooterValidator.Validate(JsonSerializer.SerializeToElement(new { type = "doc", presentation = new { fontSize = 9, color = "#1632aB", align = "right", separator = true, offsetBottomMm = 8 } }));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"fontSize\":6}")]
    [InlineData("{\"fontSize\":17}")]
    [InlineData("{\"fontSize\":\"9\"}")]
    [InlineData("{\"align\":\"justify\"}")]
    [InlineData("{\"color\":null}")]
    [InlineData("{\"color\":\"red\"}")]
    [InlineData("{\"color\":\"#12345Z\"}")]
    [InlineData("{\"separator\":\"true\"}")]
    [InlineData("{\"offsetBottomMm\":-1}")]
    [InlineData("{\"offsetBottomMm\":31}")]
    public void RecusaApresentacaoInvalida(string presentation)
    {
        using var doc = JsonDocument.Parse("{\"type\":\"doc\",\"presentation\":" + presentation + "}");
        Assert.Throws<RequisicaoInvalidaException>(() => DocumentFooterValidator.Validate(doc.RootElement));
    }
}
