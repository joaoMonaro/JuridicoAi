using JuridicoAI.Models;

namespace JuridicoAI.Service;

public interface IJurisprudenciaProvider
{
    string Tribunal { get; }

    Task<IReadOnlyList<JurisprudenciaResultado>> PesquisarAsync(
        string termo,
        int quantidadeMaxima = 10,
        CancellationToken cancellationToken = default);
}
