using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Domain.Entities;
using static JuridicoAI.Api.Application.Services.AssistenteChatService;

namespace JuridicoAI.Api.Application.Services;

public sealed record AssistenteHistoryEntry(Guid Id, long Sequencia, string Autor, JsonElement Dados, string Sha256);

internal static class AssistenteHistoryProjection
{
    public static async Task<IReadOnlyList<AssistenteHistoryEntry>> ProjectAsync(IAssistenteWorkspaceRepository repository,
        AssistenteDocumentService? documents, IAuthContext auth, AssistenteConversa conversa, Peca peca,
        IReadOnlyList<AssistenteMensagemPersistida> messages, CancellationToken ct, bool lockSources = false)
    {
        var byId = messages.ToDictionary(m => m.Id);
        var history = new List<AssistenteHistoryEntry>();
        foreach (var message in messages)
        {
            if (message.Autor == "Usuario")
            {
                var request = Read<CriarSolicitacaoAssistenteRequest>(message.Solicitacao);
                history.Add(Entry(message, new { id = message.Id, sequencia = message.Sequencia, autor = "Usuario", texto = message.Texto,
                    informacaoAdicionalDoUsuario = request?.Contexto?.ContextoAdicional }));
                continue;
            }
            if (message.Estado != "Concluida") continue;
            // Historical shared snapshots are readable in the private chat, but
            // are not reused as fresh sources after version/access changes.
            if (message.RespostaAId is { } parentId && !byId.ContainsKey(parentId))
            {
                var parent = await repository.ObterMensagemAsync(conversa.Id, parentId, ct);
                if (parent is not null) byId[parent.Id] = parent;
            }
            var source = message.RespostaAId is { } id && byId.TryGetValue(id, out var user) ? Read<CriarSolicitacaoAssistenteRequest>(user.Solicitacao) : null;
            var sourceDependent = source is null || source.VersaoPeca != peca.Versao ||
                source.Contexto?.IncluirDadosCaso == true || source.Contexto?.ContextoCasoIds?.Count > 0;
            if (documents is not null && message.RespostaAId is { } sourceId && byId.TryGetValue(sourceId, out var sourceMessage))
                sourceDependent |= !await documents.ManifestValidAsync(sourceMessage.FontesDocumentais, conversa.Id, peca.CasoId, ct, lockSources);
            var result = Read<AssistenteSolicitacaoConcluidaDto>(message.Resultado);
            IReadOnlyList<AssistenteGrupoPersistido>? decisions = null;
            string? proposalState = null;
            Guid? supersededBy = null;
            if (message.PropostaId is { } proposalId)
            {
                var proposal = await repository.ObterPropostaAsync(proposalId, auth.OrganizationId, auth.UserId, ct);
                if (proposal is not null)
                {
                    decisions = JsonSerializer.Deserialize<AssistenteGrupoPersistido[]>(proposal.Grupos, AssistentePatchCompiler.JsonOptions);
                    proposalState = AssistenteWorkspaceService.Dto(proposal, peca.Versao).Estado;
                    supersededBy = proposal.SubstituidaPorId;
                }
            }
            history.Add(Entry(message, new { id = message.Id, sequencia = message.Sequencia, autor = "Assistente", estado = "RascunhoGeradoNaoEvidenciaFactual",
                texto = sourceDependent ? null : result?.Sugestao.Texto ?? message.Texto,
                achados = sourceDependent ? null : result?.Sugestao.Achados,
                alertas = sourceDependent ? null : result?.Sugestao.Alertas,
                conteudoOmitidoPorDependenciaDeFonte = sourceDependent,
                estadoProposta = proposalState, substituidaPorId = supersededBy,
                decisoes = decisions?.Select(d => new { d.Id, d.Estado }) }));
        }
        return history;
    }
    private static AssistenteHistoryEntry Entry(AssistenteMensagemPersistida m, object payload)
    {
        var data = JsonSerializer.SerializeToElement(payload, AssistentePatchCompiler.JsonOptions);
        return new(m.Id, m.Sequencia, m.Autor, data, AssistentePatchCompiler.Hash(data));
    }
}
