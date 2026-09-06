using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Tests;

public sealed class PecaApplicationServiceTests
{
    private const string Doc = """{"type":"doc","content":[{"type":"paragraph"}]}""";
    private const string Page = """{"format":"A4","marginTop":10,"marginBottom":11,"marginLeft":12,"marginRight":13}""";

    [Fact]
    public async Task CriarSemTemplate_UsaDefaultsRascunhoEVersaoUm()
    {
        var (service, _, repository, casoId) = Create();
        var peca = await service.CriarAsync(casoId, new CriarPecaRequest("  Inicial  "), default);
        Assert.Equal("Inicial", peca.Titulo);
        Assert.Equal(PecaStatusEnum.Rascunho, peca.Status);
        Assert.Equal(1, peca.Versao);
        Assert.Equal("A4", peca.PageConfig.RootElement.GetProperty("format").GetString());
        Assert.Null(peca.TemplateId);
        Assert.Single(await service.ListarAsync(casoId, default));
    }

    [Fact]
    public async Task CriarComTemplate_CopiaDadosEMantemOrigemSemVinculoDinamico()
    {
        var (service, templates, _, casoId) = Create();
        var template = await templates.CriarAsync(new CriarTemplatePecaRequest("Modelo", Json(Doc), Json(Page)), default);
        var peca = await service.CriarAsync(casoId, new CriarPecaRequest("Peca", template.Id), default);
        await templates.AtualizarAsync(template.Id, new AtualizarTemplatePecaRequest(PageConfig: Json("""{"format":"A4","marginTop":99,"marginBottom":1,"marginLeft":1,"marginRight":1}""")), default);
        Assert.Equal(template.Id, peca.TemplateId);
        Assert.Equal(10, peca.PageConfig.RootElement.GetProperty("marginTop").GetInt32());
        Assert.Equal("paragraph", peca.ConteudoJson.RootElement.GetProperty("content")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task Atualizar_IncrementaVersaoERejeitaVersaoAntiga()
    {
        var (service, _, _, casoId) = Create();
        var peca = await service.CriarAsync(casoId, new CriarPecaRequest("Peca"), default);
        var result = await service.AtualizarAsync(peca.Id, new AtualizarPecaRequest(1, Titulo: "Novo", ConteudoJson: Json(Doc)), default);
        Assert.Equal(2, result.Versao);
        Assert.Equal("Novo", (await service.ObterAsync(peca.Id, default)).Titulo);
        await Assert.ThrowsAsync<ConflitoDeVersaoException>(() =>
            service.AtualizarAsync(peca.Id, new AtualizarPecaRequest(1, Status: PecaStatusEnum.Finalizada), default));
    }

    [Fact]
    public async Task Excluir_OcultaPecaSemRemoverTemplateOuOutrasPecas()
    {
        var (service, templates, _, casoId) = Create();
        var template = await templates.CriarAsync(new CriarTemplatePecaRequest("Modelo"), default);
        var peca = await service.CriarAsync(casoId, new CriarPecaRequest("Peca", template.Id), default);
        await templates.ExcluirAsync(template.Id, default);
        Assert.Equal(template.Id, (await service.ObterAsync(peca.Id, default)).TemplateId);
        await service.ExcluirAsync(peca.Id, 1, default);
        Assert.Empty(await service.ListarAsync(casoId, default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => service.ObterAsync(peca.Id, default));
    }

    [Fact]
    public async Task Criar_RejeitaCasoOuTemplateInexistente()
    {
        var (service, _, _, casoId) = Create();
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => service.CriarAsync(Guid.NewGuid(), new("Peca"), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => service.CriarAsync(casoId, new("Peca", Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Organizations_NaoAcessamCasosPecasOuTemplatesEntreSi()
    {
        var repository = new FakeRepository();
        var authA = new TestAuthContext();
        var authB = new TestAuthContext();
        var casoB = Guid.NewGuid();
        repository.Casos.Add(casoB);
        repository.CasoOrganizations[casoB] = authB.OrganizationId;
        var pecasA = new PecaApplicationService(repository, authA);
        var pecasB = new PecaApplicationService(repository, authB);
        var templatesA = new TemplatePecaApplicationService(repository, authA);
        var templatesB = new TemplatePecaApplicationService(repository, authB);
        var templateB = await templatesB.CriarAsync(new CriarTemplatePecaRequest("B"), default);
        var pecaB = await pecasB.CriarAsync(casoB, new CriarPecaRequest("B", templateB.Id), default);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => pecasA.ListarAsync(casoB, default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => pecasA.ObterAsync(pecaB.Id, default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => pecasA.CriarAsync(casoB, new("X"), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => templatesA.ObterAsync(templateB.Id, default));
        Assert.Empty(await templatesA.ListarAsync(default));
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static (PecaApplicationService, TemplatePecaApplicationService, FakeRepository, Guid) Create()
    {
        var repository = new FakeRepository();
        var casoId = Guid.NewGuid(); repository.Casos.Add(casoId);
        var auth = new TestAuthContext();
        repository.CasoOrganizations[casoId] = auth.OrganizationId;
        return (new(repository, auth), new(repository, auth), repository, casoId);
    }

    private sealed class FakeRepository : IPecaRepository
    {
        public HashSet<Guid> Casos { get; } = [];
        public Dictionary<Guid, Guid> CasoOrganizations { get; } = [];
        private readonly Dictionary<Guid, Peca> _pecas = [];
        private readonly Dictionary<Guid, TemplatePeca> _templates = [];
        public Task<bool> CasoExisteAsync(Guid id, Guid org, CancellationToken ct) => Task.FromResult(Casos.Contains(id) && CasoOrganizations.GetValueOrDefault(id) == org);
        public Task<Peca> CriarPecaAsync(Peca x, CancellationToken ct) { _pecas[x.Id] = x; return Task.FromResult(x); }
        public Task<IReadOnlyList<PecaListaDto>> ListarPecasAsync(Guid casoId, Guid org, CancellationToken ct) => Task.FromResult<IReadOnlyList<PecaListaDto>>(_pecas.Values.Where(x => x.CasoId == casoId && x.OrganizationId == org && x.DataExclusao is null).Select(x => new PecaListaDto(x.Id, x.Titulo, x.Status, x.Versao, x.TemplateId, x.DataAtualizacao)).ToList());
        public Task<Peca?> ObterPecaAsync(Guid id, Guid org, CancellationToken ct) => Task.FromResult(_pecas.GetValueOrDefault(id) is { DataExclusao: null } x && x.OrganizationId == org ? x : null);
        public Task<bool> AtualizarPecaAsync(Peca x, int versao, CancellationToken ct)
        {
            if (!_pecas.TryGetValue(x.Id, out var atual) || atual.DataExclusao is not null || atual.Versao != versao) return Task.FromResult(false);
            atual.Titulo = x.Titulo; atual.ConteudoJson = x.ConteudoJson; atual.PageConfig = x.PageConfig;
            atual.HeaderJson = x.HeaderJson; atual.FooterJson = x.FooterJson; atual.Styles = x.Styles;
            atual.Status = x.Status; atual.Versao = versao + 1; atual.DataAtualizacao = x.DataAtualizacao;
            return Task.FromResult(true);
        }
        public Task<bool> ExcluirPecaAsync(Guid id, Guid org, Guid user, int versao, DateTime agora, CancellationToken ct)
        { if (!_pecas.TryGetValue(id, out var x) || x.OrganizationId != org || x.DataExclusao is not null || x.Versao != versao) return Task.FromResult(false); x.DataExclusao = agora; x.Versao++; return Task.FromResult(true); }
        public Task<TemplatePeca> CriarTemplateAsync(TemplatePeca x, CancellationToken ct) { _templates[x.Id] = x; return Task.FromResult(x); }
        public Task<IReadOnlyList<TemplatePeca>> ListarTemplatesAsync(Guid org, CancellationToken ct) => Task.FromResult<IReadOnlyList<TemplatePeca>>(_templates.Values.Where(x => x.OrganizationId == org && x.DataExclusao is null).ToList());
        public Task<TemplatePeca?> ObterTemplateAsync(Guid id, Guid org, CancellationToken ct) => Task.FromResult(_templates.GetValueOrDefault(id) is { DataExclusao: null } x && x.OrganizationId == org ? x : null);
        public Task<bool> AtualizarTemplateAsync(TemplatePeca x, CancellationToken ct) => Task.FromResult(_templates.ContainsKey(x.Id));
        public Task<bool> ExcluirTemplateAsync(Guid id, Guid org, Guid user, DateTime agora, CancellationToken ct)
        { if (!_templates.TryGetValue(id, out var x) || x.OrganizationId != org || x.DataExclusao is not null) return Task.FromResult(false); x.DataExclusao = agora; return Task.FromResult(true); }
    }
}
