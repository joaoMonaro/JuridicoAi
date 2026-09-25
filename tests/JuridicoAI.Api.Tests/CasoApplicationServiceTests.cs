using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
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
        Assert.Equal(CasoStatusEnum.Ativo, caso.Status);
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

        Assert.Equal(CasoStatusEnum.Arquivado, (await service.ObterCasoAsync(caso.Id, default)).Status);
    }

    [Fact]
    public async Task Envolvido_DeOutroCasoNaoPodeSerObtido()
    {
        var (service, _, _) = CreateService();
        var casoA = await service.CriarCasoAsync(new CriarCasoRequest("A", null), default);
        var casoB = await service.CriarCasoAsync(new CriarCasoRequest("B", null), default);
        var envolvido = await service.CriarEnvolvidoAsync(casoA.Id,
            new CriarEnvolvidoCasoRequest("Cliente", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica), default);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            service.ObterEnvolvidoAsync(casoB.Id, envolvido.Id, default));
    }

    [Fact]
    public async Task Envolvidos_PermitemMultiplosDoMesmoTipo()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Cliente A", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica), default);
        await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Cliente B", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica), default);

        var itens = await service.ListarEnvolvidosAsync(caso.Id, default);
        Assert.Equal(2, itens.Count);
    }

    [Fact]
    public async Task ContextoCompleto_AgregaRelacionamentosSemBinario()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Cliente", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica), default);
        await service.CriarContextoAsync(caso.Id,
            new CriarContextoCasoRequest(TipoContextoCasoEnum.RelatoInicial, null, "Fatos"), default);
        await service.CriarDocumentoAsync(caso.Id, "prova.txt", "text/plain", 5, null,
            new MemoryStream("prova"u8.ToArray()), default);

        var completo = await service.ObterContextoCompletoAsync(caso.Id, default);

        Assert.Equal(caso.Id, completo.Caso.Id);
        Assert.Single(completo.Envolvidos);
        Assert.Empty(completo.Envolvidos[0].Enderecos);
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

    [Fact]
    public async Task Organization_NaoListaLeOuAlteraCasoDeOutraOrganization()
    {
        var repository = new InMemoryCasoRepository();
        var storage = new InMemoryFileStorage();
        var settings = Options.Create(new DocumentStorageSettings { MaxFileSizeBytes = 1024 });
        var authA = new TestAuthContext();
        var authB = new TestAuthContext();
        var serviceA = new CasoApplicationService(repository, storage, authA, settings, NullLogger<CasoApplicationService>.Instance);
        var serviceB = new CasoApplicationService(repository, storage, authB, settings, NullLogger<CasoApplicationService>.Instance);
        var casoB = await serviceB.CriarCasoAsync(new CriarCasoRequest("B", null), default);

        Assert.Empty(await serviceA.ListarCasosAsync(null, default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => serviceA.ObterCasoAsync(casoB.Id, default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => serviceA.ArquivarCasoAsync(casoB.Id, default));
    }

    [Fact]
    public async Task Envolvido_PessoaFisicaNormalizaIdentidadeEMascaraDocumentoNaListagem()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);

        var criado = await service.CriarEnvolvidoAsync(caso.Id, new CriarEnvolvidoCasoRequest(
            "  Joao da Silva  ", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica,
            CpfCnpj: "529.982.247-25", RgUf: "sp", Email: "JOAO@EXAMPLE.COM", Telefone: "+55 (11) 99999-9999"), default);
        var lista = await service.ListarEnvolvidosAsync(caso.Id, default);

        Assert.Equal("Joao da Silva", criado.Nome);
        Assert.Equal("52998224725", criado.CpfCnpj);
        Assert.Equal("SP", criado.RgUf);
        Assert.Equal("joao@example.com", criado.Email);
        Assert.Equal("+5511999999999", criado.Telefone);
        Assert.Equal("***.***.***-25", Assert.Single(lista).DocumentoMascarado);
    }

    [Fact]
    public async Task Envolvido_PessoaJuridicaAceitaCnpjValido()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);

        var criado = await service.CriarEnvolvidoAsync(caso.Id, new CriarEnvolvidoCasoRequest(
            "Empresa Exemplo Ltda.", TipoEnvolvidoCasoEnum.ParteContraria, NaturezaPessoaEnum.PessoaJuridica,
            NomeFantasia: "Empresa Exemplo", CpfCnpj: "11.222.333/0001-81"), default);

        Assert.Equal("11222333000181", criado.CpfCnpj);
        Assert.Equal("Empresa Exemplo", criado.NomeFantasia);
    }

    [Theory]
    [InlineData("529.982.247-24", NaturezaPessoaEnum.PessoaFisica)]
    [InlineData("11.222.333/0001-80", NaturezaPessoaEnum.PessoaJuridica)]
    public async Task Envolvido_RejeitaDocumentoInvalido(string documento, NaturezaPessoaEnum natureza)
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);

        await Assert.ThrowsAsync<RequisicaoInvalidaException>(() => service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Pessoa", TipoEnvolvidoCasoEnum.Cliente, natureza, CpfCnpj: documento), default));
    }

    [Fact]
    public async Task Envolvido_RejeitaDataNascimentoFutura()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);

        await Assert.ThrowsAsync<RequisicaoInvalidaException>(() => service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Pessoa", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica,
                DataNascimento: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))), default));
    }

    [Fact]
    public async Task Endereco_NormalizaCamposETrocaPrincipalDoMesmoTipo()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        var envolvido = await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Pessoa", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica), default);
        var primeiro = await service.CriarEnderecoAsync(caso.Id, envolvido.Id,
            new CriarEnderecoEnvolvidoRequest(TipoEnderecoEnum.Residencial, true, "01001-000", "Rua A", "1", null, null, "Sao Paulo", "sp", null), default);

        var segundo = await service.CriarEnderecoAsync(caso.Id, envolvido.Id,
            new CriarEnderecoEnvolvidoRequest(TipoEnderecoEnum.Residencial, true, "20040-020", "Rua B", "S/N", null, null, "Rio de Janeiro", "rj", null), default);
        var enderecos = await service.ListarEnderecosAsync(caso.Id, envolvido.Id, default);

        Assert.Equal("01001000", primeiro.Cep);
        Assert.Equal("SP", primeiro.Uf);
        Assert.False(enderecos.Single(x => x.Id == primeiro.Id).Principal);
        Assert.True(enderecos.Single(x => x.Id == segundo.Id).Principal);
    }

    [Fact]
    public async Task Endereco_DeOutroEnvolvidoNaoPodeSerObtido()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        var a = await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("A", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica), default);
        var b = await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("B", TipoEnvolvidoCasoEnum.ParteContraria, NaturezaPessoaEnum.PessoaJuridica), default);
        var endereco = await service.CriarEnderecoAsync(caso.Id, a.Id,
            new CriarEnderecoEnvolvidoRequest(TipoEnderecoEnum.Residencial, false, null, "Rua A", null, null, null, "Sao Paulo", "SP", null), default);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            service.ObterEnderecoAsync(caso.Id, b.Id, endereco.Id, default));
    }

    [Fact]
    public async Task Endereco_PodeSerAtualizadoERemovido()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        var envolvido = await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Pessoa", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica), default);
        var endereco = await service.CriarEnderecoAsync(caso.Id, envolvido.Id,
            new CriarEnderecoEnvolvidoRequest(TipoEnderecoEnum.Residencial, false, null, "Rua A", null, null, null, "Sao Paulo", "SP", null), default);

        var atualizado = await service.AtualizarEnderecoAsync(caso.Id, envolvido.Id, endereco.Id,
            new AtualizarEnderecoEnvolvidoRequest(TipoEnderecoEnum.Citacao, true, "20040-020", "Rua B", "20", null, "Centro", "Rio de Janeiro", "RJ", null), default);
        await service.RemoverEnderecoAsync(caso.Id, envolvido.Id, endereco.Id, default);

        Assert.Equal(TipoEnderecoEnum.Citacao, atualizado.Tipo);
        Assert.Equal("Rio de Janeiro", atualizado.Cidade);
        Assert.True(atualizado.Principal);
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            service.ObterEnderecoAsync(caso.Id, envolvido.Id, endereco.Id, default));
    }

    [Fact]
    public async Task Endereco_DeOutraOrganizationNaoPodeSerConsultado()
    {
        var repository = new InMemoryCasoRepository();
        var storage = new InMemoryFileStorage();
        var settings = Options.Create(new DocumentStorageSettings { MaxFileSizeBytes = 1024 });
        var serviceA = new CasoApplicationService(repository, storage, new TestAuthContext(), settings, NullLogger<CasoApplicationService>.Instance);
        var serviceB = new CasoApplicationService(repository, storage, new TestAuthContext(), settings, NullLogger<CasoApplicationService>.Instance);
        var casoB = await serviceB.CriarCasoAsync(new CriarCasoRequest("B", null), default);
        var envolvidoB = await serviceB.CriarEnvolvidoAsync(casoB.Id,
            new CriarEnvolvidoCasoRequest("Pessoa B", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica), default);
        var enderecoB = await serviceB.CriarEnderecoAsync(casoB.Id, envolvidoB.Id,
            new CriarEnderecoEnvolvidoRequest(TipoEnderecoEnum.Residencial, true, null, "Rua B", null, null, null, "Cidade B", "SP", null), default);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            serviceA.ObterEnderecoAsync(casoB.Id, envolvidoB.Id, enderecoB.Id, default));
    }

    [Fact]
    public async Task ContextoCompleto_IncluiIdentidadeEEnderecos()
    {
        var (service, _, _) = CreateService();
        var caso = await service.CriarCasoAsync(new CriarCasoRequest("Caso", null), default);
        var envolvido = await service.CriarEnvolvidoAsync(caso.Id,
            new CriarEnvolvidoCasoRequest("Pessoa", TipoEnvolvidoCasoEnum.Cliente, NaturezaPessoaEnum.PessoaFisica,
                CpfCnpj: "52998224725"), default);
        await service.CriarEnderecoAsync(caso.Id, envolvido.Id,
            new CriarEnderecoEnvolvidoRequest(TipoEnderecoEnum.Citacao, true, null, "Rua A", "10", null, null, "Sao Paulo", "SP", null), default);

        var completo = await service.ObterContextoCompletoAsync(caso.Id, default);

        var item = Assert.Single(completo.Envolvidos);
        Assert.Equal("52998224725", item.CpfCnpj);
        Assert.Single(item.Enderecos);
    }

    private static (CasoApplicationService Service, InMemoryCasoRepository Repository, InMemoryFileStorage Storage)
        CreateService(long maxFileSize = 1024)
    {
        var repository = new InMemoryCasoRepository();
        var storage = new InMemoryFileStorage();
        var settings = Options.Create(new DocumentStorageSettings { MaxFileSizeBytes = maxFileSize });
        var service = new CasoApplicationService(repository, storage, new TestAuthContext(), settings,
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
        private readonly Dictionary<Guid, EnderecoEnvolvidoCaso> _enderecos = [];
        private readonly Dictionary<Guid, ContextoCaso> _contextos = [];
        private readonly Dictionary<Guid, DocumentoCaso> _documentos = [];
        public bool FailDocumentCreate { get; set; }

        public Task<Caso> CriarCasoAsync(Caso caso, CancellationToken ct) { _casos.Add(caso.Id, caso); return Task.FromResult(caso); }
        public Task<IReadOnlyList<Caso>> ListarCasosAsync(Guid org, CasoStatusEnum? status, CancellationToken ct) => Result(_casos.Values.Where(x => x.OrganizationId == org && (status is null || x.Status == status)));
        public Task<Caso?> ObterCasoAsync(Guid id, Guid org, CancellationToken ct) => Task.FromResult(_casos.GetValueOrDefault(id) is { } x && x.OrganizationId == org ? x : null);
        public Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken ct) => Task.FromResult(_casos.ContainsKey(caso.Id));
        public Task<bool> ArquivarCasoAsync(Guid id, Guid org, Guid user, DateTime updatedAt, CancellationToken ct)
        {
            if (!_casos.TryGetValue(id, out var caso) || caso.OrganizationId != org) return Task.FromResult(false);
            caso.Status = CasoStatusEnum.Arquivado; caso.DataAtualizacao = updatedAt; return Task.FromResult(true);
        }

        public Task<EnvolvidoCaso> CriarEnvolvidoAsync(EnvolvidoCaso item, CancellationToken ct) { _envolvidos.Add(item.Id, item); return Task.FromResult(item); }
        public Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(Guid casoId, Guid org, CancellationToken ct) => Result(_envolvidos.Values.Where(x => x.CasoId == casoId && _casos.GetValueOrDefault(casoId)?.OrganizationId == org));
        public Task<EnvolvidoCaso?> ObterEnvolvidoAsync(Guid casoId, Guid id, Guid org, CancellationToken ct) => Task.FromResult(_envolvidos.GetValueOrDefault(id) is { } x && x.CasoId == casoId && _casos.GetValueOrDefault(casoId)?.OrganizationId == org ? x : null);
        public Task<bool> AtualizarEnvolvidoAsync(EnvolvidoCaso item, Guid org, CancellationToken ct) => Task.FromResult(_envolvidos.ContainsKey(item.Id) && _casos.GetValueOrDefault(item.CasoId)?.OrganizationId == org);
        public Task<bool> RemoverEnvolvidoAsync(Guid casoId, Guid id, Guid org, CancellationToken ct)
        {
            if (_casos.GetValueOrDefault(casoId)?.OrganizationId != org || !Remove(_envolvidos, casoId, id, x => x.CasoId)) return Task.FromResult(false);
            foreach (var enderecoId in _enderecos.Values.Where(x => x.EnvolvidoCasoId == id).Select(x => x.Id).ToList()) _enderecos.Remove(enderecoId);
            return Task.FromResult(true);
        }

        public Task<EnderecoEnvolvidoCaso> CriarEnderecoAsync(Guid casoId, Guid org, EnderecoEnvolvidoCaso item, CancellationToken ct)
        {
            if (!_envolvidos.TryGetValue(item.EnvolvidoCasoId, out var envolvido) || envolvido.CasoId != casoId || _casos.GetValueOrDefault(casoId)?.OrganizationId != org)
                throw new InvalidOperationException();
            if (item.Principal) DesmarcarPrincipal(item);
            _enderecos.Add(item.Id, item);
            return Task.FromResult(item);
        }
        public Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosAsync(Guid casoId, Guid envolvidoId, Guid org, CancellationToken ct) =>
            Result(EnderecosEscopados(casoId, envolvidoId, org).OrderByDescending(x => x.Principal).ThenBy(x => x.Tipo));
        public Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosCasoAsync(Guid casoId, Guid org, CancellationToken ct) =>
            Result(_enderecos.Values.Where(x => _envolvidos.GetValueOrDefault(x.EnvolvidoCasoId)?.CasoId == casoId && _casos.GetValueOrDefault(casoId)?.OrganizationId == org));
        public Task<EnderecoEnvolvidoCaso?> ObterEnderecoAsync(Guid casoId, Guid envolvidoId, Guid id, Guid org, CancellationToken ct) =>
            Task.FromResult(EnderecosEscopados(casoId, envolvidoId, org).SingleOrDefault(x => x.Id == id));
        public Task<bool> AtualizarEnderecoAsync(Guid casoId, Guid org, EnderecoEnvolvidoCaso item, CancellationToken ct)
        {
            if (!EnderecosEscopados(casoId, item.EnvolvidoCasoId, org).Any(x => x.Id == item.Id)) return Task.FromResult(false);
            if (item.Principal) DesmarcarPrincipal(item);
            _enderecos[item.Id] = item;
            return Task.FromResult(true);
        }
        public Task<bool> RemoverEnderecoAsync(Guid casoId, Guid envolvidoId, Guid id, Guid org, CancellationToken ct) =>
            Task.FromResult(EnderecosEscopados(casoId, envolvidoId, org).Any(x => x.Id == id) && _enderecos.Remove(id));

        private IEnumerable<EnderecoEnvolvidoCaso> EnderecosEscopados(Guid casoId, Guid envolvidoId, Guid org) =>
            _enderecos.Values.Where(x => x.EnvolvidoCasoId == envolvidoId && _envolvidos.GetValueOrDefault(envolvidoId)?.CasoId == casoId && _casos.GetValueOrDefault(casoId)?.OrganizationId == org);
        private void DesmarcarPrincipal(EnderecoEnvolvidoCaso item)
        {
            foreach (var atual in _enderecos.Values.Where(x => x.EnvolvidoCasoId == item.EnvolvidoCasoId && x.Tipo == item.Tipo && x.Id != item.Id))
                atual.Principal = false;
        }

        public Task<ContextoCaso> CriarContextoAsync(ContextoCaso item, CancellationToken ct) { _contextos.Add(item.Id, item); return Task.FromResult(item); }
        public Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(Guid casoId, Guid org, CancellationToken ct) => Result(_contextos.Values.Where(x => x.CasoId == casoId && _casos.GetValueOrDefault(casoId)?.OrganizationId == org));
        public Task<ContextoCaso?> ObterContextoAsync(Guid casoId, Guid id, Guid org, CancellationToken ct) => Task.FromResult(_contextos.GetValueOrDefault(id) is { } x && x.CasoId == casoId && _casos.GetValueOrDefault(casoId)?.OrganizationId == org ? x : null);
        public Task<bool> AtualizarContextoAsync(ContextoCaso item, CancellationToken ct) => Task.FromResult(_contextos.ContainsKey(item.Id));
        public Task<bool> RemoverContextoAsync(Guid casoId, Guid id, Guid org, CancellationToken ct) => Task.FromResult(_casos.GetValueOrDefault(casoId)?.OrganizationId == org && Remove(_contextos, casoId, id, x => x.CasoId));

        public Task<DocumentoCaso> CriarDocumentoAsync(DocumentoCaso item, CancellationToken ct)
        {
            if (FailDocumentCreate) throw new InvalidOperationException("falha simulada");
            _documentos.Add(item.Id, item); return Task.FromResult(item);
        }
        public Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(Guid casoId, Guid org, CancellationToken ct) => Result(_documentos.Values.Where(x => x.CasoId == casoId && _casos.GetValueOrDefault(casoId)?.OrganizationId == org));
        public Task<DocumentoCaso?> ObterDocumentoAsync(Guid casoId, Guid id, Guid org, CancellationToken ct) => Task.FromResult(_documentos.GetValueOrDefault(id) is { } x && x.CasoId == casoId && _casos.GetValueOrDefault(casoId)?.OrganizationId == org ? x : null);
        public Task<bool> RemoverDocumentoAsync(Guid casoId, Guid id, Guid org, CancellationToken ct) => Task.FromResult(_casos.GetValueOrDefault(casoId)?.OrganizationId == org && Remove(_documentos, casoId, id, x => x.CasoId));

        private static Task<IReadOnlyList<T>> Result<T>(IEnumerable<T> items) => Task.FromResult<IReadOnlyList<T>>(items.ToList());
        private static bool Remove<T>(Dictionary<Guid, T> items, Guid casoId, Guid id, Func<T, Guid> getCasoId) =>
            items.TryGetValue(id, out var item) && getCasoId(item) == casoId && items.Remove(id);
    }
}
