using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Observability;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace JuridicoAI.Api.Tests;

[CollectionDefinition("Monitoring", DisableParallelization = true)]
public sealed class MonitoringCollection;

[Collection("Monitoring")]
public sealed class AssistenteMonitoringTests
{
    [Fact]
    public void ExportadorOtlp_TransmiteApenasMetricasPermitidasEFalhaSemInterromperOperacao()
    {
        var wire = new MonitoringWireHandler();
        using var provider = Sdk.CreateMeterProviderBuilder().SetResourceBuilder(ResourceBuilder.CreateEmpty())
            .AddMeter(AssistenteTelemetry.MeterName)
            .AddOtlpExporter((exporter, reader) =>
            {
                exporter.Endpoint = new Uri("https://synthetic.test/v1/metrics");
                exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                exporter.HttpClientFactory = () => new HttpClient(wire);
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 60000;
            }).Build();
        AssistenteTelemetry.Shared.Stage("PRIVATE_DOCUMENT_CPF_JWT_API_KEY", 100);
        Assert.True(provider.ForceFlush(2000));
        Assert.NotEmpty(wire.Payloads);
        var transmitted = string.Join("\n", wire.Payloads.Select(Encoding.UTF8.GetString));
        Assert.Contains("ai.stage.duration", transmitted);
        Assert.DoesNotContain("PRIVATE_", transmitted);
        wire.Fail = true;
        AssistenteTelemetry.Shared.Stage("generation.context", 20);
        Assert.False(provider.ForceFlush(2000));
        // Business operations can continue while export is failing.
        AssistenteTelemetry.Shared.ProviderTokens(10, 2, 3);
    }

