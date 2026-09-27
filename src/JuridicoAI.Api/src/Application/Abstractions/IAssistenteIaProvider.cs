using JuridicoAI.Api.Application.DTOs;

namespace JuridicoAI.Api.Application.Abstractions;

public sealed record AssistenteIaProviderRequest(
    string Instructions,
    string Input,
    AssistenteModo Modo,
    IReadOnlyList<AssistenteOperacao> OperacoesPermitidas,
    int MaxOutputTokens);

public sealed record AssistenteIaProviderResult(
    string Titulo,
    string Texto,
    string Resumo,
    AssistenteOperacao OperacaoSugerida,
    IReadOnlyList<AssistenteAlertaDto> Alertas,
    IReadOnlyList<AssistenteAchadoDto> Achados,
    int? EntradaTokens,
    int? EntradaTokensEmCache,
    int? SaidaTokens,
    string Modelo);

public interface IAssistenteIaProvider
{
    Task<AssistenteIaProviderResult> GerarAsync(
        AssistenteIaProviderRequest request,
        CancellationToken cancellationToken);
}

public sealed class AssistenteIaProviderException(
    string code,
    bool retryable,
    int? providerStatusCode = null,
    Exception? innerException = null)
    : Exception("Falha ao gerar resposta no provedor de IA.", innerException)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
    public int? ProviderStatusCode { get; } = providerStatusCode;
}
