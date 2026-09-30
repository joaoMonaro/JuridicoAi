using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Controllers.Assistente;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Http;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class AssistenteIaHttpSecurityTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Http_SemIdentidadeRetorna401AntesDoProvider()
    {
        await using var api = await TestApi.CreateAsync();
        using var client = api.CreateAnonymousClient();

        var response = await client.PostAsync(
            $"pecas/{Guid.NewGuid()}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, api.Provider.CountCalls);
        Assert.Equal(0, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_OrganizationANaoAcessaRecursosDaOrganizationB()
    {
        await using var api = await TestApi.CreateAsync();
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var casoB = CreateCaso(organizationB, "CASO-PRIVADO-ORG-B");
        var pecaB = CreatePeca(casoB.Id, organizationB, "PECA-PRIVADA-ORG-B");
        var contextoB = new ContextoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = casoB.Id,
            Tipo = TipoContextoCasoEnum.Observacao,
            Titulo = "Contexto privado",
            Conteudo = "CONTEUDO-PRIVADO-ORG-B"
        };
        var documentoB = new DocumentoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = casoB.Id,
            NomeOriginal = "DOCUMENTO-PRIVADO-ORG-B.txt",
            NomeArmazenado = "privado.txt",
            Tipo = "Outro",
            MimeType = "text/plain",
            TamanhoBytes = 10,
            StorageId = "storage/privado"
        };
        api.Repository.Casos.Add(casoB);
        api.Repository.Pecas.Add(pecaB);
        api.Repository.Contextos.Add(contextoB);
        api.Repository.Documentos.Add(documentoB);
        using var client = api.CreateClient(organizationA);

        var responses = new[]
        {
            await client.GetAsync($"casos/{casoB.Id}"),
            await client.GetAsync($"pecas/{pecaB.Id}"),
            await client.GetAsync($"casos/{casoB.Id}/contextos/{contextoB.Id}"),
            await client.PutAsync(
                $"casos/{casoB.Id}/contextos/{contextoB.Id}",
                JsonBody(new AtualizarContextoCasoRequest(
                    TipoContextoCasoEnum.Observacao,
                    "Tentativa",
                    "ALTERACAO-INDEVIDA"))),
            await client.GetAsync($"casos/{casoB.Id}/documentos/{documentoB.Id}"),
            await client.GetAsync($"casos/{casoB.Id}/documentos/{documentoB.Id}/arquivo"),
            await client.DeleteAsync($"casos/{casoB.Id}/documentos/{documentoB.Id}"),
            await client.PostAsync(
                $"pecas/{pecaB.Id}/assistente/solicitacoes",
                JsonBody(CreateAiRequest(Guid.NewGuid())))
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("PRIVADO-ORG-B", body, StringComparison.Ordinal);
        }
        Assert.Equal("CONTEUDO-PRIVADO-ORG-B", contextoB.Conteudo);
        Assert.Equal(0, api.Storage.ReadCalls);
        Assert.Equal(0, api.Storage.DeleteCalls);
        Assert.Equal(0, api.Provider.CountCalls);
        Assert.Equal(0, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_OrganizationANaoAcessaEnvolvidosEnderecosNemListagensDaOrganizationB()
    {
        await using var api = await TestApi.CreateAsync();
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var casoB = CreateCaso(organizationB, "CASO-LISTAGEM-PRIVADA-B");
        var envolvidoB = new EnvolvidoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = casoB.Id,
            Nome = "ENVOLVIDO-PRIVADO-B",
            Tipo = TipoEnvolvidoCasoEnum.Cliente,
            Natureza = NaturezaPessoaEnum.PessoaFisica
        };
        var enderecoB = new EnderecoEnvolvidoCaso
        {
            Id = Guid.NewGuid(),
            EnvolvidoCasoId = envolvidoB.Id,
            Tipo = TipoEnderecoEnum.Residencial,
            Logradouro = "ENDERECO-PRIVADO-B",
            Cidade = "Cidade B"
        };
        api.Repository.Casos.Add(casoB);
        api.Repository.Envolvidos.Add(envolvidoB);
        api.Repository.Enderecos.Add(enderecoB);
        using var client = api.CreateClient(organizationA);

        var responses = new[]
        {
            await client.GetAsync($"casos/{casoB.Id}/envolvidos"),
            await client.GetAsync($"casos/{casoB.Id}/envolvidos/{envolvidoB.Id}"),
            await client.PutAsync(
                $"casos/{casoB.Id}/envolvidos/{envolvidoB.Id}",
                JsonBody(new AtualizarEnvolvidoCasoRequest(
                    "Tentativa",
                    TipoEnvolvidoCasoEnum.Cliente,
                    NaturezaPessoaEnum.PessoaFisica))),
            await client.DeleteAsync($"casos/{casoB.Id}/envolvidos/{envolvidoB.Id}"),
            await client.GetAsync($"casos/{casoB.Id}/envolvidos/{envolvidoB.Id}/enderecos"),
            await client.GetAsync($"casos/{casoB.Id}/envolvidos/{envolvidoB.Id}/enderecos/{enderecoB.Id}"),
            await client.PatchAsync(
                $"casos/{casoB.Id}/envolvidos/{envolvidoB.Id}/enderecos/{enderecoB.Id}",
                JsonBody(new AtualizarEnderecoEnvolvidoRequest(
                    TipoEnderecoEnum.Residencial,
                    false,
                    null,
                    "Rua tentativa",
                    null,
                    null,
                    null,
                    "Cidade tentativa",
                    "SP",
                    null))),
            await client.DeleteAsync($"casos/{casoB.Id}/envolvidos/{envolvidoB.Id}/enderecos/{enderecoB.Id}"),
            await client.GetAsync($"casos/{casoB.Id}/contextos"),
            await client.GetAsync($"casos/{casoB.Id}/documentos"),
            await client.GetAsync($"casos/{casoB.Id}/pecas"),
            await client.GetAsync($"casos/{casoB.Id}/contexto-completo")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("PRIVADO-B", body, StringComparison.Ordinal);
        }
        Assert.Equal("ENVOLVIDO-PRIVADO-B", envolvidoB.Nome);
        Assert.Equal("ENDERECO-PRIVADO-B", enderecoB.Logradouro);
    }

    [Fact]
    public async Task Http_EntradaAcimaDoLimiteRetorna413AntesDoSseELiberaSolicitacao()
    {
        await using var api = await TestApi.CreateAsync();
        var organizationId = Guid.NewGuid();
        var caso = CreateCaso(organizationId, "Caso A");
        var peca = CreatePeca(caso.Id, organizationId, "Peça A");
        api.Repository.Casos.Add(caso);
        api.Repository.Pecas.Add(peca);
        api.Provider.InputTokens = 60_001;
        var clientRequestId = Guid.NewGuid();
        using var client = api.CreateClient(organizationId);

        var rejected = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(clientRequestId)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        var rejectedBody = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("AI_INPUT_TOO_LARGE", rejectedBody, StringComparison.Ordinal);
        Assert.Equal(1, api.Provider.CountCalls);
        Assert.Equal(0, api.Provider.GenerateCalls);

        api.Provider.InputTokens = 100;
        var accepted = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(clientRequestId)));

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("text/event-stream", accepted.Content.Headers.ContentType?.MediaType);
        Assert.Contains("event: solicitacao.concluida", await accepted.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(2, api.Provider.CountCalls);
        Assert.Equal(1, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_PayloadExcessivoRetorna413AntesDoController()
    {
        await using var api = await TestApi.CreateAsync(settings => settings.MaxRequestBodyBytes = 100);
        using var client = api.CreateClient(Guid.NewGuid());
        using var content = new StringContent(new string('x', 101), Encoding.UTF8, "application/json");

        var response = await client.PostAsync(
            $"pecas/{Guid.NewGuid()}/assistente/solicitacoes",
            content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("AI_REQUEST_TOO_LARGE", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, api.Provider.CountCalls);
        Assert.Equal(0, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_ContextoExcessivoRetorna413AntesDoProvider()
    {
        await using var api = await TestApi.CreateAsync(settings => settings.MaxTotalContextCharacters = 40);
        var organizationId = Guid.NewGuid();
        var caso = CreateCaso(organizationId, "Caso A");
        var peca = CreatePeca(caso.Id, organizationId, "Peça A");
        api.Repository.Casos.Add(caso);
        api.Repository.Pecas.Add(peca);
        using var client = api.CreateClient(organizationId);

        var response = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("AI_CONTEXT_TOO_LARGE", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, api.Provider.CountCalls);
        Assert.Equal(0, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_UploadExcessivoRetorna413AntesDoStorage()
    {
        await using var api = await TestApi.CreateAsync(maxUploadBytes: 3);
        var organizationId = Guid.NewGuid();
        var caso = CreateCaso(organizationId, "Caso A");
        api.Repository.Casos.Add(caso);
        using var client = api.CreateClient(organizationId);
        using var multipart = new MultipartFormDataContent();
        using var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        multipart.Add(file, "Arquivo", "grande.txt");

        var response = await client.PostAsync($"casos/{caso.Id}/documentos", multipart);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, api.Storage.SaveCalls);
    }

    [Fact]
    public async Task Http_RateLimitRetorna429ComRetryAfter()
    {
        await using var api = await TestApi.CreateAsync(settings =>
        {
            settings.RequestsPerUserPerMinute = 1;
            settings.ConcurrentRequestsPerUser = 1;
        });
        var organizationId = Guid.NewGuid();
        var caso = CreateCaso(organizationId, "Caso A");
        var peca = CreatePeca(caso.Id, organizationId, "Peça A");
        api.Repository.Casos.Add(caso);
        api.Repository.Pecas.Add(peca);
        using var client = api.CreateClient(organizationId);

        var first = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));
        var second = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(Assert.Single(values), out var retryAfter) && retryAfter > 0);
        Assert.Contains("AI_USER_RATE_LIMITED", await second.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_FalhaDoProviderNaoExibeNemRegistraConteudoSensivel()
    {
        await using var api = await TestApi.CreateAsync();
        var organizationId = Guid.NewGuid();
        var caso = CreateCaso(organizationId, "Caso A");
        var peca = CreatePeca(caso.Id, organizationId, "Peça A");
        api.Repository.Casos.Add(caso);
        api.Repository.Pecas.Add(peca);
        const string sensitiveMarker = "API-KEY-E-PROMPT-SIGILOSOS";
        api.Provider.CountError = new AssistenteIaProviderException(
            "AI_UNAVAILABLE",
            retryable: true,
            innerException: new HttpRequestException(sensitiveMarker));
        using var client = api.CreateClient(organizationId);

        var response = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("AI_UNAVAILABLE", body, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMarker, body, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Logs.Messages, message =>
            message.Contains(sensitiveMarker, StringComparison.Ordinal));
        Assert.Equal(0, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_SaidaInvalidaDoModeloRetornaEventoSeguroELiberaConcorrencia()
    {
        await using var api = await TestApi.CreateAsync(settings =>
            settings.ConcurrentRequestsPerUser = 1);
        var organizationId = Guid.NewGuid();
        var caso = CreateCaso(organizationId, "Caso A");
        var peca = CreatePeca(caso.Id, organizationId, "Peça A");
        api.Repository.Casos.Add(caso);
        api.Repository.Pecas.Add(peca);
        const string invalidOutputMarker = "CONTEUDO-INVALIDO-DO-MODELO";
        api.Provider.Result = api.Provider.Result with
        {
            Texto = invalidOutputMarker,
            OperacaoSugerida = AssistenteOperacao.SubstituirSelecao
        };
        using var client = api.CreateClient(organizationId);

        var invalid = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var failureStream = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("event: solicitacao.falhou", failureStream, StringComparison.Ordinal);
        Assert.Contains("AI_PROVIDER_ERROR", failureStream, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidOutputMarker, failureStream, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Logs.Messages, message =>
            message.Contains(invalidOutputMarker, StringComparison.Ordinal));

        api.Provider.Result = api.Provider.Result with
        {
            Texto = "Conteúdo seguro",
            OperacaoSugerida = AssistenteOperacao.ResponderNoPainel
        };
        var retry = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Contains("event: solicitacao.concluida", await retry.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(2, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_TimeoutNoStreamRetornaErroControladoELiberaConcorrencia()
    {
        await using var api = await TestApi.CreateAsync(settings =>
        {
            settings.TimeoutSeconds = 1;
            settings.ConcurrentRequestsPerUser = 1;
        });
        var organizationId = Guid.NewGuid();
        var caso = CreateCaso(organizationId, "Caso A");
        var peca = CreatePeca(caso.Id, organizationId, "Peça A");
        api.Repository.Casos.Add(caso);
        api.Repository.Pecas.Add(peca);
        api.Provider.BlockGeneration = true;
        using var client = api.CreateClient(organizationId);

        var timedOut = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.OK, timedOut.StatusCode);
        Assert.Equal("text/event-stream", timedOut.Content.Headers.ContentType?.MediaType);
        var failureStream = await timedOut.Content.ReadAsStringAsync();
        Assert.Contains("event: solicitacao.falhou", failureStream, StringComparison.Ordinal);
        Assert.Contains("AI_PROVIDER_TIMEOUT", failureStream, StringComparison.Ordinal);

        api.Provider.BlockGeneration = false;
        var retry = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Contains("event: solicitacao.concluida", await retry.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(2, api.Provider.GenerateCalls);
    }

    [Fact]
    public async Task Http_DesconexaoDoClienteCancelaProviderELiberaConcorrencia()
    {
        await using var api = await TestApi.CreateAsync(settings =>
            settings.ConcurrentRequestsPerUser = 1);
        var organizationId = Guid.NewGuid();
        var caso = CreateCaso(organizationId, "Caso A");
        var peca = CreatePeca(caso.Id, organizationId, "Peça A");
        api.Repository.Casos.Add(caso);
        api.Repository.Pecas.Add(peca);
        api.Provider.BlockGeneration = true;
        using var client = api.CreateClient(organizationId);
        using var cancellation = new CancellationTokenSource();

        var interrupted = client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())),
            cancellation.Token);
        await api.Provider.GenerationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrupted);
        await api.Provider.GenerationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        api.Provider.BlockGeneration = false;
        await Task.Delay(50);

        var retry = await client.PostAsync(
            $"pecas/{peca.Id}/assistente/solicitacoes",
            JsonBody(CreateAiRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Contains("event: solicitacao.concluida", await retry.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(2, api.Provider.GenerateCalls);
    }

    private static CriarSolicitacaoAssistenteRequest CreateAiRequest(Guid clientRequestId) => new(
        clientRequestId,
        AssistenteModo.Perguntar,
        "Analise a solicitação.",
        1,
        AssistenteOperacao.ResponderNoPainel,
        new AssistenteContextoRequest(new AssistenteDocumentoRequest(AssistenteEscopoDocumento.Nenhum)));

    private static StringContent JsonBody<T>(T value) =>
        new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private static Caso CreateCaso(Guid organizationId, string title) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        CreatedById = Guid.NewGuid(),
        UpdatedById = Guid.NewGuid(),
        Titulo = title
    };

    private static Peca CreatePeca(Guid casoId, Guid organizationId, string title) => new()
    {
        Id = Guid.NewGuid(),
        CasoId = casoId,
        OrganizationId = organizationId,
        CreatedById = Guid.NewGuid(),
        UpdatedById = Guid.NewGuid(),
        Titulo = title,
        ConteudoJson = JsonDocument.Parse("""{"type":"doc","content":[]}"""),
        PageConfig = JsonDocument.Parse("""{"format":"A4"}"""),
        Styles = JsonDocument.Parse("{}"),
        Versao = 1
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class TestApi : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly Uri _baseAddress;

        private TestApi(
            WebApplication app,
            Uri baseAddress,
            TestRepository repository,
            TestFileStorage storage,
            TestProvider provider,
            CaptureLoggerProvider logs)
        {
            _app = app;
            _baseAddress = baseAddress;
            Repository = repository;
            Storage = storage;
            Provider = provider;
            Logs = logs;
        }

        public TestRepository Repository { get; }
        public TestFileStorage Storage { get; }
        public TestProvider Provider { get; }
        public CaptureLoggerProvider Logs { get; }

        public static async Task<TestApi> CreateAsync(
            Action<AssistenteIaSettings>? configure = null,
            long maxUploadBytes = 20 * 1024 * 1024)
        {
            var settings = new AssistenteIaSettings
            {
                Enabled = true,
                Provider = "OpenAI",
                Model = "modelo-http-teste",
                ApiKey = "chave-nao-real",
                MaxInputTokens = 60_000,
                MaxRequestBodyBytes = 512 * 1024,
                RequestsPerUserPerMinute = 10,
                ConcurrentRequestsPerUser = 2,
                RequestsPerOrganizationPerMinute = 30,
                ConcurrentRequestsPerOrganization = 6
            };
            configure?.Invoke(settings);
            var repository = new TestRepository();
            var storage = new TestFileStorage();
            var provider = new TestProvider();
            var logs = new CaptureLoggerProvider();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ApplicationName = typeof(AssistenteController).Assembly.FullName
            });
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(logs);
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(AssistenteController).Assembly)
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<IOptions<AssistenteIaSettings>>(Options.Create(settings));
            builder.Services.AddSingleton<IOptionsMonitor<AssistenteIaSettings>>(
                new StaticOptionsMonitor<AssistenteIaSettings>(settings));
            builder.Services.AddSingleton<IOptions<DocumentStorageSettings>>(
                Options.Create(new DocumentStorageSettings { MaxFileSizeBytes = maxUploadBytes }));
            builder.Services.AddSingleton(repository);
            builder.Services.AddSingleton<IPecaRepository>(repository);
            builder.Services.AddSingleton<ICasoRepository>(repository);
            builder.Services.AddSingleton(storage);
            builder.Services.AddSingleton<IFileStorage>(storage);
            builder.Services.AddSingleton(provider);
            builder.Services.AddSingleton<IAssistenteIaProvider>(provider);
            builder.Services.AddSingleton<IAssistenteTokenCounter>(provider);
            builder.Services.AddSingleton<AssistenteRequestRegistry>();
            builder.Services.AddSingleton<AssistentePromptPolicy>();
            builder.Services.AddSingleton<IAssistenteUsageLimiter, AssistenteUsageLimiter>();
            builder.Services.AddSingleton<IAssistenteFeatureAccess, AllowAllFeatureAccess>();
            builder.Services.AddSingleton<IAssistenteUsageRecorder, TestUsageRecorder>();
            builder.Services.AddScoped<IAuthContext, HttpAuthContext>();
            builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
            builder.Services.AddScoped<AssistenteContextBuilder>();
            builder.Services.AddScoped<AssistenteIaApplicationService>();
            builder.Services.AddScoped<CasoApplicationService>();
            builder.Services.AddScoped<PecaApplicationService>();

            var app = builder.Build();
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<ApiExceptionMiddleware>();
            app.UseMiddleware<AssistenteRequestSizeMiddleware>();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                ?? throw new InvalidOperationException("O servidor HTTP de teste não publicou um endereço.");
            return new TestApi(app, new Uri(address), repository, storage, provider, logs);
        }

        public HttpClient CreateClient(Guid organizationId)
        {
            var client = CreateAnonymousClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                TestAuthenticationHandler.SchemeName,
                "test-token");
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.OrganizationHeader, organizationId.ToString("D"));
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, Guid.NewGuid().ToString("D"));
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SessionHeader, Guid.NewGuid().ToString("D"));
            return client;
        }

        public HttpClient CreateAnonymousClient() => new() { BaseAddress = _baseAddress };

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            Logs.Dispose();
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string OrganizationHeader = "X-Test-Organization";
        public const string UserHeader = "X-Test-User";
        public const string SessionHeader = "X-Test-Session";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers[OrganizationHeader], out var organizationId) ||
                !Guid.TryParse(Request.Headers[UserHeader], out var userId) ||
                !Guid.TryParse(Request.Headers[SessionHeader], out var sessionId))
                return Task.FromResult(AuthenticateResult.Fail("Identidade de teste inválida."));

            var claims = new[]
            {
                new Claim("organizationId", organizationId.ToString("D")),
                new Claim("sub", userId.ToString("D")),
                new Claim("sessionId", sessionId.ToString("D")),
                new Claim("role", MembershipRoleEnum.Owner.ToString())
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class TestRepository : IPecaRepository, ICasoRepository
    {
        public List<Caso> Casos { get; } = [];
        public List<Peca> Pecas { get; } = [];
        public List<EnvolvidoCaso> Envolvidos { get; } = [];
        public List<EnderecoEnvolvidoCaso> Enderecos { get; } = [];
        public List<ContextoCaso> Contextos { get; } = [];
        public List<DocumentoCaso> Documentos { get; } = [];

        public Task<Caso?> ObterCasoAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Casos.SingleOrDefault(x => x.Id == id && x.OrganizationId == organizationId));

        public Task<Peca?> ObterPecaAsync(Guid id, Guid organizationId, CancellationToken ct) =>
            Task.FromResult(Pecas.SingleOrDefault(x => x.Id == id && x.OrganizationId == organizationId));

        public Task<ContextoCaso?> ObterContextoAsync(
            Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Casos.Any(x => x.Id == casoId && x.OrganizationId == organizationId)
                ? Contextos.SingleOrDefault(x => x.Id == id && x.CasoId == casoId)
                : null);

        public Task<DocumentoCaso?> ObterDocumentoAsync(
            Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Casos.Any(x => x.Id == casoId && x.OrganizationId == organizationId)
                ? Documentos.SingleOrDefault(x => x.Id == id && x.CasoId == casoId)
                : null);

        public Task<bool> AtualizarContextoAsync(
            ContextoCaso contexto, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Casos.Any(x => x.Id == contexto.CasoId && x.OrganizationId == organizationId) &&
                            Contextos.Any(x => x.Id == contexto.Id && x.CasoId == contexto.CasoId));

        public Task<bool> RemoverContextoAsync(
            Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(RemoveIfAuthorized(Contextos, Casos, casoId, id, organizationId, x => x.Id, x => x.CasoId));

        public Task<bool> RemoverDocumentoAsync(
            Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(RemoveIfAuthorized(Documentos, Casos, casoId, id, organizationId, x => x.Id, x => x.CasoId));

        public Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(
            Guid casoId, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EnvolvidoCaso>>(
                Casos.Any(x => x.Id == casoId && x.OrganizationId == organizationId)
                    ? Envolvidos.Where(x => x.CasoId == casoId).ToList()
                    : []);

        public Task<bool> CasoExisteAsync(Guid casoId, Guid organizationId, CancellationToken ct) =>
            Task.FromResult(Casos.Any(x => x.Id == casoId && x.OrganizationId == organizationId));

        public Task<Caso> CriarCasoAsync(Caso caso, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Caso>> ListarCasosAsync(Guid organizationId, CasoStatusEnum? status, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ArquivarCasoAsync(Guid id, Guid organizationId, Guid updatedById, DateTime dataAtualizacao, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EnvolvidoCaso> CriarEnvolvidoAsync(EnvolvidoCaso envolvido, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EnvolvidoCaso?> ObterEnvolvidoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Casos.Any(x => x.Id == casoId && x.OrganizationId == organizationId)
                ? Envolvidos.SingleOrDefault(x => x.Id == id && x.CasoId == casoId)
                : null);
        public Task<bool> AtualizarEnvolvidoAsync(EnvolvidoCaso envolvido, Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoverEnvolvidoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EnderecoEnvolvidoCaso> CriarEnderecoAsync(Guid casoId, Guid organizationId, EnderecoEnvolvidoCaso endereco, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosAsync(Guid casoId, Guid envolvidoId, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EnderecoEnvolvidoCaso>>(
                Casos.Any(x => x.Id == casoId && x.OrganizationId == organizationId) &&
                Envolvidos.Any(x => x.Id == envolvidoId && x.CasoId == casoId)
                    ? Enderecos.Where(x => x.EnvolvidoCasoId == envolvidoId).ToList()
                    : []);
        public Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosCasoAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EnderecoEnvolvidoCaso>>(
                Casos.Any(x => x.Id == casoId && x.OrganizationId == organizationId)
                    ? Enderecos.Where(x => Envolvidos.Any(e => e.CasoId == casoId && e.Id == x.EnvolvidoCasoId)).ToList()
                    : []);
        public Task<EnderecoEnvolvidoCaso?> ObterEnderecoAsync(Guid casoId, Guid envolvidoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Casos.Any(x => x.Id == casoId && x.OrganizationId == organizationId) &&
                            Envolvidos.Any(x => x.Id == envolvidoId && x.CasoId == casoId)
                ? Enderecos.SingleOrDefault(x => x.Id == id && x.EnvolvidoCasoId == envolvidoId)
                : null);
        public Task<bool> AtualizarEnderecoAsync(Guid casoId, Guid organizationId, EnderecoEnvolvidoCaso endereco, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoverEnderecoAsync(Guid casoId, Guid envolvidoId, Guid id, Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContextoCaso> CriarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DocumentoCaso> CriarDocumentoAsync(DocumentoCaso documento, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Peca> CriarPecaAsync(Peca peca, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PecaListaDto>> ListarPecasAsync(Guid casoId, Guid organizationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> AtualizarPecaAsync(Peca peca, int versaoEsperada, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExcluirPecaAsync(Guid id, Guid organizationId, Guid updatedById, int versaoEsperada, DateTime agora, CancellationToken ct) => throw new NotSupportedException();
        public Task<TemplatePeca> CriarTemplateAsync(TemplatePeca template, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<TemplatePeca>> ListarTemplatesAsync(Guid organizationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<TemplatePeca?> ObterTemplateAsync(Guid id, Guid organizationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> AtualizarTemplateAsync(TemplatePeca template, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExcluirTemplateAsync(Guid id, Guid organizationId, Guid updatedById, DateTime agora, CancellationToken ct) => throw new NotSupportedException();

        private static bool RemoveIfAuthorized<T>(
            ICollection<T> items,
            IReadOnlyCollection<Caso> cases,
            Guid caseId,
            Guid id,
            Guid organizationId,
            Func<T, Guid> idSelector,
            Func<T, Guid> caseSelector)
        {
            if (!cases.Any(x => x.Id == caseId && x.OrganizationId == organizationId)) return false;
            var item = items.SingleOrDefault(x => idSelector(x) == id && caseSelector(x) == caseId);
            return item is not null && items.Remove(item);
        }
    }

    private sealed class TestFileStorage : IFileStorage
    {
        public int SaveCalls { get; private set; }
        public int ReadCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public Task SaveAsync(string storageId, Stream content, CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
        public Task<Stream> GetAsync(string storageId, CancellationToken cancellationToken)
        {
            ReadCalls++;
            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        }
        public Task DeleteAsync(string storageId, CancellationToken cancellationToken)
        {
            DeleteCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestProvider : IAssistenteIaProvider, IAssistenteTokenCounter
    {
        public int InputTokens { get; set; } = 100;
        public int CountCalls { get; private set; }
        public int GenerateCalls { get; private set; }
        public Exception? CountError { get; set; }
        public bool BlockGeneration { get; set; }
        public AssistenteIaProviderResult Result { get; set; } = new(
            "Resposta",
            "Conteúdo seguro",
            "Resumo",
            AssistenteOperacao.ResponderNoPainel,
            [],
            [],
            100,
            0,
            20,
            "modelo-http-teste");
        public TaskCompletionSource<bool> GenerationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> GenerationCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> ContarTokensEntradaAsync(
            AssistenteIaProviderRequest request,
            CancellationToken cancellationToken)
        {
            CountCalls++;
            return CountError is null
                ? Task.FromResult(InputTokens)
                : Task.FromException<int>(CountError);
        }

        public async Task<AssistenteIaProviderResult> GerarAsync(
            AssistenteIaProviderRequest request,
            CancellationToken cancellationToken)
        {
            GenerateCalls++;
            if (BlockGeneration)
            {
                GenerationStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    GenerationCancelled.TrySetResult(true);
                    throw;
                }
            }
            return Result with { EntradaTokens = InputTokens };
        }
    }

    private sealed class AllowAllFeatureAccess : IAssistenteFeatureAccess
    {
        public Task<AssistenteFeatureSnapshot> ObterAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(new AssistenteFeatureSnapshot(
                true,
                [OrganizationFeatureEnum.AI_ASSISTANT, OrganizationFeatureEnum.AI_EDIT, OrganizationFeatureEnum.AI_REVIEW],
                Enum.GetValues<AssistenteModo>()));

        public Task ExigirModoAsync(Guid organizationId, AssistenteModo modo, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestUsageRecorder : IAssistenteUsageRecorder
    {
        public Task RegistrarAsync(AssistenteUsageRecord record, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
        public void Dispose() { }

        private sealed class CaptureLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                if (exception is not null) message += Environment.NewLine + exception;
                lock (messages) messages.Add(message);
            }
        }
    }
}
