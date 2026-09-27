using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed record AssistenteIaExecucao(
    Guid SolicitacaoId,
    Guid ClientRequestId,
    Guid OrganizationId,
    Guid PecaId,
    AssistenteModo Modo,
    AssistenteAncoraDto Ancora,
    AssistenteIaProviderRequest ProviderRequest);

public sealed class AssistenteIaApplicationService(
    IPecaRepository pecaRepository,
    ICasoRepository casoRepository,
    IAuthContext authContext,
    IAssistenteIaProvider provider,
    AssistenteContextBuilder contextBuilder,
    AssistentePromptPolicy promptPolicy,
    AssistenteRequestRegistry requestRegistry,
    IOptions<AssistenteIaSettings> options,
    ILogger<AssistenteIaApplicationService> logger)
{
    private static readonly AssistenteModo[] SupportedModes = Enum.GetValues<AssistenteModo>();
    private readonly AssistenteIaSettings _settings = options.Value;

    public AssistenteCapacidadesDto ObterCapacidades() => new(
        _settings.Enabled,
        SupportedModes,
        new AssistenteLimitesDto(
            _settings.MaxInstructionCharacters,
            _settings.MaxDocumentContextCharacters,
            _settings.MaxAdditionalContextCharacters),
        SuportaStreaming: true,
        SuportaHistorico: false);

    public async Task<AssistenteIaExecucao> PrepararAsync(
        Guid pecaId,
        CriarSolicitacaoAssistenteRequest request,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        ValidateBaseRequest(pecaId, request);

        var peca = await pecaRepository.ObterPecaAsync(pecaId, authContext.OrganizationId, cancellationToken)
            ?? throw new AssistenteIaException(
                StatusCodes.Status404NotFound,
                "PECA_NOT_FOUND",
                "Peça não encontrada.");

        var contextoRequest = request.Contexto!;
        var documento = contextoRequest.Documento!;
        var operacoesPermitidas = GetAllowedOperations(request.Modo);
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

        if (!requestRegistry.TryStart(authContext.OrganizationId, request.ClientRequestId))
            throw new AssistenteIaException(
                StatusCodes.Status409Conflict,
                "AI_REQUEST_ALREADY_RUNNING",
                "Já existe uma solicitação com este identificador em andamento.",
                retryable: true);

        var anchor = new AssistenteAncoraDto(
            documento.Selecao?.From,
            documento.Selecao?.To,
            documento.Cursor?.Posicao,
            documento.RevisaoEditor,
            documento.Selecao?.TextoOriginal);
        var providerRequest = new AssistenteIaProviderRequest(
            promptPolicy.CriarInstrucoes(request.Modo, operacoesPermitidas),
            input,
            request.Modo,
            operacoesPermitidas,
            _settings.MaxOutputTokens);
        var solicitationId = Guid.NewGuid();

        logger.LogInformation(
            "Solicitação de IA preparada. SolicitacaoId={SolicitacaoId} ClientRequestId={ClientRequestId} OrganizationId={OrganizationId} UserId={UserId} PecaId={PecaId} Modo={Modo} Escopo={Escopo} VersaoCliente={VersaoCliente} VersaoServidor={VersaoServidor} PromptVersion={PromptVersion}",
            solicitationId,
            request.ClientRequestId,
            authContext.OrganizationId,
            authContext.UserId,
            pecaId,
            request.Modo,
            documento.Escopo,
            request.VersaoPeca,
            peca.Versao,
            AssistentePromptPolicy.Version);

        return new AssistenteIaExecucao(
            solicitationId,
            request.ClientRequestId,
            authContext.OrganizationId,
            pecaId,
            request.Modo,
            anchor,
            providerRequest);
    }

    public async Task<AssistenteSolicitacaoConcluidaDto> GerarAsync(
        AssistenteIaExecucao execution,
        CancellationToken cancellationToken)
    {
        var startedAt = TimeProvider.System.GetTimestamp();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            var result = await provider.GerarAsync(execution.ProviderRequest, timeout.Token);
            ValidateProviderResult(result, execution.Modo, execution.ProviderRequest.OperacoesPermitidas);

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
                authContext.UserId,
                execution.PecaId,
                execution.Modo,
                result.Modelo,
                result.EntradaTokens,
                result.SaidaTokens,
                TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);

            return new AssistenteSolicitacaoConcluidaDto(
                suggestion,
                new AssistenteUsoDto(result.EntradaTokens, result.SaidaTokens),
                result.Modelo);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Timeout no provider de IA. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} PecaId={PecaId}",
                execution.SolicitacaoId,
                execution.OrganizationId,
                execution.PecaId);
            throw new AssistenteIaException(
                StatusCodes.Status504GatewayTimeout,
                "AI_PROVIDER_TIMEOUT",
                "A IA demorou mais que o esperado. Tente novamente.",
                retryable: true);
        }
        catch (AssistenteIaProviderException ex)
        {
            logger.LogWarning(
                ex,
                "Falha no provider de IA. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} PecaId={PecaId} Code={Code} ProviderStatusCode={ProviderStatusCode}",
                execution.SolicitacaoId,
                execution.OrganizationId,
                execution.PecaId,
                ex.Code,
                ex.ProviderStatusCode);
            throw MapProviderException(ex);
        }
    }

    public void Finalizar(AssistenteIaExecucao execution) =>
        requestRegistry.Complete(execution.OrganizationId, execution.ClientRequestId);

    private void EnsureEnabled()
    {
        if (!_settings.Enabled)
            throw new AssistenteIaException(
                StatusCodes.Status503ServiceUnavailable,
                "AI_UNAVAILABLE",
                "O assistente de IA não está habilitado.",
                retryable: true);
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
        if (request.Instrucao.Trim().Length > _settings.MaxInstructionCharacters)
            throw TooLarge("A instrução ultrapassa o limite permitido.");
        if (request.Contexto?.Documento is null)
            throw Invalid("O contexto do documento é obrigatório.");
        if ((request.Contexto.ContextoAdicional?.Trim().Length ?? 0) > _settings.MaxAdditionalContextCharacters)
            throw TooLarge("O contexto adicional ultrapassa o limite permitido.");
        if ((request.Contexto.Documento.Texto?.Length ?? 0) > _settings.MaxDocumentContextCharacters)
            throw TooLarge("O contexto do documento ultrapassa o limite permitido.");
        if ((request.Contexto.ContextoCasoIds?.Count ?? 0) > 20)
            throw TooLarge("Foram selecionados contextos do caso em excesso.");
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
        if (result.Texto.Length > _settings.MaxOutputCharacters)
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
        "AI_RATE_LIMITED" => new(StatusCodes.Status429TooManyRequests, ex.Code, "O limite de uso da IA foi atingido. Tente novamente mais tarde.", true, ex),
        "AI_PROVIDER_TIMEOUT" => new(StatusCodes.Status504GatewayTimeout, ex.Code, "A IA demorou mais que o esperado. Tente novamente.", true, ex),
        "AI_UNAVAILABLE" => new(StatusCodes.Status503ServiceUnavailable, ex.Code, "O provedor de IA está indisponível.", true, ex),
        _ => new(StatusCodes.Status502BadGateway, "AI_PROVIDER_ERROR", "Não foi possível gerar uma resposta válida.", ex.Retryable, ex)
    };

    private static AssistenteIaException Invalid(string message) =>
        new(StatusCodes.Status400BadRequest, "AI_INVALID_REQUEST", message);

    private static AssistenteIaException Incompatible(string message) =>
        new(StatusCodes.Status422UnprocessableEntity, "AI_CONTEXT_INCOMPATIBLE", message);

    private static AssistenteIaException TooLarge(string message) =>
        new(StatusCodes.Status413PayloadTooLarge, "AI_CONTEXT_TOO_LARGE", message);

    private static AssistenteIaException ProviderOutputInvalid(string message) =>
        new(StatusCodes.Status502BadGateway, "AI_PROVIDER_ERROR", message);
}
