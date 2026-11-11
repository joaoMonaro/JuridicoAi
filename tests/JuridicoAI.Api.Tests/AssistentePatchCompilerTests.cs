using System.Text.Json;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;

namespace JuridicoAI.Api.Tests;

public sealed class AssistentePatchCompilerTests
{
    public static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement.Clone();
    public static JsonElement Paragraph(string text) => JsonSerializer.SerializeToElement(new { type = "paragraph", content = new[] { new { type = "text", text } } });
    private readonly AssistentePatchCompiler compiler = new();

    [Fact]
    public void ContratoFicticio_ConfereHashesEAplicacaoParcial()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../implementacoes/features/assistente-conversacional/contratos/exemplos"));
        var original = Element(File.ReadAllText(Path.Combine(path, "peca-base.json")));
        var proposal = JsonSerializer.Deserialize<AssistentePropostaDto>(File.ReadAllText(Path.Combine(path, "proposta.json")), AssistentePatchCompiler.JsonOptions)!;
        var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "recibo.json"))).RootElement;
        using var result = compiler.Aplicar(original, [proposal.Grupos[0]]);
        Assert.Equal(receipt.GetProperty("documentSha256").GetString(), AssistentePatchCompiler.Hash(result.RootElement));
        Assert.True(JsonElement.DeepEquals(receipt.GetProperty("conteudoJson"), result.RootElement));
    }

    [Fact]
    public void DocumentoLegadoMalformado_NaoProduzErroInterno()
    {
        var exception = Assert.Throws<AssistenteIaException>(() => compiler.Compilar(
            Element("""{"type":2,"content":[]}"""), Guid.NewGuid(),
            [new("Alteração", [new(0, 1, [Paragraph("Novo")])])]));
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void Canonico_PreservaUnicodeEIgnoraOrdemDeChaves()
    {
        var expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("""{"a":{"x":1},"b":"João 🧑"}""")));
        Assert.Equal(expected, AssistentePatchCompiler.Hash(Element("""{"b":"João 🧑","a":{"x":1}}""")));
        Assert.Equal(AssistentePatchCompiler.Hash(Element("""{"b":"João 🧑","a":{"x":1}}""")),
            AssistentePatchCompiler.Hash(Element("""{"a":{"x":1},"b":"João 🧑"}""")));
        Assert.NotEqual(AssistentePatchCompiler.Hash("texto"), AssistentePatchCompiler.Hash("Texto"));
    }

    [Theory]
    [InlineData("""{"type":"heading","attrs":{"level":"2"}}""")]
    [InlineData("""{"type":"paragraph","attrs":{"textAlign":false}}""")]
    [InlineData("""{"type":"paragraph","content":[{"type":"text","text":"x","marks":[{"type":"link","attrs":{"href":"javascript:alert(1)"}}]}]}""")]
    [InlineData("""{"type":"unknown"}""")]
    [InlineData("""{"type":"listItem","content":[{"type":"heading","attrs":{"level":2}}]}""")]
    public void NovaEstruturaInvalida_ERejeitadaComo400(string node)
    {
        var exception = Assert.Throws<AssistenteIaException>(() => compiler.Compilar(
            Element("""{"type":"doc","content":[{"type":"paragraph"}]}"""), Guid.NewGuid(),
            [new("Alteração", [new(0, 1, [Element(node)])])]));
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void InsercaoParcial_RemapEFormatoOriginalPreservados()
    {
        var doc = Element("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"João","marks":[{"type":"bold"}]}]},{"type":"paragraph","content":[{"type":"text","text":"Segundo"}]}]}""");
        var groups = compiler.Compilar(doc, Guid.NewGuid(), [new("Inserir", [new(0, 0, [Paragraph("Antes")])]), new("Reescrever", [new(1, 1, [Paragraph("Depois")])])]);
        using var partial = compiler.Aplicar(doc, [groups[0]]);
        var remapped = compiler.Remapear(groups, new HashSet<Guid> { groups[0].Id });
        Assert.Equal(2, remapped[1].Operacoes[0].StartIndex);
        Assert.Equal(groups[1].PreviewSha256, remapped[1].PreviewSha256);
        using var final = compiler.Aplicar(partial.RootElement, [remapped[1]]);
        Assert.True(JsonElement.DeepEquals(doc.GetProperty("content")[0], final.RootElement.GetProperty("content")[1]));
        Assert.Equal("Depois", final.RootElement.GetProperty("content")[2].GetProperty("content")[0].GetProperty("text").GetString());
    }
}
