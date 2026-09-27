namespace JuridicoAI.Api.Application.Exceptions;

public sealed class AssistenteIaException(
    int statusCode,
    string code,
    string message,
    bool retryable = false,
    Exception? innerException = null)
    : ApiException(message, innerException)
{
    public override int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}
