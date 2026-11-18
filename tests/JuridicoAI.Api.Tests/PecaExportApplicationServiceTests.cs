using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Tests;

public sealed class PecaExportApplicationServiceTests
{
    [Fact]
    public async Task ExportaSnapshotSemAtualizarPecaEOuConsultarModelo()
    {
        var peca = PecaDocxGeneratorTests.Fixture(false); peca.Titulo = "Petição: A/B?";
        var converter = new Converter(); var repo = new Repository(peca);
        var service = new PecaExportApplicationService(repo, new TestAuthContext(peca.OrganizationId), new(), converter);
        var docx = await service.ExportarAsync(peca.Id, new("docx", 1), default);
        Assert.Equal("Petição AB.docx", docx.FileName);
        Assert.Contains("wordprocessingml", docx.ContentType);
        Assert.True(docx.Content.AsSpan().StartsWith("PK"u8)); Assert.Equal(0, converter.Calls);
        var pdf = await service.ExportarAsync(peca.Id, new("pdf", 1), default);
        Assert.Equal("application/pdf", pdf.ContentType); Assert.Equal("Petição AB.pdf", pdf.FileName);
        Assert.Equal(1, converter.Calls); Assert.Equal(1, peca.Versao);
        Assert.Equal(2, repo.Reads);
    }

    [Fact]
    public async Task RejeitaOutraOrganizationVersaoDesatualizadaEFormatoInvalido()
    {
        var peca = PecaDocxGeneratorTests.Fixture(false); var repo = new Repository(peca); var converter = new Converter();
        var foreign = new PecaExportApplicationService(repo, new TestAuthContext(), new(), converter);
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => foreign.ExportarAsync(peca.Id, new("pdf", 1), default));
        var service = new PecaExportApplicationService(repo, new TestAuthContext(peca.OrganizationId), new(), converter);
        await Assert.ThrowsAsync<ConflitoDeVersaoException>(() => service.ExportarAsync(peca.Id, new("pdf", 2), default));
        await Assert.ThrowsAsync<RequisicaoInvalidaException>(() => service.ExportarAsync(peca.Id, new("doc", 1), default));
        await Assert.ThrowsAsync<RequisicaoInvalidaException>(() => service.ExportarAsync(peca.Id, new("docx", 0), default));
        peca.DataExclusao = DateTime.UtcNow;
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => service.ExportarAsync(peca.Id, new("docx", 1), default));
        Assert.Equal(0, converter.Calls);
    }

    private sealed class Converter : IPecaPdfConverter
    {
        public int Calls;
        public Task<byte[]> ConvertAsync(byte[] docx, CancellationToken ct) { Calls++; return Task.FromResult("%PDF-test"u8.ToArray()); }
    }
    private sealed class Repository(Peca peca) : IPecaRepository
    {
        public int Reads;
        public Task<Peca?> ObterPecaAsync(Guid id, Guid organizationId, CancellationToken ct)
        { Reads++; return Task.FromResult<Peca?>(id == peca.Id && organizationId == peca.OrganizationId && peca.DataExclusao is null ? peca : null); }
        public Task<bool> CasoExisteAsync(Guid a, Guid b, CancellationToken c) => throw new NotSupportedException();
        public Task<Peca> CriarPecaAsync(Peca a, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<PecaListaDto>> ListarPecasAsync(Guid a, Guid b, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> AtualizarPecaAsync(Peca a, int b, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> ExcluirPecaAsync(Guid a, Guid b, Guid d, int e, DateTime f, CancellationToken c) => throw new NotSupportedException();
        public Task<TemplatePeca> CriarTemplateAsync(TemplatePeca a, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<TemplatePeca>> ListarTemplatesAsync(Guid a, CancellationToken c) => throw new NotSupportedException();
        public Task<TemplatePeca?> ObterTemplateAsync(Guid a, Guid b, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> AtualizarTemplateAsync(TemplatePeca a, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> ExcluirTemplateAsync(Guid a, Guid b, Guid d, DateTime e, CancellationToken c) => throw new NotSupportedException();
    }
}
