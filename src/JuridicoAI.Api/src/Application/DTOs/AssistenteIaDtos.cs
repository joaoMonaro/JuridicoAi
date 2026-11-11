namespace JuridicoAI.Api.Application.DTOs;

public enum AssistenteModo
{
    Perguntar,
    Redigir,
    Reescrever,
    Revisar
}

public enum AssistenteOperacao
{
    ResponderNoPainel,
    InserirNoCursor,
    InserirAposSelecao,
    SubstituirSelecao,
    CriarSecao,
    ProporAlteracoes
}

public enum AssistenteIntencao { Responder, EditarPeca, GerarMinuta }

public enum AssistenteEscopoDocumento
{
    Nenhum,
    Selecao,
    SecaoAtual,
    DocumentoCompleto
}

public enum AssistenteCategoriaAchado
{
    Lacuna,
    Contradicao,
    Clareza,
    Estrutura,
    Verificacao
}

public sealed record AssistenteSelecaoRequest(
    int From,
    int To,
    string? TextoOriginal);

public sealed record AssistenteCursorRequest(int Posicao);

public sealed record AssistenteDocumentoRequest(
    AssistenteEscopoDocumento Escopo,
    string? Texto = null,
    string? TituloSecao = null,
    AssistenteSelecaoRequest? Selecao = null,
    AssistenteCursorRequest? Cursor = null,
    long RevisaoEditor = 0);

public sealed record AssistenteContextoRequest(
    AssistenteDocumentoRequest? Documento = null,
    string? ContextoAdicional = null,
    bool IncluirDadosCaso = false,
    IReadOnlyList<Guid>? ContextoCasoIds = null);

public sealed record CriarSolicitacaoAssistenteRequest(
    Guid ClientRequestId,
    AssistenteModo Modo,
    string? Instrucao,
    int VersaoPeca,
    AssistenteOperacao? OperacaoPreferida,
    AssistenteContextoRequest? Contexto,
    bool InteracaoAutomatica = false,
    Guid? PropostaReferenciadaId = null);

public sealed record AssistenteLimitesDto(
    int InstrucaoCaracteres,
    int ContextoDocumentoCaracteres,
    int ContextoAdicionalCaracteres);

public sealed record AssistenteCapacidadesDto(
    bool Habilitado,
    IReadOnlyList<AssistenteModo> Modos,
    AssistenteLimitesDto Limites,
    bool SuportaStreaming,
    bool SuportaHistorico,
    IReadOnlyList<string>? Features = null,
    bool SuportaRevisaoPersistida = false,
    bool SuportaConversaLivre = false);

public sealed record AssistenteTransparenciaDto(
    string Versao,
    bool Confirmado,
    DateTime? ConfirmadoEm,
    string Titulo,
    string Mensagem,
    string AvisoDeRevisao,
    string? PoliticaDePrivacidadeUrl);

public sealed record ConfirmarAssistenteTransparenciaRequest(string? Versao);

public sealed record AssistenteAlertaDto(string Codigo, string Mensagem);

public sealed record AssistenteAchadoDto(
    string Id,
    AssistenteCategoriaAchado Categoria,
    string Titulo,
    string Descricao,
    IReadOnlyList<AssistenteAlertaDto> Alertas);

public sealed record AssistenteAncoraDto(
    int? From,
    int? To,
    int? Posicao,
    long RevisaoEditor,
    string? TextoOriginal);

public sealed record AssistenteSugestaoDto(
    Guid Id,
    AssistenteModo Modo,
    AssistenteOperacao OperacaoSugerida,
    string Titulo,
    string Texto,
    string Resumo,
    IReadOnlyList<AssistenteAlertaDto> Alertas,
    IReadOnlyList<AssistenteAchadoDto> Achados,
    AssistenteAncoraDto Ancora,
    Guid? MensagemUsuarioId = null);

public sealed record AssistenteUsoDto(int? EntradaTokens, int? SaidaTokens, int? EntradaTokensEmCache = null);

public sealed record AssistenteSolicitacaoIniciadaDto(
    Guid SolicitacaoId,
    Guid ClientRequestId,
    Guid CorrelationId);

public sealed record AssistenteSolicitacaoConcluidaDto(
    AssistenteSugestaoDto Sugestao,
    AssistenteUsoDto Uso,
    string Modelo,
    AssistentePropostaDto? Proposta = null,
    [property: System.Text.Json.Serialization.JsonIgnore] AssistenteRascunhoAutomatico? RascunhoAutomatico = null);

public sealed record AssistenteRascunhoAutomatico(AssistenteIntencao Intencao,
    IReadOnlyList<AssistenteGrupoRequest> Grupos, bool SubstituirPropostaReferenciada);

public sealed record AssistenteSolicitacaoFalhouDto(
    string Code,
    string Message,
    bool Retryable,
    string? RequestId);