    [Fact]
    public void Metricas_UsamDimensoesLimitadasSemConteudoECacheNaoSomaNaEntrada()
    {
        var values = new List<(string Name, double Value, string Tags)>();
        using var listener = Listen(values);
        AssistenteTelemetry.Shared.Stage("PRIVATE_PROMPT", 1200);
        AssistenteTelemetry.Shared.OperationalEvent("PRIVATE_EXCEPTION");
        AssistenteTelemetry.Shared.ProviderTokens(100, 20, 30);
        Assert.Contains(values, p => p.Name == "ai.stage.duration" && p.Value == 1.2 && p.Tags.Contains("other"));
        Assert.Contains(values, p => p.Name == "ai.provider.tokens" && p.Value == 100 && p.Tags.Contains("input"));
        Assert.Contains(values, p => p.Name == "ai.provider.tokens" && p.Value == 20 && p.Tags.Contains("cached_input"));
        Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(values.Select(p => new { p.Name, p.Value, p.Tags })));
    }

    [Fact]
    public void FalhaDoListener_NaoInterrompeMedicaoNemOperacao()
    {
        using var listener = new MeterListener();
        listener.InstrumentPublished = (i, l) => { if (i.Meter.Name == AssistenteTelemetry.MeterName) l.EnableMeasurementEvents(i); };
        listener.SetMeasurementEventCallback<double>((_, _, _, _) => throw new InvalidOperationException("PRIVATE_ERROR"));
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("PRIVATE_ERROR"));
        listener.Start();
        AssistenteTelemetry.Shared.Stage("generation.context", 5);
        AssistenteTelemetry.Shared.ProviderTokens(10, 1, 2);
        AssistenteTelemetry.Shared.WorkerFailure(AssistenteWorkerKind.Deletion);
        using var measurement = AssistenteTelemetry.MeasureProvider(AssistenteProviderOperation.Generation);
        measurement.Dispatched();
        measurement.Succeeded();
    }

    [Fact]
    public async Task ColetaFalha_PreservaSnapshotERegistraFalhaSemExporExcecao()
    {
        var clock = new MonitoringClock(DateTimeOffset.Parse("2026-11-19T10:00:00Z"));
        using var state = new AssistenteMonitoringState(clock);
        var values = new List<(string Name, double Value, string Tags)>();
        using var listener = Listen(values);
        listener.RecordObservableInstruments();
        Assert.Contains(values, p => p.Name == "ai_monitoring_snapshot_available" && p.Value == 0);
        Assert.DoesNotContain(values, p => p.Name == "ai_jobs");
        var snapshot = new AssistenteMonitoringSnapshot(clock.GetUtcNow().UtcDateTime, [new("ai_jobs", 2, "NaFila", "generation")]);
        state.Publish(snapshot);
        var services = new ServiceCollection().AddScoped<AssistenteMonitoringCollector>(_ => throw new Exception("PRIVATE_CONNECTION_STRING")).BuildServiceProvider();
        await using (services)
        {
            var worker = new AssistenteMonitoringWorker(services.GetRequiredService<IServiceScopeFactory>(), state,
                Options.Create(new AssistenteMonitoringSettings()), clock, NullLogger<AssistenteMonitoringWorker>.Instance);
            clock.Advance(TimeSpan.FromMinutes(4));
            await worker.CollectOnceAsync(default);
        }
        Assert.Same(snapshot, state.Snapshot);
        listener.RecordObservableInstruments();
        Assert.Contains(values, p => p.Name == "ai_monitoring_snapshot_age_seconds" && p.Value == 240);
        Assert.Contains(values, p => p.Name == "ai_monitoring_collection_failures" && p.Value == 1);
        Assert.Contains(values, p => p.Name == "ai_jobs" && p.Value == 2);
        Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(values.Select(p => new { p.Name, p.Value, p.Tags })));
    }

    [Fact]
    public async Task Provedor_ContaChamadasFisicasSeparadasDaContagemDeTokensEConheceUsageMesmoComSaidaInvalida()
    {
        var values = new List<(string Name, double Value, string Tags)>();
        using var listener = Listen(values);
        var handler = new MonitoringHandler(message => message.RequestUri!.AbsolutePath.EndsWith("input_tokens")
            ? new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":100}") }
            : new(HttpStatusCode.OK) { Content = new StringContent("data: {\"type\":\"response.output_text.delta\",\"delta\":\"PRIVATE_INVALID_OUTPUT\"}\n\ndata: {\"type\":\"response.completed\",\"response\":{\"model\":\"fixture\",\"usage\":{\"input_tokens\":100,\"output_tokens\":30,\"input_tokens_details\":{\"cached_tokens\":20}}}}\n\n", Encoding.UTF8, "text/event-stream") });
        var provider = new OpenAiAssistenteIaProvider(new HttpClient(handler) { BaseAddress = new("https://synthetic.test/v1/") },
            Options.Create(new AssistenteIaSettings { Model = "fixture", ApiKey = "PRIVATE_API_KEY" }));
        var request = new AssistenteIaProviderRequest("PRIVATE_POLICY", "PRIVATE_DOCUMENT", AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel], 1000, true);
        Assert.Equal(100, await provider.ContarTokensEntradaAsync(request, default));
        await Assert.ThrowsAsync<AssistenteIaProviderException>(() => provider.GerarAsync(request, default));
        var calls = values.Where(p => p.Name == "ai.provider.call.duration").ToArray();
        Assert.Equal(2, calls.Length);
        Assert.Contains(calls, p => p.Tags.Contains("input_tokens") && p.Tags.Contains("succeeded"));
        Assert.Contains(calls, p => p.Tags.Contains("generation") && p.Tags.Contains("failed"));
        Assert.Equal(3, values.Count(p => p.Name == "ai.provider.tokens"));
        Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(values.Select(p => new { p.Name, p.Value, p.Tags })));
    }

    [Fact]
    public async Task Provedor_CancelamentoAntesDoEnvioNaoContaChamadaFisica()
    {
        var values = new List<(string Name, double Value, string Tags)>();
        using var listener = Listen(values);
        var provider = new OpenAiAssistenteIaProvider(new HttpClient(new MonitoringHandler(_ => throw new Exception("Should not send")))
            { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(new AssistenteIaSettings { Model = "fixture" }));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GerarAsync(new("policy", "document", AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel], 1000, true), cancellation.Token));
        Assert.DoesNotContain(values, p => p.Name == "ai.provider.call.duration" || p.Name == "ai.provider.usage.unknown");
    }

    private static MeterListener Listen(List<(string Name, double Value, string Tags)> values)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (i, l) => { if (i.Meter.Name == AssistenteTelemetry.MeterName) l.EnableMeasurementEvents(i); };
        listener.SetMeasurementEventCallback<double>((i, v, t, _) => values.Add((i.Name, v, JsonSerializer.Serialize(t.ToArray()))));
        listener.SetMeasurementEventCallback<long>((i, v, t, _) => values.Add((i.Name, v, JsonSerializer.Serialize(t.ToArray()))));
        listener.SetMeasurementEventCallback<int>((i, v, t, _) => values.Add((i.Name, v, JsonSerializer.Serialize(t.ToArray()))));
        listener.Start();
        return listener;
    }

    private sealed class MonitoringHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
    private sealed class MonitoringWireHandler : HttpMessageHandler
    {
        public List<byte[]> Payloads { get; } = [];
        public bool Fail { get; set; }
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken ct)
        {
            Payloads.Add(request.Content!.ReadAsByteArrayAsync(ct).GetAwaiter().GetResult());
            var response = new HttpResponseMessage(Fail ? HttpStatusCode.BadRequest : HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentType = new("application/x-protobuf");
            return response;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(Send(request, ct));
    }
}

public sealed class MonitoringClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan value) => now += value;
}
