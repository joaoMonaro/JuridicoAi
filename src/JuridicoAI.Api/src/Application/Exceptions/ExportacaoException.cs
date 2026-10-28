namespace JuridicoAI.Api.Application.Exceptions;

public sealed class ExportacaoException(string message, bool timeout = false) : ApiException(message)
{
    public override int StatusCode => timeout ? StatusCodes.Status504GatewayTimeout : StatusCodes.Status503ServiceUnavailable;
}
