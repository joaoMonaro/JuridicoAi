using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class CasoApplicationServiceTests
{
    [Fact]
    public async Task CriarCaso_NormalizaEntradaEDefineStatusAtivo()
    {
        var (service, _, _) = CreateService();

        var caso = await service.CriarCasoAsync(
            new CriarCasoRequest("  Negativacao indevida  ", "  Relato inicial  "), default);

        Assert.NotEqual(Guid.Empty, caso.Id);
        Assert.Equal("Negativacao indevida", caso.Titulo);
        Assert.Equal("Relato inicial", caso.Descricao);
        Assert.Equal(CasoStatus.Ativo, caso.Status);
        Assert.Equal(caso.DataCriacao, caso.DataAtualizacao);
    }

    [Fact]
    public async Task CriarCaso_RejeitaTituloVazio()
    {
        var (service, _, _) = CreateService();
        await Assert.ThrowsAsync<RequisicaoInvalidaException>(() =>
            service.CriarCasoAsync(new CriarCasoRequest("  ", null), default));
    }

    [Fact]
    public async Task ArquivarCaso_MantemRegistroEAlteraStatus()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);

        await service.ArquivarCasoAsync(caso.Id, default);

        Assert.Equal(CasoStatus.Arquivado, (await service.ObterCasoAsync(caso.Id, default)).Status);
    }

    [Fact]
    public async Task Envolvido_DeOutroCasoNaoPodeSerObtido()
    {
        var (service, _, _) = CreateService();
        var casoA = await service.CriarCasoAsync(new CriarCasoRequest("A", null), default);
        var casoB = await service.CriarCasoAsync(new CriarCasoRequest("B", null), default);
        var envolvido = await service.CriarEnvolvidoAsync(casoA.Id,
            new CriarEnvolvidoCasoRequest("Cliente", TipoEnvolvidoCaso.Cliente, null, null), default);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            service.ObterEnvolvidoAsync(casoB.Id, envolvido.Id, default));
    }

    [Fact]
    public async Task Envolvidos_PermitemMultiplosDoMesmoTipo()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Cliente A", TipoEnvolvidoCaso.Cliente, null, null), default);
        await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Cliente B", TipoEnvolvidoCaso.Cliente, null, null), default);

        var itens = await service.ListarEnvolvidosAsync(caso.Id, default);
        Assert.Equal(2, itens.Count);
    }

    [Fact]
    public async Task ContextoCompleto_AgregaRelacionamentosSemBinario()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Cliente", TipoEnvolvidoCaso.Cliente, null, null), default);
        await service.CriarContextoAsync(caso.Id,
            new CriarContextoCasoRequest(TipoContextoCaso.RelatoInicial, null, "Fatos"), default);
        await service.CriarDocumentoAsync(caso.Id, "prova.txt", "text/plain", 5, null,
            new MemoryStream("prova"u8.ToArray()), default);

        var completo = await service.ObterContextoCompletoAsync(caso.Id, default);

        Assert.Equal(caso.Id, completo.Caso.Id);
        Assert.Single(completo.Envolvidos);
        Assert.Single(completo.Contextos);
        Assert.Single(completo.Documentos);
    }

    [Fact]
    public async Task Upload_GeraStorageIdSeguroEIgnoraCaminhoDoCliente()
    {
        var (service, _, storage) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);

        var documento = await service.CriarDocumentoAsync(
            caso.Id, "../../contrato.pdf", "application/pdf", 3, "Contrato",
            new MemoryStream([1, 2, 3]), default);

        Assert.Equal("contrato.pdf", documento.NomeOriginal);
        Assert.StartsWith($"casos/{caso.Id:N}/documentos/", documento.StorageId);
        Assert.DoesNotContain("..", documento.StorageId);
        Assert.Contains(documento.StorageId, storage.Saved);
    }

    [Fact]
    public async Task Upload_RejeitaTipoNaoPermitidoAntesDeSalvar()
    {
        var (service, _, storage) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);

        await Assert.ThrowsAsync<TipoArquivoNaoSuportadoException>(() => service.CriarDocumentoAsync(
            caso.Id, "malware.exe", "application/octet-stream", 3, null,
            new MemoryStream([1, 2, 3]), default));
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Upload_RejeitaArquivoAcimaDoLimite()
    {
        var (service, _, _) = CreateService(maxFileSize: 2);
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        await Assert.ThrowsAsync<ArquivoMuitoGrandeException>(() => service.CriarDocumentoAsync(
            caso.Id, "prova.txt", "text/plain", 3, null,
            new MemoryStream([1, 2, 3]), default));
    }

    [Fact]
    public async Task Upload_FalhaDePersistenciaRemoveArquivoSalvo()
    {
        var (service, repository, storage) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        repository.FailDocumentCreate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CriarDocumentoAsync(
            caso.Id, "prova.txt", "text/plain", 3, null,
            new MemoryStream([1, 2, 3]), default));

        Assert.Empty(storage.Saved);
        Assert.Single(storage.Deleted);
    }

    private static (CasoApplicationService Service, InMemoryCasoRepository Repository, InMemoryFileStorage Storage)
        CreateService(long maxFileSize = 1024)
    {
        var repository = new InMemoryCasoRepository();
        var storage = new InMemoryFileStorage();
        var settings = Options.Create(new DocumentStorageSettings { MaxFileSizeBytes = maxFileSize });
        var service = new CasoApplicationService(repository, storage, settings,
            NullLogger<CasoApplicationService>.Instance);
        return (service, repository, storage);
    }

    private sealed class InMemoryFileStorage : IFileStorage
    {
        public HashSet<string> Saved { get; } = [];
        public List<string> Deleted { get; } = [];

        public Task SaveAsync(string storageId, Stream content, CancellationToken cancellationToken)
        {
            Saved.Add(storageId);
            return Task.CompletedTask;
        }

        public Task<Stream> GetAsync(string storageId, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream([1]));

        public Task DeleteAsync(string storageId, CancellationToken cancellationToken)
        {
            Saved.Remove(storageId);
            Deleted.Add(storageId);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCasoRepository : ICasoRepository
    {
        private readonly Dictionary<Guid, Caso> _casos = [];
        private readonly Dictionary<Guid, EnvolvidoCaso> _envolvidos = [];
        private readonly Dictionary<Guid, ContextoCaso> _contextos = [];
        private readonly Dictionary<Guid, DocumentoCaso> _documentos = [];
        public bool FailDocumentCreate { get; set; }

        public Task<Caso> CriarCasoAsync(Caso caso, CancellationToken ct) { _casos.Add(caso.Id, caso); return Task.FromResult(caso); }
        public Task<IReadOnlyList<Caso>> ListarCasosAsync(CasoStatus? status, CancellationToken ct) => Result(_casos.Values.Where(x => status is null || x.Status == status));
        public Task<Caso?> ObterCasoAsync(Guid id, CancellationToken ct) => Task.FromResult(_casos.GetValueOrDefault(id));
        public Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken ct) => Task.FromResult(_casos.ContainsKey(caso.Id));
        public Task<bool> ArquivarCasoAsync(Guid id, DateTime updatedAt, CancellationToken ct)
        {
            if (!_casos.TryGetValue(id, out var caso)) return Task.FromResult(false);
            caso.Status = CasoStatus.Arquivado; caso.DataAtualizacao = updatedAt; return Task.FromResult(true);
        }

        public Task<EnvolvidoCaso> CriarEnvolvidoAsync(EnvolvidoCaso item, CancellationToken ct) { _envolvidos.Add(item.Id, item); return Task.FromResult(item); }
        public Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(Guid casoId, CancellationToken ct) => Result(_envolvidos.Values.Where(x => x.CasoId == casoId));
        public Task<EnvolvidoCaso?> ObterEnvolvidoAsync(Guid casoId, Guid id, CancellationToken ct) => Task.FromResult(_envolvidos.GetValueOrDefault(id) is { } x && x.CasoId == casoId ? x : null);
        public Task<bool> AtualizarEnvolvidoAsync(EnvolvidoCaso item, CancellationToken ct) => Task.FromResult(_envolvidos.ContainsKey(item.Id));
        public Task<bool> RemoverEnvolvidoAsync(Guid casoId, Guid id, CancellationToken ct) => Task.FromResult(Remove(_envolvidos, casoId, id, x => x.CasoId));

        public Task<ContextoCaso> CriarContextoAsync(ContextoCaso item, CancellationToken ct) { _contextos.Add(item.Id, item); return Task.FromResult(item); }
        public Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(Guid casoId, CancellationToken ct) => Result(_contextos.Values.Where(x => x.CasoId == casoId));
        public Task<ContextoCaso?> ObterContextoAsync(Guid casoId, Guid id, CancellationToken ct) => Task.FromResult(_contextos.GetValueOrDefault(id) is { } x && x.CasoId == casoId ? x : null);
        public Task<bool> AtualizarContextoAsync(ContextoCaso item, CancellationToken ct) => Task.FromResult(_contextos.ContainsKey(item.Id));
        public Task<bool> RemoverContextoAsync(Guid casoId, Guid id, CancellationToken ct) => Task.FromResult(Remove(_contextos, casoId, id, x => x.CasoId));

        public Task<DocumentoCaso> CriarDocumentoAsync(DocumentoCaso item, CancellationToken ct)
        {
            if (FailDocumentCreate) throw new InvalidOperationException("falha simulada");
            _documentos.Add(item.Id, item); return Task.FromResult(item);
        }
        public Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(Guid casoId, CancellationToken ct) => Result(_documentos.Values.Where(x => x.CasoId == casoId));
        public Task<DocumentoCaso?> ObterDocumentoAsync(Guid casoId, Guid id, CancellationToken ct) => Task.FromResult(_documentos.GetValueOrDefault(id) is { } x && x.CasoId == casoId ? x : null);
        public Task<bool> RemoverDocumentoAsync(Guid casoId, Guid id, CancellationToken ct) => Task.FromResult(Remove(_documentos, casoId, id, x => x.CasoId));

        private static Task<IReadOnlyList<T>> Result<T>(IEnumerable<T> items) => Task.FromResult<IReadOnlyList<T>>(items.ToList());
        private static bool Remove<T>(Dictionary<Guid, T> items, Guid casoId, Guid id, Func<T, Guid> getCasoId) =>
            items.TryGetValue(id, out var item) && getCasoId(item) == casoId && items.Remove(id);
    }
}
