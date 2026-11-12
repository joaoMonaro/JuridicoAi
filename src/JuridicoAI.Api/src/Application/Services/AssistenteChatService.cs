using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Application.Services;

public sealed record AssistenteChatInicio(Guid PecaId, AssistenteMensagemPersistida Pedido,
    AssistenteMensagemPersistida Resposta, string Contexto, bool Novo,
    JsonElement? DocumentoBase = null, AssistentePropostaPersistida? PropostaReferenciada = null,
    IReadOnlyList<AssistenteAlertaDto>? AlertasDocumentais = null,
    IReadOnlyList<AssistenteFonteRecuperacao>? FontesRecuperacao = null, Guid? CasoId = null, AssistenteHistoryContext? Historico = null, AssistenteAnaliseDocumental? Analise = null, AssistenteGeracao? Geracao = null);

// Durable history around the existing request-bound generation. A disconnected
// generation is cancelled, not silently restarted or billed a second time.
public sealed class AssistenteChatService(IAssistenteWorkspaceRepository repository, IPecaRepository pecas,
    IAuthContext auth, IAssistenteFeatureAccess features, IAssistenteTransparencyService transparency,
    IOptionsMonitor<AssistenteIaSettings> options, AssistenteDocumentService? documents = null, AssistenteHistoryService? history = null, AssistenteDocumentAnalysisService? analysis = null, AssistenteGenerationService? generations = null)
{
    public async Task<AssistenteChatInicio> IniciarAsync(Guid conversaId, CriarSolicitacaoAssistenteRequest request, CancellationToken ct)
    {
        if (!options.CurrentValue.ConversationV2Enabled || !options.CurrentValue.ReviewV2Enabled)
            throw new AssistenteIaException(503, "AI_CONVERSATION_DISABLED", "A conversa persistida não está habilitada.");
        if (request.InteracaoAutomatica && !options.CurrentValue.AutomaticConversationEnabled)
            throw new AssistenteIaException(503, "AI_AUTOMATIC_CONVERSATION_DISABLED", "A conversa livre não está habilitada.");
        await features.ExigirModoAsync(auth.OrganizationId, request.Modo, ct);
        await transparency.ExigirConfirmacaoAsync(ct);
        // Queue work before reserving a message. A "processing" response must
        // not roll back the very jobs that the next attempt is waiting for.
        if (documents?.Enabled == true) await documents.SynchronizeAsync(conversaId, ct);
        return await repository.EmTransacaoAsync(async token =>
        {
            var conversa = await ConversaAsync(conversaId, token);
            var peca = await pecas.ObterPecaAsync(conversa.PecaId, auth.OrganizationId, token) ?? throw Missing();
            var hash = AssistentePatchCompiler.Hash(request);
            var existing = await repository.ObterPedidoMensagemAsync(conversaId, request.ClientRequestId, token);
            if (existing is not null)
            {
                if (existing.RequestSha256 != hash) throw new AssistenteIaException(409, "AI_IDEMPOTENCY_MISMATCH", "O identificador já foi utilizado com outro conteúdo.");
                var answer = await repository.ObterRespostaAsync(conversaId, existing.Id, token) ?? throw Missing();
                if (answer.Estado == "Gerando" && answer.ExpiraEm <= DateTime.UtcNow)
                { answer.Estado = "Interrompida"; answer.CodigoErro = "AI_REQUEST_INTERRUPTED"; repository.Atualizar(answer); }
                return new AssistenteChatInicio(peca.Id, existing, answer, string.Empty, false);
            }
            if (peca.Versao != request.VersaoPeca) throw AssistentePatchCompiler.Stale();
            var active = await repository.ObterMensagemAtivaAsync(conversaId, token);
            if (active is not null)
            {
                if (active.ExpiraEm > DateTime.UtcNow) throw new AssistenteIaException(409, "AI_CONVERSATION_BUSY", "Aguarde a conclusão do pedido atual.");
                active.Estado = "Interrompida"; active.CodigoErro = "AI_REQUEST_INTERRUPTED";
                repository.Atualizar(active);
            }
            if (generations?.Enabled == true)
            {
                var nowQueued = DateTime.UtcNow;
                var queuedUser = new AssistenteMensagemPersistida { Id = Guid.NewGuid(), OrganizationId = auth.OrganizationId, ConversaId = conversaId,
                    Sequencia = conversa.ProximaSequencia++, Autor = "Usuario", Texto = request.Instrucao!.Trim(), ClientRequestId = request.ClientRequestId,
                    RequestSha256 = hash, Solicitacao = AssistentePatchCompiler.Json(request), CriadaEm = nowQueued };
                var queuedResponse = new AssistenteMensagemPersistida { Id = Guid.NewGuid(), OrganizationId = auth.OrganizationId, ConversaId = conversaId,
                    Sequencia = conversa.ProximaSequencia++, Autor = "Assistente", RespostaAId = queuedUser.Id, CriadaEm = nowQueued };
                repository.Adicionar(queuedUser); repository.Adicionar(queuedResponse);
                await generations.AddAsync(conversa, peca, queuedUser, queuedResponse, request, token);
                conversa.AtualizadaEm = nowQueued;
                return new AssistenteChatInicio(peca.Id, queuedUser, queuedResponse, "", true);
            }
            AssistentePropostaPersistida? reference = null;
            if (request.PropostaReferenciadaId is { } refId)
            {
                reference = await repository.ObterPropostaAsync(refId, auth.OrganizationId, auth.UserId, token);
                if (reference is null || reference.ConversaId != conversaId) throw Missing();
            }
            var documental = documents is null ? null : await documents.ContextAsync(conversa, peca, request.AnexoIds, token, request.InteracaoAutomatica);
            var ready = request.InteracaoAutomatica && analysis is not null ? await analysis.ReadyAsync(conversa.Id,
                documental is null ? null : JsonDocument.Parse(documental.Manifesto), token) : null;
            if (ready is not null) documental = documental! with { Texto = documental.Texto + analysis!.Context(ready) };
            var (context, historic) = await ContextoAsync(conversa, peca, reference, request.InteracaoAutomatica, documental?.Texto.Length ?? 0, token);
            context += documental?.Texto;
            if (context.Length > options.CurrentValue.MaxTotalContextCharacters) throw TooLarge();
            var now = DateTime.UtcNow;
            var user = new AssistenteMensagemPersistida
            {
                Id = Guid.NewGuid(), ConversaId = conversaId, OrganizationId = auth.OrganizationId,
                Sequencia = conversa.ProximaSequencia++, Autor = "Usuario", Texto = request.Instrucao!.Trim(),
                ClientRequestId = request.ClientRequestId, RequestSha256 = hash,
                Solicitacao = AssistentePatchCompiler.Json(request), CriadaEm = now,
                FontesDocumentais = documental is null ? null : JsonDocument.Parse(documental.Manifesto)
            };
            var response = new AssistenteMensagemPersistida
            {
                Id = Guid.NewGuid(), ConversaId = conversaId, OrganizationId = auth.OrganizationId,
                Sequencia = conversa.ProximaSequencia++, Autor = "Assistente", RespostaAId = user.Id,
                ContextoHistorico = historic is null ? null : AssistentePatchCompiler.Json(historic.Estado),
                Estado = "Gerando", ExpiraEm = now.AddSeconds(options.CurrentValue.TimeoutSeconds + 30), CriadaEm = now
            };
            repository.Adicionar(user); repository.Adicionar(response);
            conversa.AtualizadaEm = now;
            return new AssistenteChatInicio(peca.Id, user, response, context, true, peca.ConteudoJson.RootElement.Clone(), reference, documental?.Alertas,
                request.InteracaoAutomatica ? documental?.FontesRecuperacao : null, peca.CasoId, historic, ready);
        }, ct);
    }

    public async Task<AssistenteChatInicio> PrepararGeracaoAsync(AssistenteGeracao job, CancellationToken ct)
    {
        if (generations is null || documents is null) throw Missing();
        return await repository.EmTransacaoAsync(async token =>
        {
            var conversation = await ConversaAsync(job.ConversaId, token);
            await generations.ValidateAsync(job, token, lockSources: true);
            var user = await repository.ObterMensagemAsync(job.ConversaId, job.PedidoId, token) ?? throw Missing();
            var response = await repository.ObterMensagemAsync(job.ConversaId, job.RespostaId, token) ?? throw Missing();
            if (response.GeracaoId != job.Id || response.Estado is not ("AguardandoGeracao" or "Gerando")) throw Missing();
            var piece = await pecas.ObterPecaAsync(job.PecaId, auth.OrganizationId, token) ?? throw Missing();
            var request = Read<CriarSolicitacaoAssistenteRequest>(user.Solicitacao)!;
            var reference = request.PropostaReferenciadaId is { } id ? await repository.ObterPropostaAsync(id, auth.OrganizationId, auth.UserId, token) : null;
            if (request.PropostaReferenciadaId.HasValue && reference is null) throw Missing();
            response.Estado = "Gerando"; repository.Atualizar(response);
            if (job.Contexto is not null)
            {
                var saved = AssistenteGenerationService.Read<AssistenteGenerationContext>(job.Contexto);
                if (saved.Historico is not null && response.ContextoHistorico is not null && Read<AssistenteContextoHistoricoDto>(response.ContextoHistorico) is { } metadata && metadata.VersaoMemoria > saved.Historico.VersaoAnterior)
                { saved.Historico.Salva = true; saved.Historico.Estado = metadata; }
                var oldAnalysis = saved.AnaliseId.HasValue && analysis is not null ? await analysis.ReadyAsync(job.ConversaId, user.FontesDocumentais, token) : null;
                return new AssistenteChatInicio(piece.Id, user, response, saved.Texto + saved.Evidencias, true, saved.Documento, reference, saved.Alertas,
                    saved.Fontes, piece.CasoId, saved.Historico, oldAnalysis, job);
            }
            var sources = AssistenteGenerationService.Read<AssistenteSourceSnapshot[]>(job.FontesIds).Select(s => s.Id).ToArray();
            var documental = await documents.ContextAsync(conversation, piece, request.AnexoIds, token, request.InteracaoAutomatica, sources);
            user.FontesDocumentais = JsonDocument.Parse(documental.Manifesto); repository.Atualizar(user);
            var ready = analysis is null ? null : await analysis.ReadyAsync(job.ConversaId, user.FontesDocumentais, token);
            if (ready is not null) documental = documental with { Texto = documental.Texto + analysis!.Context(ready) };
            var (text, historic) = await ContextoAsync(conversation, piece, reference, request.InteracaoAutomatica, documental.Texto.Length, token,
                compactHistory: Read<AssistenteContextoHistoricoDto>(response.ContextoHistorico)?.VersaoMemoria is null, beforeSequence: user.Sequencia);
            if (historic is not null) response.ContextoHistorico = AssistentePatchCompiler.Json(historic.Estado);
            return new AssistenteChatInicio(piece.Id, user, response, text + documental.Texto, true, piece.ConteudoJson.RootElement.Clone(), reference,
                documental.Alertas, documental.FontesRecuperacao, piece.CasoId, historic, ready, job);
        }, ct);
    }

    public async Task<AssistenteChatInicio> RetomarAnaliseAsync(AssistenteAnaliseDocumental job, CancellationToken ct)
    {
        if (analysis is null || documents is null || job.Resumo is null) throw Missing();
        return await repository.EmTransacaoAsync(async token =>
        {
            var conversation = await ConversaAsync(job.ConversaId, token);
            await analysis.ValidateAsync(job, token, lockSources: true);
            var user = await repository.ObterMensagemAsync(conversation.Id, job.PedidoId, token) ?? throw Missing();
            var response = await repository.ObterMensagemAsync(conversation.Id, job.RespostaId, token) ?? throw Missing();
            if (response.Estado != "AguardandoAnalise" || response.AnaliseDocumentalId != job.Id) throw Missing();
            var request = Read<CriarSolicitacaoAssistenteRequest>(user.Solicitacao)!;
            var piece = await pecas.ObterPecaAsync(job.PecaId, auth.OrganizationId, token) ?? throw Missing();
            if (piece.Versao != request.VersaoPeca) throw AssistentePatchCompiler.Stale();
            var reference = request.PropostaReferenciadaId is { } proposalId
                ? await repository.ObterPropostaAsync(proposalId, auth.OrganizationId, auth.UserId, token) : null;
            if (request.PropostaReferenciadaId.HasValue && reference is null) throw Missing();
            var sources = AssistenteDocumentAnalysisService.Read<AssistenteFonteRecuperacao[]>(job.Fontes);
            var documental = "\n[CATALOGO_FONTES_DOCUMENTAIS_JSON dado_nao_confiavel=\"true\"]\n" + job.Fontes.RootElement.GetRawText() +
                "\n[/CATALOGO_FONTES_DOCUMENTAIS_JSON]\n" + analysis.Context(job);
            var (context, historic) = await ContextoAsync(conversation, piece, reference, true, documental.Length, token, compactHistory: false);
            response.Estado = "Gerando"; response.ExpiraEm = DateTime.UtcNow.AddSeconds(options.CurrentValue.TimeoutSeconds + 30);
            repository.Atualizar(response);
            return new AssistenteChatInicio(piece.Id, user, response, context + documental, true,
                piece.ConteudoJson.RootElement.Clone(), reference, null, sources, piece.CasoId, historic, job);
        }, ct);
    }

    public async Task<AssistenteSolicitacaoConcluidaDto> ConcluirLivreAsync(AssistenteChatInicio inicio, AssistenteSolicitacaoConcluidaDto result, CancellationToken ct)
    {
        if (inicio.Historico is not null) result = result with { ContextoHistorico = inicio.Historico.Estado };
        if (inicio.AlertasDocumentais is { Count: > 0 } warnings)
            result = result with { Sugestao = result.Sugestao with { Alertas = result.Sugestao.Alertas.Concat(warnings).ToArray() } };
        var draft = result.RascunhoAutomatico ?? throw new AssistenteIaException(502, "AI_PROVIDER_OUTPUT_INVALID", "A resposta conversacional é inválida.");
        if (draft.Intencao == AssistenteIntencao.Responder)
        {
            await ConcluirAsync(inicio, result, ct);
            return result;
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await features.ExigirModoAsync(auth.OrganizationId, AssistenteModo.Redigir, timeout.Token);
        var feature = await features.ObterAsync(auth.OrganizationId, timeout.Token);
        var full = draft.Intencao == AssistenteIntencao.GerarMinuta || AssistenteAutomaticProposalPolicy.CobreDocumento(inicio.DocumentoBase!.Value, draft.Grupos);
        if (full && !feature.Features.Contains(OrganizationFeatureEnum.AI_DOCUMENT_GENERATION))
            throw new AssistenteIaException(403, "AI_FEATURE_NOT_ENABLED", "A geração integral não está habilitada para esta organização.");
        if (!options.CurrentValue.AutomaticConversationEnabled)
            throw new AssistenteIaException(503, "AI_AUTOMATIC_CONVERSATION_DISABLED", "A conversa livre foi desabilitada.");
        return await repository.EmTransacaoAsync(async token =>
        {
            var conversation = await ConversaAsync(inicio.Pedido.ConversaId, token);
            await ValidateDocumentSourcesAsync(inicio, token);
            var user = await repository.ObterMensagemAsync(conversation.Id, inicio.Pedido.Id, token) ?? throw Missing();
            var response = await repository.ObterRespostaAsync(conversation.Id, user.Id, token) ?? throw Missing();
            if (response.Estado != "Gerando" || response.ExpiraEm <= DateTime.UtcNow)
                throw new AssistenteIaException(409, "AI_REQUEST_INTERRUPTED", "O pedido foi interrompido.");
            var piece = await pecas.ObterPecaAsync(inicio.PecaId, auth.OrganizationId, token) ?? throw Missing();
            var request = Read<CriarSolicitacaoAssistenteRequest>(user.Solicitacao)!;
            var snapshotId = Guid.NewGuid();
            var groups = new AssistentePatchCompiler().Compilar(inicio.DocumentoBase!.Value, snapshotId, draft.Grupos);
            var proposal = new AssistentePropostaPersistida
            {
                Id = Guid.NewGuid(), OrganizationId = auth.OrganizationId, ConversaId = conversation.Id,
                ClientRequestId = request.ClientRequestId, RequestSha256 = user.RequestSha256!, SnapshotId = snapshotId,
                VersaoPecaAplicavel = request.VersaoPeca, DocumentoBase = JsonDocument.Parse(inicio.DocumentoBase.Value.GetRawText()),
                Grupos = AssistentePatchCompiler.Json(groups), CriadaEm = DateTime.UtcNow
            };
            if (draft.SubstituirPropostaReferenciada)
            {
                var old = inicio.PropostaReferenciada is null ? null : await repository.ObterPropostaAsync(inicio.PropostaReferenciada.Id, auth.OrganizationId, auth.UserId, token);
                if (old is null || old.SubstituidaPorId.HasValue || old.VersaoDecisoes != inicio.PropostaReferenciada!.VersaoDecisoes ||
                    !JsonSerializer.Deserialize<AssistenteGrupoPersistido[]>(old.Grupos, AssistentePatchCompiler.JsonOptions)!.Any(g => g.Estado == "Pendente"))
                    throw new AssistenteIaException(409, "AI_PROPOSAL_REVISION_CONFLICT", "A proposta referenciada mudou. Faça um novo pedido sobre o estado atual.");
                if (piece.Versao == request.VersaoPeca)
                { old.SubstituidaPorId = proposal.Id; repository.Atualizar(old); }
            }
            repository.Adicionar(proposal);
            user.PropostaId = proposal.Id; response.PropostaId = proposal.Id;
            result = result with { Proposta = AssistenteWorkspaceService.Dto(proposal, piece.Versao) };
            response.Estado = "Concluida"; response.Texto = result.Sugestao.Texto;
            if (result.CoberturaDocumental is not null) response.CoberturaDocumental = AssistentePatchCompiler.Json(result.CoberturaDocumental);
            response.Resultado = AssistentePatchCompiler.Json(result);
            if (analysis is not null) await analysis.CompleteAsync(inicio.Analise, response.Id, token);
            if (generations is not null) await generations.CompleteAsync(inicio.Geracao, token);
            repository.Atualizar(user); repository.Atualizar(response);
            conversation.AtualizadaEm = DateTime.UtcNow;
            return result;
        }, timeout.Token);
    }

    public async Task ConcluirAsync(AssistenteChatInicio inicio, AssistenteSolicitacaoConcluidaDto result, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await repository.EmTransacaoAsync(async token =>
        {
            var conversa = await ConversaAsync(inicio.Pedido.ConversaId, token);
            await ValidateDocumentSourcesAsync(inicio, token);
            var response = await repository.ObterRespostaAsync(conversa.Id, inicio.Pedido.Id, token) ?? throw Missing();
            if (response.Estado != "Gerando" || response.ExpiraEm <= DateTime.UtcNow)
                throw new AssistenteIaException(409, "AI_REQUEST_INTERRUPTED", "O pedido foi interrompido. O resultado tardio não foi publicado.");
            response.Estado = "Concluida";
            response.Texto = result.Sugestao.Texto;
            if (result.CoberturaDocumental is not null) response.CoberturaDocumental = AssistentePatchCompiler.Json(result.CoberturaDocumental);
            response.Resultado = AssistentePatchCompiler.Json(result);
            if (analysis is not null) await analysis.CompleteAsync(inicio.Analise, response.Id, token);
            if (generations is not null) await generations.CompleteAsync(inicio.Geracao, token);
            repository.Atualizar(response);
            conversa.AtualizadaEm = DateTime.UtcNow;
            return true;
        }, timeout.Token);
    }

    public async Task FalharAsync(AssistenteChatInicio inicio, string codigo, bool cancelado)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await repository.EmTransacaoAsync(async token =>
        {
            await ConversaAsync(inicio.Pedido.ConversaId, token);
            var response = await repository.ObterRespostaAsync(inicio.Pedido.ConversaId, inicio.Pedido.Id, token) ?? throw Missing();
            if (response.Estado == "Gerando")
            {
                response.Estado = cancelado ? "Cancelada" : "Falhou";
                response.CodigoErro = codigo;
                repository.Atualizar(response);
            }
            return true;
        }, timeout.Token);
    }

    public async Task RejeitarRespostaAsync(Guid conversaId, Guid mensagemId, CancellationToken ct)
    {
        await features.ExigirModoAsync(auth.OrganizationId, AssistenteModo.Perguntar, ct);
        await repository.EmTransacaoAsync(async token =>
        {
            await ConversaAsync(conversaId, token);
            var user = await repository.ObterMensagemAsync(conversaId, mensagemId, token);
            var response = await repository.ObterRespostaAsync(conversaId, mensagemId, token);
            if (user?.Autor != "Usuario" || response is null) throw Missing();
            if (response.PropostaId is not null || response.Estado is not ("Concluida" or "Rejeitada"))
                throw new AssistenteIaException(409, "AI_RESPONSE_DECISION_CONFLICT", "Use a revisão da proposta para decidir alterações já registradas.");
            response.Estado = "Rejeitada";
            repository.Atualizar(response);
            return true;
        }, ct);
    }

    private async Task<(string, AssistenteHistoryContext?)> ContextoAsync(AssistenteConversa conversa, Peca peca, AssistentePropostaPersistida? reference, bool automatic, int reservedCharacters, CancellationToken ct, bool compactHistory = true, long? beforeSequence = null)
    {
        var referenceUnavailable = false;
        if (reference is not null && documents is not null)
        {
            var referenceMessage = await repository.ObterPedidoMensagemAsync(conversa.Id, reference.ClientRequestId, ct);
            if (!await documents.ManifestValidAsync(referenceMessage?.FontesDocumentais, conversa.Id, peca.CasoId, ct))
            { referenceUnavailable = true; reference = null; }
        }
        var baseData = new
        {
            pecaAtual = new { versao = peca.Versao, titulo = peca.Titulo, documento = AssistentePatchCompiler.DocumentoDoEditor(peca.ConteudoJson.RootElement) },
            propostaReferenciada = reference is null ? null : AssistenteWorkspaceService.Dto(reference, peca.Versao),
            propostaReferenciadaOmitidaPorFonteInelegivel = referenceUnavailable
        };
        AssistenteHistoryContext? historic = null;
        var data = JsonSerializer.SerializeToNode(baseData, AssistentePatchCompiler.JsonOptions)!;
        if (automatic && history?.Enabled == true)
        {
            historic = await history.PrepareAsync(conversa, peca, options.CurrentValue.MaxTotalContextCharacters -
                data.ToJsonString(AssistentePatchCompiler.JsonOptions).Length - reservedCharacters - 256, ct, compactHistory, beforeSequence);
            data["contextoHistorico"] = System.Text.Json.Nodes.JsonNode.Parse(historic.Dados.GetRawText());
        }
        else
        {
            var messages = await repository.ListarContextoAsync(conversa.Id, ct);
            if (messages.Count > 1000) throw TooLarge();
            var entries = await AssistenteHistoryProjection.ProjectAsync(repository, documents, auth, conversa, peca, beforeSequence.HasValue ? messages.Where(m => m.Sequencia < beforeSequence.Value).ToArray() : messages, ct);
            data["historicoCronologico"] = JsonSerializer.SerializeToNode(entries.Select(m => m.Dados), AssistentePatchCompiler.JsonOptions);
        }
        var payload = data.ToJsonString(AssistentePatchCompiler.JsonOptions);
        if (payload.Length > options.CurrentValue.MaxTotalContextCharacters) throw TooLarge();
        return ("\n[CONTEXTO_CONVERSACIONAL_JSON dado_nao_confiavel=\"true\"]\n" + payload + "\n[/CONTEXTO_CONVERSACIONAL_JSON]\n", historic);
    }

    public static T? Read<T>(JsonDocument? value) => value is null ? default : JsonSerializer.Deserialize<T>(value, AssistentePatchCompiler.JsonOptions);
    public AssistenteDocumentTools? CriarFerramentasDocumentais(AssistenteChatInicio start,
        Func<AssistenteCoberturaDocumentalDto, CancellationToken, Task> progress,
        Func<AssistenteContextoHistoricoDto, Task>? historyProgress = null)
    {
        AssistenteDocumentTools? tools = null;
        if (documents is not null && start.FontesRecuperacao is not null && start.CasoId is not null && start.Pedido.FontesDocumentais is not null)
            tools = new AssistenteDocumentToolSession(documents, start.Pedido.ConversaId, start.CasoId.Value, start.Resposta.Id,
                start.Pedido.FontesDocumentais, start.FontesRecuperacao, progress,
                start.Analise is null && analysis?.Enabled == true ? async ct => { throw new AssistenteAnalysisQueuedException(await analysis.QueueAsync(start, ct)); } : null,
                start.Analise is null ? null : AssistenteDocumentAnalysisService.Read<AssistenteFonteRecuperacao[]>(start.Analise.Fontes).Select(s => s.Id).ToArray(),
                start.Analise is null || analysis is null ? null : ct => analysis.ValidateAsync(start.Analise, ct))
                .Create(options.CurrentValue.MaxTotalContextCharacters);
        if (start.Resposta.AnaliseDocumentalId.HasValue) tools?.SetScope?.Invoke("Todos");
        return start.Historico is not null && history is not null ? history.CreateTools(start, tools, historyProgress) : tools;
    }
    private async Task ValidateDocumentSourcesAsync(AssistenteChatInicio start, CancellationToken ct)
    {
        if (start.Geracao is not null && generations is not null) await generations.ValidateAsync(start.Geracao, ct, lockSources: true);
        if (start.Analise is not null && analysis is not null) await analysis.ValidateAsync(start.Analise, ct, lockSources: true);
        if (start.Historico is not null && history is not null) await history.ValidateAsync(start.Historico, ct, lockSources: true);
        if (documents is null || start.Pedido.FontesDocumentais is null) return;
        var piece = await pecas.ObterPecaAsync(start.PecaId, auth.OrganizationId, ct) ?? throw Missing();
        if (!await documents.ManifestValidAsync(start.Pedido.FontesDocumentais, start.Pedido.ConversaId, piece.CasoId, ct, lockSources: true))
            throw new AssistenteIaException(409, "AI_DOCUMENT_SOURCE_CHANGED", "Uma fonte foi alterada, removida ou reprocessada. O resultado não foi publicado; faça um novo pedido com as fontes atuais.");
    }
    public static AssistenteMensagemDto Dto(AssistenteMensagemPersistida m) => new(m.Id, m.Sequencia, m.Autor, m.Texto, m.PropostaId, m.CriadaEm,
        m.ClientRequestId, m.RespostaAId, m.Estado == "Gerando" && m.ExpiraEm <= DateTime.UtcNow ? "Interrompida" : m.Estado,
        m.CodigoErro, Read<CriarSolicitacaoAssistenteRequest>(m.Solicitacao), Read<AssistenteSolicitacaoConcluidaDto>(m.Resultado),
        Read<AssistenteCoberturaDocumentalDto>(m.CoberturaDocumental), Read<AssistenteContextoHistoricoDto>(m.ContextoHistorico), m.AnaliseDocumentalId, m.GeracaoId);
    private async Task<AssistenteConversa> ConversaAsync(Guid id, CancellationToken ct) =>
        await repository.ObterConversaAsync(id, auth.OrganizationId, auth.UserId, true, ct) ?? throw Missing();
    private static AssistenteIaException Missing() => new(404, "AI_RESOURCE_NOT_FOUND", "Recurso não encontrado.");
    private static AssistenteIaException TooLarge() => new(413, "AI_HISTORY_CONTEXT_TOO_LARGE", "O histórico permanece salvo, mas excede o limite de contexto desta etapa. O pedido não foi enviado à IA.");
}
