using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class AssistenteIaApplicationServiceTests
{
    [Fact]
    public async Task Capacidades_RefletemConfiguracaoSemExporSegredos()
    {
        var setup = Create();

        var result = await setup.Service.ObterCapacidadesAsync(default);

        Assert.True(result.Habilitado);
        Assert.Contains(AssistenteModo.Reescrever, result.Modos);
        Assert.Equal(4_000, result.Limites.InstrucaoCaracteres);
        Assert.True(result.SuportaStreaming);
        Assert.False(result.SuportaHistorico);
    }

    [Fact]
    public async Task Preparar_RejeitaReescritaSemSelecao()
    {
        var setup = Create();
        var request = Request(
            AssistenteModo.Reescrever,
            AssistenteOperacao.SubstituirSelecao,
            new AssistenteDocumentoRequest(AssistenteEscopoDocumento.Nenhum));

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            setup.Service.PrepararAsync(setup.Peca.Id, request, default));

        Assert.Equal("AI_CONTEXT_INCOMPATIBLE", exception.Code);
        Assert.Equal(422, exception.StatusCode);
    }

    [Fact]
    public async Task Preparar_NaoEncontraPecaDeOutraOrganization()
    {
        var setup = Create();
        var outraPeca = CreatePeca(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        setup.Pecas.Items.Add(outraPeca);

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            setup.Service.PrepararAsync(outraPeca.Id, Pergunta(), default));

        Assert.Equal("PECA_NOT_FOUND", exception.Code);
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task Gerar_UsaSomenteContextoAutorizadoENaoEscreveNaPeca()
    {
        var setup = Create();
        setup.Casos.Cases.Add(new Caso
        {
            Id = setup.Peca.CasoId,
            OrganizationId = setup.Auth.OrganizationId,
            Titulo = "Bloqueio de conta",
            Descricao = "Conta destinada ao recebimento de benefício."
        });
        setup.Casos.Envolvidos.Add(new EnvolvidoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = setup.Peca.CasoId,
            Nome = "Maria de Souza",
            Tipo = TipoEnvolvidoCasoEnum.Cliente,
            Natureza = NaturezaPessoaEnum.PessoaFisica,
            Profissao = "Aposentada",
            CpfCnpj = "52998224725",
            Email = "privado@example.test"
        });
        var contexto = new ContextoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = setup.Peca.CasoId,
            Tipo = TipoContextoCasoEnum.RelatoInicial,
            Titulo = "Relato",
            Conteudo = "O bloqueio persiste há quinze dias."
        };
        setup.Casos.Contextos.Add(contexto);
        var request = Pergunta(
            new AssistenteDocumentoRequest(
                AssistenteEscopoDocumento.SecaoAtual,
                "DOS FATOS\nA conta foi bloqueada.",
                "DOS FATOS",
                RevisaoEditor: 4),
            incluirDadosCaso: true,
            contextIds: [contexto.Id]);

        var execution = await setup.Service.PrepararAsync(setup.Peca.Id, request, default);
        var result = await setup.Service.GerarAsync(execution, default);
        await setup.Service.FinalizarAsync(execution);

        Assert.NotNull(setup.Provider.LastRequest);
        Assert.Contains("não possui pesquisa jurídica externa", setup.Provider.LastRequest.Instructions);
        Assert.Contains("REFERENCIA_NAO_VERIFICADA", setup.Provider.LastRequest.Instructions);
        Assert.Contains("Maria de Souza", setup.Provider.LastRequest.Input);
        Assert.Contains("Aposentada", setup.Provider.LastRequest.Input);
        Assert.Contains("O bloqueio persiste há quinze dias.", setup.Provider.LastRequest.Input);
        Assert.DoesNotContain("52998224725", setup.Provider.LastRequest.Input);
        Assert.DoesNotContain("privado@example.test", setup.Provider.LastRequest.Input);
        Assert.Equal("Resposta segura", result.Sugestao.Texto);
        Assert.Equal(0, setup.Pecas.UpdateCalls);
        var usage = Assert.Single(setup.UsageRecorder.Records);
        Assert.Equal("succeeded", usage.Status);
        Assert.Equal(execution.CorrelationId, usage.CorrelationId);
        Assert.Equal(100, usage.InputTokens);
    }

    [Fact]
    public async Task Preparar_RejeitaContextoDeOutroCaso()
    {
        var setup = Create();
        var context = new ContextoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = Guid.NewGuid(),
            Tipo = TipoContextoCasoEnum.Observacao,
            Conteudo = "Não pode entrar no prompt."
        };
        setup.Casos.Contextos.Add(context);

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            setup.Service.PrepararAsync(
                setup.Peca.Id,
                Pergunta(contextIds: [context.Id]),
                default));

        Assert.Equal("AI_CONTEXT_INCOMPATIBLE", exception.Code);
    }

    [Fact]
    public async Task Preparar_ImpedeMesmoClientRequestIdEnquantoEstiverEmAndamento()
    {
        var setup = Create();
        var clientRequestId = Guid.NewGuid();
        var request = Pergunta(clientRequestId: clientRequestId);
        var execution = await setup.Service.PrepararAsync(setup.Peca.Id, request, default);

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            setup.Service.PrepararAsync(setup.Peca.Id, request, default));
        await setup.Service.FinalizarAsync(execution);

        Assert.Equal("AI_REQUEST_ALREADY_RUNNING", exception.Code);
        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task Gerar_RejeitaOperacaoDoProviderIncompativelComModo()
    {
        var setup = Create();
        setup.Provider.Result = setup.Provider.Result with
        {
            OperacaoSugerida = AssistenteOperacao.SubstituirSelecao
        };
        var execution = await setup.Service.PrepararAsync(setup.Peca.Id, Pergunta(), default);

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            setup.Service.GerarAsync(execution, default));
        await setup.Service.FinalizarAsync(execution);

        Assert.Equal("AI_PROVIDER_ERROR", exception.Code);
        Assert.Equal(502, exception.StatusCode);
        var usage = Assert.Single(setup.UsageRecorder.Records);
        Assert.Equal("failed", usage.Status);
        Assert.Equal(100, usage.InputTokens);
    }

    [Fact]
    public async Task Logs_NaoIncluemInstrucaoNemConteudoJuridico()
    {
        var setup = Create();
        const string sensitiveMarker = "CPF-SEGREDO-52998224725";
        var request = new CriarSolicitacaoAssistenteRequest(
            Guid.NewGuid(),
            AssistenteModo.Perguntar,
            $"Analise {sensitiveMarker}",
            1,
            AssistenteOperacao.ResponderNoPainel,
            new AssistenteContextoRequest(new AssistenteDocumentoRequest(
                AssistenteEscopoDocumento.SecaoAtual,
                $"DOS FATOS {sensitiveMarker}")));

        var execution = await setup.Service.PrepararAsync(setup.Peca.Id, request, default);
        await setup.Service.GerarAsync(execution, default);
        await setup.Service.FinalizarAsync(execution);

        Assert.DoesNotContain(setup.Logger.Messages, message => message.Contains(sensitiveMarker, StringComparison.Ordinal));
    }

    private static Setup Create()
    {
        var settings = new AssistenteIaSettings
        {
            Enabled = true,
            Model = "modelo-teste",
            ApiKey = "chave-teste"
        };
        var auth = new TestAuthContext();
        var peca = CreatePeca(Guid.NewGuid(), Guid.NewGuid(), auth.OrganizationId);
        var pecas = new PecaRepositoryStub();
        pecas.Items.Add(peca);
        var casos = new CasoRepositoryStub(auth.OrganizationId);
        var provider = new ProviderStub();
        var usageRecorder = new UsageRecorderStub();
        var logger = new CaptureLogger<AssistenteIaApplicationService>();
        var service = new AssistenteIaApplicationService(
            pecas,
            casos,
            auth,
            provider,
            new AssistenteContextBuilder(Options.Create(settings)),
            new AssistentePromptPolicy(),
            new AssistenteRequestRegistry(),
            new FeatureAccessStub(),
            new UsageLimiterStub(),
            usageRecorder,
            new CorrelationContextStub(),
            new TestOptionsMonitor<AssistenteIaSettings>(settings),
            logger);
        return new Setup(service, pecas, casos, provider, usageRecorder, logger, auth, peca);
    }

    private static Peca CreatePeca(Guid id, Guid casoId, Guid organizationId) => new()
    {
        Id = id,
        CasoId = casoId,
        OrganizationId = organizationId,
        CreatedById = Guid.NewGuid(),
        UpdatedById = Guid.NewGuid(),
        Titulo = "Petição",
        ConteudoJson = JsonDocument.Parse("""{"type":"doc","content":[]}"""),
        PageConfig = JsonDocument.Parse("""{"format":"A4"}"""),
        Styles = JsonDocument.Parse("{}"),
        Versao = 1
    };

    private static CriarSolicitacaoAssistenteRequest Pergunta(
        AssistenteDocumentoRequest? document = null,
        bool incluirDadosCaso = false,
        IReadOnlyList<Guid>? contextIds = null,
        Guid? clientRequestId = null) =>
        Request(
            AssistenteModo.Perguntar,
            AssistenteOperacao.ResponderNoPainel,
            document ?? new AssistenteDocumentoRequest(AssistenteEscopoDocumento.Nenhum),
            incluirDadosCaso,
            contextIds,
            clientRequestId);

    private static CriarSolicitacaoAssistenteRequest Request(
        AssistenteModo mode,
        AssistenteOperacao operation,
        AssistenteDocumentoRequest document,
        bool incluirDadosCaso = false,
        IReadOnlyList<Guid>? contextIds = null,
        Guid? clientRequestId = null) => new(
            clientRequestId ?? Guid.NewGuid(),
            mode,
            "Analise o conteúdo informado.",
            1,
            operation,
            new AssistenteContextoRequest(document, IncluirDadosCaso: incluirDadosCaso, ContextoCasoIds: contextIds));

    private sealed record Setup(
        AssistenteIaApplicationService Service,
        PecaRepositoryStub Pecas,
        CasoRepositoryStub Casos,
        ProviderStub Provider,
        UsageRecorderStub UsageRecorder,
        CaptureLogger<AssistenteIaApplicationService> Logger,
        TestAuthContext Auth,
        Peca Peca);

    private sealed class ProviderStub : IAssistenteIaProvider
    {
        public AssistenteIaProviderRequest? LastRequest { get; private set; }
        public AssistenteIaProviderResult Result { get; set; } = new(
            "Análise",
            "Resposta segura",
            "Resumo",
            AssistenteOperacao.ResponderNoPainel,
            [],
            [],
            100,
            0,
            20,
            "modelo-teste");

        public Task<AssistenteIaProviderResult> GerarAsync(
            AssistenteIaProviderRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FeatureAccessStub : IAssistenteFeatureAccess
    {
        public Task<AssistenteFeatureSnapshot> ObterAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(new AssistenteFeatureSnapshot(
                true,
                [OrganizationFeatureEnum.AI_ASSISTANT, OrganizationFeatureEnum.AI_EDIT, OrganizationFeatureEnum.AI_REVIEW],
                Enum.GetValues<AssistenteModo>()));

        public Task ExigirModoAsync(Guid organizationId, AssistenteModo modo, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class UsageLimiterStub : IAssistenteUsageLimiter
    {
        public ValueTask<IAsyncDisposable> AdquirirAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IAsyncDisposable>(new NoopLease());

        private sealed class NoopLease : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class UsageRecorderStub : IAssistenteUsageRecorder
    {
        public List<AssistenteUsageRecord> Records { get; } = [];
        public Task RegistrarAsync(AssistenteUsageRecord record, CancellationToken cancellationToken)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class CorrelationContextStub : ICorrelationContext
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
    }

    private sealed class TestOptionsMonitor<T>(T initialValue) : IOptionsMonitor<T>
    {
        private readonly T _value = initialValue;
        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class PecaRepositoryStub : IPecaRepository
    {
        public List<Peca> Items { get; } = [];
        public int UpdateCalls { get; private set; }

        public Task<Peca?> ObterPecaAsync(Guid id, Guid organizationId, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.Id == id && x.OrganizationId == organizationId));

        public Task<bool> AtualizarPecaAsync(Peca peca, int versaoEsperada, CancellationToken ct)
        {
            UpdateCalls++;
            return Task.FromResult(true);
        }

        public Task<bool> CasoExisteAsync(Guid casoId, Guid organizationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Peca> CriarPecaAsync(Peca peca, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PecaListaDto>> ListarPecasAsync(Guid casoId, Guid organizationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExcluirPecaAsync(Guid id, Guid organizationId, Guid updatedById, int versaoEsperada, DateTime agora, CancellationToken ct) => throw new NotSupportedException();
        public Task<TemplatePeca> CriarTemplateAsync(TemplatePeca template, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<TemplatePeca>> ListarTemplatesAsync(Guid organizationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<TemplatePeca?> ObterTemplateAsync(Guid id, Guid organizationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> AtualizarTemplateAsync(TemplatePeca template, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExcluirTemplateAsync(Guid id, Guid organizationId, Guid updatedById, DateTime agora, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CasoRepositoryStub(Guid organizationId) : ICasoRepository
    {
        public List<Caso> Cases { get; } = [];
        public List<EnvolvidoCaso> Envolvidos { get; } = [];
        public List<ContextoCaso> Contextos { get; } = [];

        public Task<Caso?> ObterCasoAsync(Guid id, Guid org, CancellationToken ct) =>
            Task.FromResult(Cases.SingleOrDefault(x => x.Id == id && x.OrganizationId == org));

        public Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(Guid casoId, Guid org, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EnvolvidoCaso>>(
                org == organizationId ? Envolvidos.Where(x => x.CasoId == casoId).ToList() : []);

        public Task<ContextoCaso?> ObterContextoAsync(Guid casoId, Guid id, Guid org, CancellationToken ct) =>
            Task.FromResult(
                org == organizationId
                    ? Contextos.SingleOrDefault(x => x.CasoId == casoId && x.Id == id)
                    : null);

        public Task<Caso> CriarCasoAsync(Caso caso, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Caso>> ListarCasosAsync(Guid org, CasoStatusEnum? status, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ArquivarCasoAsync(Guid id, Guid org, Guid updatedById, DateTime dataAtualizacao, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EnvolvidoCaso> CriarEnvolvidoAsync(EnvolvidoCaso envolvido, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EnvolvidoCaso?> ObterEnvolvidoAsync(Guid casoId, Guid id, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AtualizarEnvolvidoAsync(EnvolvidoCaso envolvido, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoverEnvolvidoAsync(Guid casoId, Guid id, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EnderecoEnvolvidoCaso> CriarEnderecoAsync(Guid casoId, Guid org, EnderecoEnvolvidoCaso endereco, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosAsync(Guid casoId, Guid envolvidoId, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosCasoAsync(Guid casoId, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EnderecoEnvolvidoCaso?> ObterEnderecoAsync(Guid casoId, Guid envolvidoId, Guid id, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AtualizarEnderecoAsync(Guid casoId, Guid org, EnderecoEnvolvidoCaso endereco, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoverEnderecoAsync(Guid casoId, Guid envolvidoId, Guid id, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContextoCaso> CriarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(Guid casoId, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AtualizarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoverContextoAsync(Guid casoId, Guid id, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DocumentoCaso> CriarDocumentoAsync(DocumentoCaso documento, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(Guid casoId, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DocumentoCaso?> ObterDocumentoAsync(Guid casoId, Guid id, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoverDocumentoAsync(Guid casoId, Guid id, Guid org, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
