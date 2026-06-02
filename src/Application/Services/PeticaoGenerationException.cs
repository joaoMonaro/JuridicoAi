namespace JuridicoAI.Application.Services;

public sealed class PeticaoGenerationException : Exception
{
    public PeticaoGenerationException(string message)
        : base(message)
    {
    }

    public PeticaoGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
