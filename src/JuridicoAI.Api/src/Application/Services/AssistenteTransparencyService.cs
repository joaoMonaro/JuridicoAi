using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteTransparencyService(
    IAiTransparencyAcknowledgementRepository repository,
    IAuthContext authContext,
    IOptionsMonitor<AssistenteIaSettings> options) : IAssistenteTransparencyService
{
    private const string Title = "Uso de inteligência artificial";
    private const string Message =
        "Algumas informações do caso poderão ser processadas por provedores de inteligência artificial " +
        "para gerar, revisar ou analisar conteúdo jurídico. A plataforma procura enviar apenas as " +
        "informações necessárias à execução da solicitação e pode aplicar técnicas de minimização e " +
        "pseudonimização de dados.";
    private const string ReviewNotice =
        "As respostas geradas por inteligência artificial podem conter erros e devem ser revisadas " +
        "pelo profissional responsável.";

    private AssistenteIaSettings Settings => options.CurrentValue;
    private string CurrentVersion => Settings.TransparencyNoticeVersion + (Settings.ConversationV2Enabled ? "-conversation-v2" : Settings.ReviewV2Enabled ? "-review-v2" : string.Empty);

    public async Task<AssistenteTransparenciaDto> ObterAsync(CancellationToken cancellationToken)
    {
        var acknowledgement = await GetCurrentAsync(cancellationToken);
        return CreateDto(acknowledgement);
    }

    public async Task<AssistenteTransparenciaDto> ConfirmarAsync(
        ConfirmarAssistenteTransparenciaRequest request,
        CancellationToken cancellationToken)
    {
        var currentVersion = CurrentVersion;
        if (string.IsNullOrWhiteSpace(request.Versao) ||
            !string.Equals(request.Versao.Trim(), currentVersion, StringComparison.Ordinal))
            throw new AssistenteIaException(
                StatusCodes.Status409Conflict,
                "AI_TRANSPARENCY_NOTICE_OUTDATED",
                "O aviso de uso da IA foi atualizado. Leia a versão vigente antes de continuar.");

        var acknowledgement = await GetCurrentAsync(cancellationToken);
        if (acknowledgement is null)
        {
            acknowledgement = new AiTransparencyAcknowledgement
            {
                Id = Guid.NewGuid(),
                OrganizationId = authContext.OrganizationId,
                UserId = authContext.UserId,
                NoticeVersion = currentVersion,
                AcknowledgedAt = DateTime.UtcNow
            };
            await repository.RegistrarAsync(acknowledgement, cancellationToken);
            acknowledgement = await GetCurrentAsync(cancellationToken) ?? acknowledgement;
        }

        return CreateDto(acknowledgement);
    }

    public async Task ExigirConfirmacaoAsync(CancellationToken cancellationToken)
    {
        if (await GetCurrentAsync(cancellationToken) is not null) return;

        throw new AssistenteIaException(
            StatusCodes.Status428PreconditionRequired,
            "AI_TRANSPARENCY_ACK_REQUIRED",
            "Leia e confirme o aviso de uso da inteligência artificial antes de continuar.");
    }

    private Task<AiTransparencyAcknowledgement?> GetCurrentAsync(CancellationToken cancellationToken) =>
        repository.ObterAsync(
            authContext.OrganizationId,
            authContext.UserId,
            CurrentVersion,
            cancellationToken);

    private AssistenteTransparenciaDto CreateDto(AiTransparencyAcknowledgement? acknowledgement) => new(
        CurrentVersion,
        acknowledgement is not null,
        acknowledgement?.AcknowledgedAt,
        Title,
        Message + (Settings.ConversationV2Enabled
            ? " Mensagens, respostas e propostas serão salvas em uma conversa individual vinculada à Peça. O histórico dessa conversa será utilizado como contexto em novos pedidos."
            : Settings.ReviewV2Enabled
            ? " Propostas de alteração e pedidos associados serão salvos em uma conversa individual vinculada à Peça."
            : string.Empty),
        ReviewNotice,
        Settings.PrivacyPolicyUrl);
}
