namespace JuridicoAI.Api.Application.Exceptions;

public abstract class ApiException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

public sealed class RequisicaoInvalidaException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

public sealed class RecursoNaoEncontradoException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public sealed class ArquivoMuitoGrandeException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status413PayloadTooLarge;
}

public sealed class TipoArquivoNaoSuportadoException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status415UnsupportedMediaType;
}

public sealed class ConflitoDeVersaoException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public string Code => "PECA_VERSION_CONFLICT";
}

public sealed class NaoAutorizadoException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}

public sealed class ConflitoException(string message, string? code = null) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public string? Code { get; } = code;
}
