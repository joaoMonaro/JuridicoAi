using System.Security.Cryptography;
using System.Text.Json.Nodes;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Infrastructure.Recovery;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.Api.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
namespace JuridicoAI.Api.Tests;

public sealed class DeletionJournalTests
{
    [Fact]
    public async Task Registro_PersisteApenasMetadadosAssinadosEReenvioNaoDuplica()
    {
        using var f = new DeletionJournalFixture(); await f.Journal.InitializeAsync(default);
        var record = Authorization(); await f.Journal.AppendAsync(record, default); await f.Journal.AppendAsync(record, default);
        var snapshot = await f.NewJournal().ReadAsync(default);
        Assert.Equal(record, Assert.Single(snapshot.Records)); Assert.Equal(64, snapshot.Sha256.Length);
        var payload = JsonNode.Parse(await File.ReadAllTextAsync(f.FilePath))!;
        Assert.Equal(new[] { "authorizedAt", "conversationId", "requestId", "userId" }, payload["records"]![0]!.AsObject().Select(p => p.Key).Order().ToArray());
        Assert.DoesNotContain(f.Settings.IntegrityKeyBase64, payload.ToJsonString());
        if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(f.FilePath));
    }
    [Fact]
    public async Task Concorrencia_InstanciasDiferentesNaoPerdemAutorizacoes()
    {
        using var f = new DeletionJournalFixture(); await f.Journal.InitializeAsync(default);
        var records = Enumerable.Range(0, 12).Select(_ => Authorization()).ToArray();
        await Task.WhenAll(records.Select(r => f.NewJournal().AppendAsync(r, default)));
        Assert.Equal(12, (await f.Journal.ReadAsync(default)).Records.Count);
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.AppendAsync(records[0] with { UserId = Guid.NewGuid() }, default));
    }
    [Fact]
    public async Task AdulteracaoOmissaoChaveOuEscopoErradoSaoRejeitados()
    {
        using var f = new DeletionJournalFixture(); await f.Journal.InitializeAsync(default); await f.Journal.AppendAsync(Authorization(), default);
        var original = await File.ReadAllBytesAsync(f.FilePath);
        var json = JsonNode.Parse(original)!; json["records"]!.AsArray().Clear(); await File.WriteAllTextAsync(f.FilePath, json.ToJsonString());
        Assert.Equal("AI_DELETION_JOURNAL_UNAVAILABLE", (await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.ReadAsync(default))).Code);
        await File.WriteAllBytesAsync(f.FilePath, original);
        var key = f.Settings.IntegrityKeyBase64; f.Settings.IntegrityKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.ReadAsync(default)); f.Settings.IntegrityKeyBase64 = key;
        f.Settings.ScopeId = Guid.NewGuid(); await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.ReadAsync(default));
    }
    [Fact]
    public async Task RegistroAusente_NaoRecriaSilenciosamenteNemConfirmaExclusao()
    {
        using var f = new DeletionJournalFixture();
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.ReadAsync(default));
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.AppendAsync(Authorization(), default));
        Assert.False(File.Exists(f.FilePath));
        await f.Journal.InitializeAsync(default); File.Delete(f.FilePath);
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.AppendAsync(Authorization(), default));
    }
    [Fact]
    public async Task RegistroDentroDosDocumentosOuAtravesDeLinkEhRejeitado()
    {
        using var f = new DeletionJournalFixture();
        f.Settings.JournalDirectory = Path.Combine(f.Documents, "journal");
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.InitializeAsync(default));
        if (OperatingSystem.IsWindows()) return;
        var target = Path.Combine(f.Root, "target"); Directory.CreateDirectory(target);
        var link = Path.Combine(f.Root, "link"); Directory.CreateSymbolicLink(link, target);
        f.Settings.JournalDirectory = link;
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Journal.InitializeAsync(default));
    }
    [Fact]
    public async Task Catalogo_LocalizaArquivosOrfaosDoChatSemIncluirCasoOuOutrosChats()
    {
        using var f = new DeletionJournalFixture(); var conversation = Guid.NewGuid(); var organization = Guid.NewGuid();
        var own = $"assistant/{organization:N}/{conversation:N}/privado.txt";
        await f.Storage.SaveAsync(own, new MemoryStream([1,2]), default);
        await f.Storage.SaveAsync("case/compartilhado.txt", new MemoryStream([3]), default);
        await f.Storage.SaveAsync($"assistant/{organization:N}/{Guid.NewGuid():N}/outro.txt", new MemoryStream([4]), default);
        Assert.Equal(own, Assert.Single(await f.Catalog.ListAsync(conversation, default)));
        if (OperatingSystem.IsWindows()) return;
        File.CreateSymbolicLink(Path.Combine(f.Documents, own.Replace('/', Path.DirectorySeparatorChar) + ".link"), Path.Combine(f.Documents,"case/compartilhado.txt"));
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Catalog.ListAsync(conversation, default));
        var linkedDirectory = Path.Combine(f.Documents, $"assistant/{organization:N}/{conversation:N}/linked-case");
        Directory.CreateSymbolicLink(linkedDirectory, Path.Combine(f.Documents, "case"));
        await Assert.ThrowsAsync<AssistenteIaException>(() => f.Storage.DeleteAsync($"assistant/{organization:N}/{conversation:N}/linked-case/compartilhado.txt", default));
        Assert.True(File.Exists(Path.Combine(f.Documents,"case/compartilhado.txt")));
    }
    public static AssistenteDeletionAuthorization Authorization()
    {
        var now = DateTime.UtcNow;
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc));
    }
}
public sealed class DeletionJournalFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "juridicoai-p05-" + Guid.NewGuid().ToString("N"));
    public string Documents => Path.Combine(Root, "app", "documents");
    public string FilePath => Path.Combine(Settings.JournalDirectory, "authorizations.json");
    public DeletionProtectionSettings Settings { get; }
    public DocumentStorageSettings StorageSettings => new() { RootPath = Documents };
    public IHostEnvironment Environment { get; }
    public FileDeletionJournal Journal { get; }
    public LocalFileStorage Storage { get; }
    public PrivateConversationFileCatalog Catalog { get; }
    public DeletionJournalFixture()
    {
        Directory.CreateDirectory(Path.Combine(Root,"app"));
        Environment = new FixtureHost(Path.Combine(Root,"app"));
        Settings = new() { JournalDirectory = Path.Combine(Root,"independent"), ScopeId = Guid.NewGuid(), IntegrityKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        Journal = NewJournal(); Storage = new(Options.Create(StorageSettings), Environment); Catalog = new(Options.Create(StorageSettings), Environment);
    }
    public FileDeletionJournal NewJournal() => new(Options.Create(Settings), Options.Create(StorageSettings), Environment);
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    private sealed class FixtureHost(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "P05Fixture";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
