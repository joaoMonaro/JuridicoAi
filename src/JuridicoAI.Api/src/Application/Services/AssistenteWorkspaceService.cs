using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteWorkspaceService(
    IAssistenteWorkspaceRepository repository, IPecaRepository pecas, IAuthContext auth,
    IAssistenteFeatureAccess features, IAssistenteTransparencyService transparency,
    IOptionsMonitor<AssistenteIaSettings> settings, AssistentePatchCompiler compiler, AssistenteDocumentService? documents = null)
{
    public async Task<AssistenteConversaDto> ObterOuCriarAsync(Guid pecaId, CancellationToken ct)
    {
        ExigirRollout();
        await features.ExigirModoAsync(auth.OrganizationId, AssistenteModo.Perguntar, ct);
        var peca = await PecaAsync(pecaId, ct);
        var item = await repository.ObterOuCriarAsync(auth.OrganizationId, auth.UserId, peca.Id, peca.Titulo, ct);
        return new(item.Id, item.PecaId, item.Titulo, item.CriadaEm);
    }

    public async Task<IReadOnlyList<AssistenteMensagemDto>> MensagensAsync(Guid conversaId, long? antes, int limite, CancellationToken ct)
    {
        await ConversaAsync(conversaId, false, ct);
        if (limite is < 1 or > 100 || antes is < 1) throw AssistentePatchCompiler.Invalid("Paginação inválida.");
        var messages = await repository.ListarMensagensAsync(conversaId, antes, limite, ct);
        return messages.OrderBy(x => x.Sequencia).Select(AssistenteChatService.Dto).ToArray();
    }

    public async Task<IReadOnlyList<AssistentePropostaDto>> PropostasAsync(Guid conversaId, CancellationToken ct)
    {
        var conversa = await ConversaAsync(conversaId, false, ct);
        var peca = await pecas.ObterPecaAsync(conversa.PecaId, auth.OrganizationId, ct);
        var result = new List<AssistentePropostaDto>();
        foreach (var proposal in await repository.ListarPropostasAsync(conversaId, ct))
            result.Add(await DtoComFontesAsync(proposal, peca, ct));
        return result;
    }

    public async Task<AssistentePropostaDto> PropostaAsync(Guid id, CancellationToken ct)
    {
        var item = await PropostaPropriaAsync(id, ct);
        var conversa = await ConversaAsync(item.ConversaId, false, ct);
        var peca = await pecas.ObterPecaAsync(conversa.PecaId, auth.OrganizationId, ct);
        return await DtoComFontesAsync(item, peca, ct);
    }
    private async Task<AssistentePropostaDto> DtoComFontesAsync(AssistentePropostaPersistida proposal, Peca? piece, CancellationToken ct)
    {
        var result = Dto(proposal, piece?.Versao);
        if (documents is null || piece is null || result.Estado is not ("Pendente" or "ParcialmenteResolvida")) return result;
        var message = await repository.ObterPedidoMensagemAsync(proposal.ConversaId, proposal.ClientRequestId, ct);
        return await documents.ManifestValidAsync(message?.FontesDocumentais, proposal.ConversaId, piece.CasoId, ct)
            ? result : result with { Estado = "Desatualizada" };
    }

    // Bridge for an editable suggestion already presented by the existing chat.
    // Text supplied here is a private draft, not an assertion of provider provenance.
    public async Task<AssistentePropostaDto> RegistrarAsync(Guid conversaId, RegistrarPropostaRequest request, CancellationToken ct)
    {
        await ExigirEdicaoAsync(ct);
        if (request.ClientRequestId == Guid.Empty || request.VersaoPecaEsperada < 1 ||
            string.IsNullOrWhiteSpace(request.Instrucao) || request.Instrucao.Length > 20000 ||
            string.IsNullOrWhiteSpace(request.TextoResposta) || request.TextoResposta.Length > 120000)
            throw AssistentePatchCompiler.Invalid("Pedido de proposta inválido.");
        var hash = AssistentePatchCompiler.Hash(request);
        return await repository.EmTransacaoAsync(async token =>
        {
            var conversa = await ConversaAsync(conversaId, true, token);
            var existing = await repository.ObterPorPedidoAsync(conversaId, request.ClientRequestId, token);
            if (existing is not null)
            {
                if (existing.RequestSha256 != hash) throw Mismatch();
                return Dto(existing, null);
            }
            var peca = await PecaAsync(conversa.PecaId, token);
            if (peca.Versao != request.VersaoPecaEsperada) throw AssistentePatchCompiler.Stale();
            var snapshotId = Guid.NewGuid();
            var groups = compiler.Compilar(peca.ConteudoJson.RootElement, snapshotId, request.Grupos);
            var now = DateTime.UtcNow;
            var proposal = new AssistentePropostaPersistida
            {
                Id = Guid.NewGuid(), OrganizationId = auth.OrganizationId, ConversaId = conversa.Id,
                ClientRequestId = request.ClientRequestId, RequestSha256 = hash, SnapshotId = snapshotId,
                VersaoPecaAplicavel = peca.Versao, DocumentoBase = JsonDocument.Parse(peca.ConteudoJson.RootElement.GetRawText()),
                Grupos = AssistentePatchCompiler.Json(groups), CriadaEm = now
            };
            repository.Adicionar(proposal);
            if (request.MensagemUsuarioId is { } messageId)
            {
                var user = await repository.ObterMensagemAsync(conversaId, messageId, token);
                var response = await repository.ObterRespostaAsync(conversaId, messageId, token);
                if (user?.Autor != "Usuario" || response?.Estado != "Concluida" || user.Texto != request.Instrucao.Trim()) throw Missing();
                user.PropostaId = proposal.Id; response.PropostaId = proposal.Id;
                repository.Atualizar(user); repository.Atualizar(response);
            }
            else foreach (var pair in new[] { ("Usuario", request.Instrucao), ("Assistente", request.TextoResposta) })
                repository.Adicionar(new AssistenteMensagemPersistida
                {
                    Id = Guid.NewGuid(), OrganizationId = auth.OrganizationId, ConversaId = conversa.Id,
                    PropostaId = proposal.Id, Sequencia = conversa.ProximaSequencia++, Autor = pair.Item1,
                    Texto = pair.Item2.Trim(), CriadaEm = now
                });
            conversa.AtualizadaEm = now;
            return Dto(proposal, peca.Versao);
        }, ct);
    }

    public async Task<AssistenteReciboDto> AplicarAsync(Guid propostaId, AplicarGruposRequest request, CancellationToken ct)
    {
        await ExigirEdicaoAsync(ct);
        if (request.RequestId == Guid.Empty || request.Grupos is null || request.Grupos.Count is < 1 or > 100 ||
            request.Grupos.Any(g => g is null || g.Id == Guid.Empty || string.IsNullOrEmpty(g.PreviewSha256)) ||
            request.Grupos.Select(x => x.Id).Distinct().Count() != request.Grupos.Count)
            throw AssistentePatchCompiler.Invalid("Seleção de alterações inválida.");
        var own = await PropostaPropriaAsync(propostaId, ct);
        var hash = AssistentePatchCompiler.Hash(request);
        return await repository.EmTransacaoAsync(async token =>
        {
            var conversa = await ConversaAsync(own.ConversaId, true, token);
            var proposal = await PropostaPropriaAsync(propostaId, token);
            var peca = await PecaAsync(conversa.PecaId, token);
            var existing = await repository.ObterReciboAsync(conversa.Id, request.RequestId, token);
            if (existing is not null)
            {
                if (existing.PropostaId != propostaId || existing.RequestSha256 != hash) throw Mismatch();
                return Recibo(existing, proposal, peca.Versao);
            }
            ExigirRevisao(proposal, request.RevisaoProposta, request.VersaoDecisoes);
            if (documents is not null)
            {
                var sourceMessage = await repository.ObterPedidoMensagemAsync(conversa.Id, proposal.ClientRequestId, token);
                if (!await documents.ManifestValidAsync(sourceMessage?.FontesDocumentais, conversa.Id, peca.CasoId, token, lockSources: true))
                    throw new AssistenteIaException(409, "AI_DOCUMENT_SOURCE_CHANGED", "As fontes desta proposta mudaram. Peça uma nova revisão antes de aprovar.");
            }
            if (peca.Versao != request.VersaoPecaEsperada || peca.Versao != proposal.VersaoPecaAplicavel ||
                AssistentePatchCompiler.Hash(peca.ConteudoJson.RootElement) != AssistentePatchCompiler.Hash(proposal.DocumentoBase.RootElement))
                throw AssistentePatchCompiler.Stale();
            var groups = Grupos(proposal);
            var ids = request.Grupos.Select(x => x.Id).ToHashSet();
            var selected = groups.Where(x => ids.Contains(x.Id)).ToArray();
            if (selected.Length != ids.Count || selected.Any(g => g.Estado != "Pendente" ||
                request.Grupos.Single(x => x.Id == g.Id).PreviewSha256 != g.PreviewSha256))
                throw RevisionConflict();
            var result = compiler.Aplicar(proposal.DocumentoBase.RootElement, selected);
            var now = DateTime.UtcNow;
            peca.ConteudoJson = result;
            peca.UpdatedById = auth.UserId;
            peca.DataAtualizacao = now;
            if (!await pecas.AtualizarPecaAsync(peca, request.VersaoPecaEsperada, token)) throw AssistentePatchCompiler.Stale();
            proposal.Grupos = AssistentePatchCompiler.Json(compiler.Remapear(groups, ids));
            proposal.DocumentoBase = JsonDocument.Parse(result.RootElement.GetRawText());
            proposal.VersaoPecaAplicavel = request.VersaoPecaEsperada + 1;
            proposal.VersaoDecisoes++;
            proposal.SnapshotId = Guid.NewGuid();
            repository.Atualizar(proposal);
            var receipt = new AssistenteAplicacaoRecibo
            {
                Id = Guid.NewGuid(), OrganizationId = auth.OrganizationId, ConversaId = conversa.Id,
                PropostaId = propostaId, RequestId = request.RequestId, RequestSha256 = hash,
                VersaoAnterior = request.VersaoPecaEsperada, VersaoResultante = proposal.VersaoPecaAplicavel,
                ConteudoJson = JsonDocument.Parse(result.RootElement.GetRawText()),
                GruposAplicados = AssistentePatchCompiler.Json(ids.Order()), CriadaEm = now
            };
            repository.Adicionar(receipt);
            conversa.AtualizadaEm = now;
            return Recibo(receipt, proposal, proposal.VersaoPecaAplicavel);
        }, ct);
    }

    public async Task<AssistenteReciboDto> ReciboAsync(Guid conversaId, Guid requestId, CancellationToken ct)
    {
        var conversa = await ConversaAsync(conversaId, false, ct);
        var receipt = await repository.ObterReciboAsync(conversaId, requestId, ct) ?? throw Missing();
        var proposal = await PropostaPropriaAsync(receipt.PropostaId, ct);
        var peca = await PecaAsync(conversa.PecaId, ct);
        return Recibo(receipt, proposal, peca.Versao);
    }

    public async Task<AssistentePropostaDto> RejeitarAsync(Guid id, RejeitarGruposRequest request, CancellationToken ct)
    {
        await ExigirEdicaoAsync(ct);
        if (request.Grupos is null || request.Grupos.Count is < 1 or > 100 || request.Grupos.Distinct().Count() != request.Grupos.Count)
            throw AssistentePatchCompiler.Invalid("Grupos inválidos.");
        var own = await PropostaPropriaAsync(id, ct);
        return await repository.EmTransacaoAsync(async token =>
        {
            await ConversaAsync(own.ConversaId, true, token);
            var proposal = await PropostaPropriaAsync(id, token);
            var groups = Grupos(proposal);
            ExigirRevisao(proposal, request.RevisaoProposta, request.VersaoDecisoes);
            if (request.Grupos.Any(group => !groups.Any(x => x.Id == group && x.Estado == "Pendente"))) throw RevisionConflict();
            proposal.Grupos = AssistentePatchCompiler.Json(groups.Select(g => request.Grupos.Contains(g.Id) ? g with { Estado = "Rejeitado" } : g));
            proposal.VersaoDecisoes++;
            repository.Atualizar(proposal);
            return Dto(proposal, null);
        }, ct);
    }

    private static IReadOnlyList<AssistenteGrupoPersistido> Grupos(AssistentePropostaPersistida p) =>
        JsonSerializer.Deserialize<AssistenteGrupoPersistido[]>(p.Grupos.RootElement, AssistentePatchCompiler.JsonOptions)!;
    public static AssistentePropostaDto Dto(AssistentePropostaPersistida p, int? currentVersion)
    {
        var groups = Grupos(p);
        var pending = groups.Count(x => x.Estado == "Pendente");
        var state = p.SubstituidaPorId.HasValue ? "Substituida" : pending == 0 ? "Resolvida" : currentVersion.HasValue && currentVersion != p.VersaoPecaAplicavel ? "Desatualizada" :
            pending == groups.Count ? "Pendente" : "ParcialmenteResolvida";
        return new("assistente-v2", p.Id, p.ConversaId, p.SnapshotId, p.Revisao, p.VersaoDecisoes,
            p.VersaoPecaAplicavel, state, p.DocumentoBase.RootElement.Clone(), groups, p.SubstituidaPorId);
    }
    private static AssistenteReciboDto Recibo(AssistenteAplicacaoRecibo r, AssistentePropostaPersistida p, int currentVersion) =>
        new("assistente-v2", r.RequestId, r.PropostaId, r.VersaoAnterior, r.VersaoResultante, currentVersion,
            currentVersion == r.VersaoResultante, JsonSerializer.Deserialize<Guid[]>(r.GruposAplicados.RootElement)!,
            r.ConteudoJson.RootElement.Clone(), AssistentePatchCompiler.Hash(r.ConteudoJson.RootElement), r.CriadaEm, Dto(p, currentVersion));
    private async Task<Peca> PecaAsync(Guid id, CancellationToken ct) => await pecas.ObterPecaAsync(id, auth.OrganizationId, ct) ?? throw Missing();
    private async Task<AssistenteConversa> ConversaAsync(Guid id, bool bloquear, CancellationToken ct) =>
        await repository.ObterConversaAsync(id, auth.OrganizationId, auth.UserId, bloquear, ct) ?? throw Missing();
    private async Task<AssistentePropostaPersistida> PropostaPropriaAsync(Guid id, CancellationToken ct) =>
        await repository.ObterPropostaAsync(id, auth.OrganizationId, auth.UserId, ct) ?? throw Missing();
    private void ExigirRollout()
    {
        if (!settings.CurrentValue.ReviewV2Enabled) throw new AssistenteIaException(503, "AI_REVIEW_V2_DISABLED", "A revisão persistida ainda não está habilitada.");
    }
    private async Task ExigirEdicaoAsync(CancellationToken ct)
    { ExigirRollout(); await features.ExigirModoAsync(auth.OrganizationId, AssistenteModo.Redigir, ct); await transparency.ExigirConfirmacaoAsync(ct); }
    private static void ExigirRevisao(AssistentePropostaPersistida p, int revision, int decisions)
    { if (p.SubstituidaPorId.HasValue || p.Revisao != revision || p.VersaoDecisoes != decisions) throw RevisionConflict(); }
    private static AssistenteIaException Missing() => new(404, "AI_RESOURCE_NOT_FOUND", "Recurso não encontrado.");
    private static AssistenteIaException Mismatch() => new(409, "AI_IDEMPOTENCY_MISMATCH", "O identificador já foi utilizado com outro conteúdo.");
    private static AssistenteIaException RevisionConflict() => new(409, "AI_PROPOSAL_REVISION_CONFLICT", "Atualize a revisão antes de decidir estas alterações.");
}
