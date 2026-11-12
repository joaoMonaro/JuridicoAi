using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using System.Text.Json;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Application.Services;

public sealed record AssistenteIaExecucao(
    Guid SolicitacaoId,
    Guid ClientRequestId,
    Guid OrganizationId,
    Guid UserId,
    Guid PecaId,
    Guid CorrelationId,
    AssistenteModo Modo,
    AssistenteAncoraDto Ancora,
    AssistenteIaProviderRequest ProviderRequest,
    int? InputTokensContados,
    long StartedAtTimestamp,
    IAsyncDisposable UsageLease,
    string PromptVersion = AssistentePromptPolicy.Version,
    JsonElement? DocumentoBase = null);

public sealed class AssistenteIaApplicationService(
    IPecaRepository pecaRepository,
    ICasoRepository casoRepository,
    IAuthContext authContext,
    IAssistenteIaProvider provider,
    IAssistenteTokenCounter tokenCounter,
    AssistenteContextBuilder contextBuilder,
    AssistentePromptPolicy promptPolicy,
    AssistenteRequestRegistry requestRegistry,
    IAssistenteFeatureAccess featureAccess,
    IAssistenteTransparencyService transparencyService,
    IAssistenteUsageLimiter usageLimiter,
    IAssistenteUsageRecorder usageRecorder,
    ICorrelationContext correlationContext,
    IOptionsMonitor<AssistenteIaSettings> options,
    ILogger<AssistenteIaApplicationService> logger,
    IOptions<AssistenteDocumentSettings>? documentOptions = null)
{
    private AssistenteIaSettings Settings => options.CurrentValue;

    public async Task<AssistenteCapacidadesDto> ObterCapacidadesAsync(CancellationToken cancellationToken)
    {
        var snapshot = await featureAccess.ObterAsync(authContext.OrganizationId, cancellationToken);
        var settings = Settings;
        return new(
            snapshot.Habilitado,
            snapshot.Modos,
            new AssistenteLimitesDto(
                settings.MaxInstructionCharacters,
                settings.MaxDocumentContextCharacters,
                settings.MaxAdditionalContextCharacters),
            SuportaStreaming: true,
            SuportaHistorico: settings.ConversationV2Enabled && settings.ReviewV2Enabled && snapshot.Habilitado,
            snapshot.Features.Select(x => x.ToString()).ToArray(),
            SuportaRevisaoPersistida: settings.ReviewV2Enabled && snapshot.Modos.Contains(AssistenteModo.Redigir),
            SuportaConversaLivre: settings.AutomaticConversationEnabled && settings.ConversationV2Enabled && settings.ReviewV2Enabled && snapshot.Habilitado,
            Documentos: documentOptions?.Value is { Enabled: true } doc && snapshot.Habilitado && settings.AutomaticConversationEnabled
                ? new(doc.MaxFileBytes, doc.MaxPages, doc.MaxPixels / 1_000_000, doc.MaxAttachments, doc.MaxConversationBytes, doc.OcrEnabled) : null);
    }

    public async Task<AssistenteIaExecucao> PrepararAsync(
        Guid pecaId,
        CriarSolicitacaoAssistenteRequest request,
        CancellationToken cancellationToken,
        string? contextoConversa = null,
        bool conversaLivre = false, AssistenteDocumentTools? ferramentasDocumentais = null)
    {
        ValidateBaseRequest(pecaId, request);
        if (request.InteracaoAutomatica != conversaLivre || conversaLivre && (!Settings.AutomaticConversationEnabled || contextoConversa is null))
            throw Invalid("O fluxo de conversa solicitado não está disponível.");
        await featureAccess.ExigirModoAsync(authContext.OrganizationId, request.Modo, cancellationToken);
        await transparencyService.ExigirConfirmacaoAsync(cancellationToken);

        var peca = await pecaRepository.ObterPecaAsync(pecaId, authContext.OrganizationId, cancellationToken)
            ?? throw new AssistenteIaException(
                StatusCodes.Status404NotFound,
                "PECA_NOT_FOUND",
                "Peça não encontrada.");

        var contextoRequest = request.Contexto!;
        var documento = contextoRequest.Documento!;
        var snapshot = conversaLivre ? await featureAccess.ObterAsync(authContext.OrganizationId, cancellationToken) : null;
        IReadOnlyList<AssistenteOperacao> operacoesPermitidas = conversaLivre ?
            snapshot!.Modos.Contains(AssistenteModo.Redigir) ? [AssistenteOperacao.ResponderNoPainel, AssistenteOperacao.ProporAlteracoes] : [AssistenteOperacao.ResponderNoPainel]
            : GetAllowedOperations(request.Modo);
        var podeGerarMinuta = conversaLivre && snapshot!.Modos.Contains(AssistenteModo.Redigir) && snapshot.Features.Contains(OrganizationFeatureEnum.AI_DOCUMENT_GENERATION);
        if (conversaLivre && peca.Versao != request.VersaoPeca) throw AssistentePatchCompiler.Stale();
        var operacaoPreferida = request.OperacaoPreferida ?? GetDefaultOperation(request.Modo);
        ValidateDocumentContext(request.Modo, operacaoPreferida, documento);

        if (!operacoesPermitidas.Contains(operacaoPreferida))
            throw Invalid("A operação preferida não é compatível com o modo selecionado.");

        Caso? caso = null;
        IReadOnlyList<EnvolvidoCaso> envolvidos = [];
        if (contextoRequest.IncluirDadosCaso)
        {
            caso = await casoRepository.ObterCasoAsync(peca.CasoId, authContext.OrganizationId, cancellationToken)
                ?? throw new AssistenteIaException(
                    StatusCodes.Status404NotFound,
                    "CASO_NOT_FOUND",
                    "Caso da peça não encontrado.");
            envolvidos = await casoRepository.ListarEnvolvidosAsync(
                peca.CasoId,
                authContext.OrganizationId,
                cancellationToken);
        }

        var contextos = await LoadContextsAsync(
            peca.CasoId,
            contextoRequest.ContextoCasoIds,
            cancellationToken);
        var input = contextBuilder.Criar(
            request.Instrucao!.Trim(),
            documento,
            contextoRequest.ContextoAdicional,
            caso,
            envolvidos,
            contextos);
        if (contextoConversa is not null)
        {
            input = contextoConversa + input;
            if (input.Length > Settings.MaxTotalContextCharacters)
                throw new AssistenteIaException(413, "AI_HISTORY_CONTEXT_TOO_LARGE", "O histórico permanece salvo, mas o contexto completo excede o limite desta etapa.");
        }

        var correlationId = correlationContext.CorrelationId;
        var usageLease = await usageLimiter.AdquirirAsync(
            authContext.OrganizationId,
            authContext.UserId,
            cancellationToken);
        if (!requestRegistry.TryStart(authContext.OrganizationId, request.ClientRequestId))
        {
            await usageLease.DisposeAsync();
            throw new AssistenteIaException(
                StatusCodes.Status409Conflict,
                "AI_REQUEST_ALREADY_RUNNING",
                "Já existe uma solicitação com este identificador em andamento.",
                retryable: true);
        }

        var anchor = new AssistenteAncoraDto(
            documento.Selecao?.From,
            documento.Selecao?.To,
            documento.Cursor?.Posicao,
            documento.RevisaoEditor,
            documento.Selecao?.TextoOriginal);
        var providerRequest = new AssistenteIaProviderRequest(
            (conversaLivre ? promptPolicy.CriarInstrucoesLivres(operacoesPermitidas.Contains(AssistenteOperacao.ProporAlteracoes), podeGerarMinuta) :
                promptPolicy.CriarInstrucoes(request.Modo, operacoesPermitidas)) + (contextoConversa is null ? "" : AssistenteConversationPolicy.Instrucoes) +
                (ferramentasDocumentais?.HasDocumentTools == true ? AssistenteDocumentRetrievalPolicy.Instructions : "") +
                (ferramentasDocumentais?.AdditionalInstructions ?? ""),
            input,
            request.Modo,
            operacoesPermitidas,
            Settings.MaxOutputTokens, Conversacional: conversaLivre, PodeGerarMinuta: podeGerarMinuta, FerramentasDocumentais: ferramentasDocumentais);
        var solicitationId = Guid.NewGuid();
        var startedAt = TimeProvider.System.GetTimestamp();
        var execution = new AssistenteIaExecucao(
            solicitationId,
            request.ClientRequestId,
            authContext.OrganizationId,
            authContext.UserId,
            pecaId,
            correlationId,
            request.Modo,
            anchor,
            providerRequest,
            InputTokensContados: null,
            StartedAtTimestamp: startedAt,
            UsageLease: usageLease,
            PromptVersion: (conversaLivre ? AssistentePromptPolicy.AutomaticVersion : AssistentePromptPolicy.Version) + (contextoConversa is null ? "" : "/" + AssistenteConversationPolicy.Version) +
                (ferramentasDocumentais?.HasDocumentTools == true ? "/" + AssistenteDocumentRetrievalPolicy.Version : "") +
                (ferramentasDocumentais?.AdditionalPolicyVersion ?? ""),
            DocumentoBase: conversaLivre ? peca.ConteudoJson.RootElement.Clone() : null);

        try
        {
            using var timeout = CreateTimeout(cancellationToken, startedAt);
            if (ferramentasDocumentais?.BeforeCall is not null) await ferramentasDocumentais.BeforeCall(timeout.Token);
            var inputTokens = await tokenCounter.ContarTokensEntradaAsync(providerRequest, timeout.Token);
            execution = execution with { InputTokensContados = inputTokens };
            if (ferramentasDocumentais is not null)
            {
                ferramentasDocumentais.InitialInputTokens = inputTokens;
                ferramentasDocumentais.MaxInputTokens = Settings.MaxInputTokens;
                var currentExecution = execution;
                ferramentasDocumentais.RecordCall = call => RecordProviderCallAsync(currentExecution, call);
            }
            if (inputTokens > Settings.MaxInputTokens)
                throw InputTooLarge("O contexto selecionado ultrapassa o limite de tokens permitido.");

            logger.LogInformation(
                "Solicitação de IA preparada. SolicitacaoId={SolicitacaoId} ClientRequestId={ClientRequestId} OrganizationId={OrganizationId} UserId={UserId} PecaId={PecaId} Modo={Modo} Escopo={Escopo} VersaoCliente={VersaoCliente} VersaoServidor={VersaoServidor} PromptVersion={PromptVersion} EntradaTokens={EntradaTokens}",
                solicitationId,
                request.ClientRequestId,
                authContext.OrganizationId,
                authContext.UserId,
                pecaId,
                request.Modo,
                documento.Escopo,
                request.VersaoPeca,
                peca.Versao,
                execution.PromptVersion,
                inputTokens);

            return execution;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RecordUsageAsync(execution, null, Settings.Model, execution.InputTokensContados, null, null,
                "cancelled", "AI_CANCELLED", ElapsedMilliseconds(startedAt));
            await FinalizarAsync(execution);
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Timeout na contagem de tokens da IA. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} PecaId={PecaId}",
                execution.SolicitacaoId,
                execution.OrganizationId,
                execution.PecaId);
            await RecordUsageAsync(execution, null, Settings.Model, execution.InputTokensContados, null, null,
                "failed", "AI_PROVIDER_TIMEOUT", ElapsedMilliseconds(startedAt));
            await FinalizarAsync(execution);
            throw new AssistenteIaException(
                StatusCodes.Status504GatewayTimeout,
                "AI_PROVIDER_TIMEOUT",
                "A IA demorou mais que o esperado. Tente novamente.",
                retryable: true);
        }
        catch (AssistenteIaProviderException ex)
        {
            logger.LogWarning(
                "Falha na contagem de tokens da IA. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} PecaId={PecaId} Code={Code} ProviderStatusCode={ProviderStatusCode}",
                execution.SolicitacaoId,
                execution.OrganizationId,
                execution.PecaId,
                ex.Code,
                ex.ProviderStatusCode);
            await RecordUsageAsync(execution, null, Settings.Model, execution.InputTokensContados, null, null,
                "failed", ex.Code, ElapsedMilliseconds(startedAt));
            await FinalizarAsync(execution);
            throw MapProviderException(ex);
        }
        catch (AssistenteIaException ex)
        {
            await RecordUsageAsync(execution, null, Settings.Model, execution.InputTokensContados, null, null,
                "failed", ex.Code, ElapsedMilliseconds(startedAt));
            await FinalizarAsync(execution);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Falha inesperada na contagem de tokens da IA. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} PecaId={PecaId} ErrorType={ErrorType}",
                execution.SolicitacaoId,
                execution.OrganizationId,
                execution.PecaId,
                ex.GetType().Name);
            await RecordUsageAsync(execution, null, Settings.Model, execution.InputTokensContados, null, null,
                "failed", "AI_PROVIDER_ERROR", ElapsedMilliseconds(startedAt));
            await FinalizarAsync(execution);
            throw new AssistenteIaException(
                StatusCodes.Status502BadGateway,
                "AI_PROVIDER_ERROR",
                "Não foi possível preparar a solicitação de IA.",
                retryable: true);
        }
    }

    public async Task<AssistenteSolicitacaoConcluidaDto> GerarAsync(
        AssistenteIaExecucao execution,
        CancellationToken cancellationToken)
    {
        var startedAt = execution.StartedAtTimestamp;
        AssistenteIaProviderResult? providerResult = null;
        try
        {
            using var timeout = CreateTimeout(cancellationToken, startedAt);
            var result = providerResult = await provider.GerarAsync(execution.ProviderRequest, timeout.Token);
            ValidateProviderResult(result, execution.Modo, execution.ProviderRequest.OperacoesPermitidas);
            AssistenteCoberturaDocumentalDto? coverage = null;
            if (execution.ProviderRequest.FerramentasDocumentais is { HasDocumentTools: true } tools)
            {
                if (result.CoberturaPretendida is not ("Todos" or "Relevantes")) throw ProviderOutputInvalid("A IA não indicou o escopo da consulta documental.");
                tools.SetScope?.Invoke(result.CoberturaPretendida);
                coverage = tools.Coverage;
                result = AssistenteDocumentRetrievalPolicy.ValidateCoverage(result, coverage);
            }
            if (execution.ProviderRequest.Conversacional)
            {
                var draft = result.RascunhoAutomatico;
                if (draft is null || !Enum.IsDefined(draft.Intencao) || draft.Grupos is null ||
                    (draft.Intencao == AssistenteIntencao.Responder) != (result.OperacaoSugerida == AssistenteOperacao.ResponderNoPainel) ||
                    (draft.Intencao == AssistenteIntencao.Responder ? draft.Grupos.Count != 0 || draft.SubstituirPropostaReferenciada : draft.Grupos.Count == 0))
                    throw ProviderOutputInvalid("A IA retornou uma intenção ou proposta inconsistente.");
                if (draft.Intencao != AssistenteIntencao.Responder)
                {
                    try { new AssistentePatchCompiler().Compilar(execution.DocumentoBase!.Value, Guid.NewGuid(), draft.Grupos); }
                    catch (AssistenteIaException) { throw ProviderOutputInvalid("A IA retornou alterações que não puderam ser validadas. A Peça foi preservada."); }
                    if ((draft.Intencao == AssistenteIntencao.GerarMinuta || AssistenteAutomaticProposalPolicy.CobreDocumento(execution.DocumentoBase!.Value, draft.Grupos)) && !execution.ProviderRequest.PodeGerarMinuta)
                        throw ProviderOutputInvalid("A proposta abrange a Peça inteira sem autorização de geração integral.");
                }
            }
            var inputTokens = result.EntradaTokens ?? execution.InputTokensContados;

            var suggestion = new AssistenteSugestaoDto(
                Guid.NewGuid(),
                execution.Modo,
                result.OperacaoSugerida,
                result.Titulo.Trim(),
                result.Texto.Trim(),
                result.Resumo.Trim(),
                result.Alertas,
                result.Achados,
                execution.Ancora);

            logger.LogInformation(
                "Solicitação de IA concluída. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} UserId={UserId} PecaId={PecaId} Modo={Modo} Modelo={Modelo} EntradaTokens={EntradaTokens} SaidaTokens={SaidaTokens} DuracaoMs={DuracaoMs}",
                execution.SolicitacaoId,
                execution.OrganizationId,
                execution.UserId,
                execution.PecaId,
                execution.Modo,
                result.Modelo,
                inputTokens,
                result.SaidaTokens,
                TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);

            await RecordUsageAsync(
                execution,
                result.OperacaoSugerida,
                result.Modelo,
                inputTokens,
                result.EntradaTokensEmCache,
                result.SaidaTokens,
                "succeeded",
                null,
                ElapsedMilliseconds(startedAt));

            return new AssistenteSolicitacaoConcluidaDto(
                suggestion,
                new AssistenteUsoDto(inputTokens, result.SaidaTokens, result.EntradaTokensEmCache),
                result.Modelo,
                RascunhoAutomatico: execution.ProviderRequest.Conversacional ? result.RascunhoAutomatico : null,
                CoberturaDocumental: coverage);
        }
        catch (AssistenteAnalysisQueuedException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RecordUsageAsync(execution, null, Settings.Model, execution.InputTokensContados, null, null,
                "cancelled", "AI_CANCELLED", ElapsedMilliseconds(startedAt));
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Timeout no provider de IA. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} PecaId={PecaId}",
                execution.SolicitacaoId,
                execution.OrganizationId,
                execution.PecaId);
            await RecordUsageAsync(execution, null, Settings.Model, execution.InputTokensContados, null, null,
                "failed", "AI_PROVIDER_TIMEOUT", ElapsedMilliseconds(startedAt));
            throw new AssistenteIaException(
                StatusCodes.Status504GatewayTimeout,
                "AI_PROVIDER_TIMEOUT",
                "A IA demorou mais que o esperado. Tente novamente.",
                retryable: true);
        }
        catch (AssistenteIaProviderException ex)
        {
            logger.LogWarning(
                "Falha no provider de IA. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} PecaId={PecaId} Code={Code} ProviderStatusCode={ProviderStatusCode}",
                execution.SolicitacaoId,
                execution.OrganizationId,
                execution.PecaId,
                ex.Code,
                ex.ProviderStatusCode);
            await RecordUsageAsync(execution, null, Settings.Model, execution.InputTokensContados, null, null,
                "failed", ex.Code, ElapsedMilliseconds(startedAt));
            throw MapProviderException(ex);
        }
        catch (AssistenteIaException ex)
        {
            await RecordUsageAsync(execution, providerResult?.OperacaoSugerida, providerResult?.Modelo ?? Settings.Model,
                providerResult?.EntradaTokens ?? execution.InputTokensContados,
                providerResult?.EntradaTokensEmCache,
                providerResult?.SaidaTokens,
                "failed", ex.Code, ElapsedMilliseconds(startedAt));
            throw;
        }
        catch (Exception)
        {
            await RecordUsageAsync(execution, providerResult?.OperacaoSugerida, providerResult?.Modelo ?? Settings.Model,
                providerResult?.EntradaTokens ?? execution.InputTokensContados,
                providerResult?.EntradaTokensEmCache,
                providerResult?.SaidaTokens,
                "failed", "AI_PROVIDER_ERROR", ElapsedMilliseconds(startedAt));
            throw;
        }
    }

    public async ValueTask FinalizarAsync(AssistenteIaExecucao execution)
    {
        requestRegistry.Complete(execution.OrganizationId, execution.ClientRequestId);
        await execution.UsageLease.DisposeAsync();
    }

    private void ValidateBaseRequest(Guid pecaId, CriarSolicitacaoAssistenteRequest request)
    {
        if (pecaId == Guid.Empty || request.ClientRequestId == Guid.Empty)
            throw Invalid("Os identificadores da peça e da solicitação são obrigatórios.");
        if (!Enum.IsDefined(request.Modo) ||
            request.OperacaoPreferida.HasValue && !Enum.IsDefined(request.OperacaoPreferida.Value))
            throw Invalid("O modo ou a operação informada não é suportado.");
        if (request.VersaoPeca <= 0)
            throw Invalid("A versão conhecida da peça deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(request.Instrucao))
            throw Invalid("A instrução é obrigatória.");
        if (request.Instrucao.Trim().Length > Settings.MaxInstructionCharacters)
            throw TooLarge("A instrução ultrapassa o limite permitido.");
        if (request.Contexto?.Documento is null)
            throw Invalid("O contexto do documento é obrigatório.");
        if ((request.Contexto.ContextoAdicional?.Trim().Length ?? 0) > Settings.MaxAdditionalContextCharacters)
            throw TooLarge("O contexto adicional ultrapassa o limite permitido.");
        if ((request.Contexto.Documento.Texto?.Length ?? 0) > Settings.MaxDocumentContextCharacters)
            throw TooLarge("O contexto do documento ultrapassa o limite permitido.");
        if ((request.Contexto.ContextoCasoIds?.Count ?? 0) > 20)
            throw TooLarge("Foram selecionados contextos do caso em excesso.");
    }

    public void ValidarEntrada(Guid pecaId, CriarSolicitacaoAssistenteRequest request)
    {
        ValidateBaseRequest(pecaId, request);
        ValidateDocumentContext(request.Modo, request.OperacaoPreferida ?? GetDefaultOperation(request.Modo), request.Contexto!.Documento!);
    }

    private static void ValidateDocumentContext(
        AssistenteModo modo,
        AssistenteOperacao preferredOperation,
        AssistenteDocumentoRequest documento)
    {
        var text = documento.Texto?.Trim() ?? string.Empty;
        if (!Enum.IsDefined(documento.Escopo))
            throw Invalid("O escopo do documento não é suportado.");
        if (documento.Escopo == AssistenteEscopoDocumento.Nenhum && text.Length > 0)
            throw Invalid("O escopo Nenhum não pode enviar texto do documento.");
        if (documento.Escopo != AssistenteEscopoDocumento.Nenhum && text.Length == 0)
            throw Invalid("O escopo selecionado exige texto do documento.");
        if (documento.RevisaoEditor < 0)
            throw Invalid("A revisão do editor não pode ser negativa.");

        var selection = documento.Selecao;
        if (modo == AssistenteModo.Reescrever &&
            (documento.Escopo != AssistenteEscopoDocumento.Selecao || selection is null ||
             string.IsNullOrWhiteSpace(selection.TextoOriginal)))
            throw Incompatible("O modo Reescrever exige uma seleção não vazia.");
        if (selection is not null &&
            (selection.From < 0 || selection.To <= selection.From || string.IsNullOrWhiteSpace(selection.TextoOriginal)))
            throw Incompatible("A seleção informada é inválida.");
        if (documento.Escopo == AssistenteEscopoDocumento.Selecao && selection is null)
            throw Incompatible("O escopo Seleção exige as âncoras da seleção.");
        if (selection is not null && documento.Escopo == AssistenteEscopoDocumento.Selecao &&
            !string.Equals(text, selection.TextoOriginal.Trim(), StringComparison.Ordinal))
            throw Incompatible("O texto da seleção não corresponde ao snapshot informado.");
        if (documento.Cursor is { Posicao: < 0 })
            throw Incompatible("A posição do cursor é inválida.");
        if (preferredOperation == AssistenteOperacao.InserirNoCursor && documento.Cursor is null)
            throw Incompatible("A inserção no cursor exige uma posição de destino.");
        if (preferredOperation is AssistenteOperacao.InserirAposSelecao or AssistenteOperacao.SubstituirSelecao && selection is null)
            throw Incompatible("A operação selecionada exige uma seleção.");
    }

    private async Task<IReadOnlyList<ContextoCaso>> LoadContextsAsync(
        Guid casoId,
        IReadOnlyList<Guid>? contextIds,
        CancellationToken cancellationToken)
    {
        if (contextIds is null || contextIds.Count == 0) return [];
        var result = new List<ContextoCaso>(contextIds.Count);
        foreach (var id in contextIds.Distinct())
        {
            if (id == Guid.Empty) throw Incompatible("Um contexto do caso possui identificador inválido.");
            var item = await casoRepository.ObterContextoAsync(
                casoId,
                id,
                authContext.OrganizationId,
                cancellationToken);
            if (item is null)
                throw Incompatible("Um dos contextos selecionados não pertence ao caso da peça.");
            result.Add(item);
        }
        return result;
    }

    private void ValidateProviderResult(
        AssistenteIaProviderResult result,
        AssistenteModo mode,
        IReadOnlyList<AssistenteOperacao> allowedOperations)
    {
        if (!allowedOperations.Contains(result.OperacaoSugerida))
            throw ProviderOutputInvalid("A IA retornou uma operação incompatível com a solicitação.");
        if (mode != AssistenteModo.Revisar && string.IsNullOrWhiteSpace(result.Texto))
            throw ProviderOutputInvalid("A IA retornou uma sugestão sem conteúdo.");
        if (result.Texto.Length > Settings.MaxOutputCharacters)
            throw ProviderOutputInvalid("A resposta da IA ultrapassou o limite permitido.");
        if (result.Titulo.Length > 200 || result.Resumo.Length > 2_000)
            throw ProviderOutputInvalid("A resposta da IA contém metadados inválidos.");
        if (result.Alertas.Count > 20 || result.Achados.Count > 30)
            throw ProviderOutputInvalid("A resposta da IA contém itens em excesso.");
        if (result.Alertas.Any(x => string.IsNullOrWhiteSpace(x.Codigo) || string.IsNullOrWhiteSpace(x.Mensagem)) ||
            result.Achados.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Titulo) ||
                                    string.IsNullOrWhiteSpace(x.Descricao)))
            throw ProviderOutputInvalid("A resposta da IA contém itens incompletos.");
    }

    private static IReadOnlyList<AssistenteOperacao> GetAllowedOperations(AssistenteModo mode) => mode switch
    {
        AssistenteModo.Perguntar => [AssistenteOperacao.ResponderNoPainel],
        AssistenteModo.Redigir => [AssistenteOperacao.InserirNoCursor, AssistenteOperacao.InserirAposSelecao, AssistenteOperacao.CriarSecao],
        AssistenteModo.Reescrever => [AssistenteOperacao.SubstituirSelecao],
        AssistenteModo.Revisar => [AssistenteOperacao.ResponderNoPainel],
        _ => throw Invalid("O modo informado não é suportado.")
    };

    private static AssistenteOperacao GetDefaultOperation(AssistenteModo mode) => mode switch
    {
        AssistenteModo.Perguntar => AssistenteOperacao.ResponderNoPainel,
        AssistenteModo.Redigir => AssistenteOperacao.InserirNoCursor,
        AssistenteModo.Reescrever => AssistenteOperacao.SubstituirSelecao,
        AssistenteModo.Revisar => AssistenteOperacao.ResponderNoPainel,
        _ => throw Invalid("O modo informado não é suportado.")
    };

    private static AssistenteIaException MapProviderException(AssistenteIaProviderException ex) => ex.Code switch
    {
        "AI_RATE_LIMITED" => new(StatusCodes.Status429TooManyRequests, ex.Code, "O limite de uso da IA foi atingido. Tente novamente mais tarde.", true),
        "AI_PROVIDER_TIMEOUT" => new(StatusCodes.Status504GatewayTimeout, ex.Code, "A IA demorou mais que o esperado. Tente novamente.", true),
        "AI_UNAVAILABLE" => new(StatusCodes.Status503ServiceUnavailable, ex.Code, "O provedor de IA está indisponível.", true),
        _ => new(StatusCodes.Status502BadGateway, "AI_PROVIDER_ERROR", "Não foi possível gerar uma resposta válida.", ex.Retryable)
    };

    private static AssistenteIaException Invalid(string message) =>
        new(StatusCodes.Status400BadRequest, "AI_INVALID_REQUEST", message);

    private static AssistenteIaException Incompatible(string message) =>
        new(StatusCodes.Status422UnprocessableEntity, "AI_CONTEXT_INCOMPATIBLE", message);

    private static AssistenteIaException TooLarge(string message) =>
        new(StatusCodes.Status413PayloadTooLarge, "AI_CONTEXT_TOO_LARGE", message);

    private static AssistenteIaException InputTooLarge(string message) =>
        new(StatusCodes.Status413PayloadTooLarge, "AI_INPUT_TOO_LARGE", message);

    private static AssistenteIaException ProviderOutputInvalid(string message) =>
        new(StatusCodes.Status502BadGateway, "AI_PROVIDER_ERROR", message);

    private Task RecordUsageAsync(
        AssistenteIaExecucao execution,
        AssistenteOperacao? operation,
        string model,
        int? inputTokens,
        int? cachedInputTokens,
        int? outputTokens,
        string status,
        string? errorCode,
        long durationMs) => execution.ProviderRequest.FerramentasDocumentais?.RecordedCalls > 0 ? Task.CompletedTask : usageRecorder.RegistrarAsync(new AssistenteUsageRecord(
            execution.OrganizationId,
            execution.UserId,
            execution.PecaId,
            execution.SolicitacaoId,
            execution.ClientRequestId,
            execution.CorrelationId,
            execution.Modo,
            operation,
            Settings.Provider,
            model,
            execution.PromptVersion,
            AttemptCount: 1,
            inputTokens,
            cachedInputTokens,
            outputTokens,
            status,
            errorCode,
            durationMs),
        CancellationToken.None);

    private Task RecordProviderCallAsync(AssistenteIaExecucao execution, AssistenteProviderCall call) =>
        usageRecorder.RegistrarAsync(new AssistenteUsageRecord(execution.OrganizationId, execution.UserId, execution.PecaId,
            execution.SolicitacaoId, execution.ClientRequestId, execution.CorrelationId, execution.Modo, null,
            Settings.Provider, call.Model, execution.PromptVersion, call.Ordinal, call.InputTokens, call.CachedInputTokens,
            call.OutputTokens, call.Status, call.ErrorCode, call.DurationMs, execution.ProviderRequest.FerramentasDocumentais?.RequireDurableUsage == true), CancellationToken.None);

    private static long ElapsedMilliseconds(long startedAt) =>
        (long)TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds;

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken, long startedAt)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = TimeSpan.FromSeconds(Settings.TimeoutSeconds) -
                        TimeProvider.System.GetElapsedTime(startedAt);
        timeout.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        return timeout;
    }
}
