using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class AssistenteTransparencyServiceTests
{
    [Fact]
    public async Task Documentos_ExigemNovoAceiteComFinalidadeExplicita()
    {
        var setup = Create();
        await setup.Service.ConfirmarAsync(new("ai-transparency-v1"), default);
        var service = new AssistenteTransparencyService(setup.Repository, new TestAuthContext(), setup.Options,
            Microsoft.Extensions.Options.Options.Create(new AssistenteDocumentSettings { Enabled = true }));
        var notice = await service.ObterAsync(default);
        Assert.False(notice.Confirmado); Assert.EndsWith("-documents-v1", notice.Versao);
        Assert.Contains("Anexos", notice.Mensagem); Assert.Contains("provedor", notice.Mensagem);
        Assert.Equal("AI_TRANSPARENCY_ACK_REQUIRED", (await Assert.ThrowsAsync<AssistenteIaException>(() => service.ExigirConfirmacaoAsync(default))).Code);
    }
    [Fact]
    public async Task ConversaPersistida_NaoReutilizaAceiteLimitadoAPropostas()
    {
        var setup = Create();
        setup.Options.Current.ReviewV2Enabled = true;
        await setup.Service.ConfirmarAsync(new("ai-transparency-v1-review-v2"), default);
        setup.Options.Current.ConversationV2Enabled = true;
        var notice = await setup.Service.ObterAsync(default);
        Assert.False(notice.Confirmado);
        Assert.Equal("ai-transparency-v1-conversation-v2", notice.Versao);
        Assert.Contains("histórico dessa conversa será utilizado", notice.Mensagem);
    }
    [Fact]
    public async Task RevisaoPersistida_ExigeAvisoNovoSobreArmazenamento()
    {
        var setup = Create();
        await setup.Service.ConfirmarAsync(new("ai-transparency-v1"), default);
        setup.Options.Current.ReviewV2Enabled = true;
        var status = await setup.Service.ObterAsync(default);
        Assert.False(status.Confirmado);
        Assert.Equal("ai-transparency-v1-review-v2", status.Versao);
        Assert.Contains("salvos em uma conversa individual", status.Mensagem);
        await Assert.ThrowsAsync<AssistenteIaException>(() => setup.Service.ExigirConfirmacaoAsync(default));
    }
    [Fact]
    public async Task Confirmar_EIdempotenteParaUsuarioOrganizationEVersao()
    {
        var setup = Create();

        var initial = await setup.Service.ObterAsync(default);
        var first = await setup.Service.ConfirmarAsync(
            new ConfirmarAssistenteTransparenciaRequest("ai-transparency-v1"),
            default);
        var second = await setup.Service.ConfirmarAsync(
            new ConfirmarAssistenteTransparenciaRequest("ai-transparency-v1"),
            default);

        Assert.False(initial.Confirmado);
        Assert.True(first.Confirmado);
        Assert.Equal(first.ConfirmadoEm, second.ConfirmadoEm);
        Assert.Single(setup.Repository.Items);
    }

    [Fact]
    public async Task Confirmacao_NaoValeParaOutroUsuarioOuOrganization()
    {
        var repository = new TransparencyRepositoryStub();
        var settings = new MutableOptionsMonitor<AssistenteIaSettings>(new AssistenteIaSettings());
        var original = new TestAuthContext();
        var originalService = new AssistenteTransparencyService(repository, original, settings);
        await originalService.ConfirmarAsync(
            new ConfirmarAssistenteTransparenciaRequest("ai-transparency-v1"),
            default);

        var otherUser = new AssistenteTransparencyService(
            repository,
            new TestAuthContext(original.OrganizationId, Guid.NewGuid()),
            settings);
        var otherOrganization = new AssistenteTransparencyService(
            repository,
            new TestAuthContext(Guid.NewGuid(), original.UserId),
            settings);

        Assert.False((await otherUser.ObterAsync(default)).Confirmado);
        Assert.False((await otherOrganization.ObterAsync(default)).Confirmado);
    }

    [Fact]
    public async Task NovaVersao_ExigeNovaConfirmacao()
    {
        var setup = Create();
        await setup.Service.ConfirmarAsync(
            new ConfirmarAssistenteTransparenciaRequest("ai-transparency-v1"),
            default);
        setup.Options.Current = new AssistenteIaSettings
        {
            TransparencyNoticeVersion = "ai-transparency-v2"
        };

        var status = await setup.Service.ObterAsync(default);
        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            setup.Service.ExigirConfirmacaoAsync(default));

        Assert.False(status.Confirmado);
        Assert.Equal("ai-transparency-v2", status.Versao);
        Assert.Equal("AI_TRANSPARENCY_ACK_REQUIRED", exception.Code);
        Assert.Equal(428, exception.StatusCode);
    }

    [Fact]
    public async Task Confirmar_RejeitaVersaoDesatualizada()
    {
        var setup = Create();

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            setup.Service.ConfirmarAsync(
                new ConfirmarAssistenteTransparenciaRequest("ai-transparency-v0"),
                default));

        Assert.Equal("AI_TRANSPARENCY_NOTICE_OUTDATED", exception.Code);
        Assert.Equal(409, exception.StatusCode);
        Assert.Empty(setup.Repository.Items);
    }

    private static Setup Create()
    {
        var repository = new TransparencyRepositoryStub();
        var options = new MutableOptionsMonitor<AssistenteIaSettings>(new AssistenteIaSettings());
        var service = new AssistenteTransparencyService(repository, new TestAuthContext(), options);
        return new Setup(service, repository, options);
    }

    private sealed record Setup(
        AssistenteTransparencyService Service,
        TransparencyRepositoryStub Repository,
        MutableOptionsMonitor<AssistenteIaSettings> Options);

    private sealed class TransparencyRepositoryStub : IAiTransparencyAcknowledgementRepository
    {
        public List<AiTransparencyAcknowledgement> Items { get; } = [];

        public Task<AiTransparencyAcknowledgement?> ObterAsync(
            Guid organizationId,
            Guid userId,
            string noticeVersion,
            CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(x =>
                x.OrganizationId == organizationId &&
                x.UserId == userId &&
                x.NoticeVersion == noticeVersion));

        public Task RegistrarAsync(
            AiTransparencyAcknowledgement acknowledgement,
            CancellationToken cancellationToken)
        {
            if (!Items.Any(x => x.OrganizationId == acknowledgement.OrganizationId &&
                                x.UserId == acknowledgement.UserId &&
                                x.NoticeVersion == acknowledgement.NoticeVersion))
                Items.Add(acknowledgement);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableOptionsMonitor<T>(T current) : IOptionsMonitor<T>
    {
        public T Current { get; set; } = current;
        public T CurrentValue => Current;
        public T Get(string? name) => Current;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
