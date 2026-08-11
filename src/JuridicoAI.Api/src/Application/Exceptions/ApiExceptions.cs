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
