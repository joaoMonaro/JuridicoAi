using JuridicoAI.Domain.Entities;

namespace JuridicoAI.Application.Services;

public interface IJurisprudenciaProvider
{
    string Tribunal { get; }

    Task<IReadOnlyList<JurisprudenciaResultado>> PesquisarAsync(
        string termo,
        int quantidadeMaxima = 10,
        CancellationToken cancellationToken = default);
}
