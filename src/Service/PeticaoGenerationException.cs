namespace JuridicoAI.Service;

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
