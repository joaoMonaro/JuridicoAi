using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Abstractions;

public interface ICasoRepository
{
    Task<Caso> CriarCasoAsync(Caso caso, CancellationToken cancellationToken);
    Task<IReadOnlyList<Caso>> ListarCasosAsync(CasoStatus? status, CancellationToken cancellationToken);
    Task<Caso?> ObterCasoAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken cancellationToken);
    Task<bool> ArquivarCasoAsync(Guid id, DateTime dataAtualizacao, CancellationToken cancellationToken);

    Task<EnvolvidoCaso> CriarEnvolvidoAsync(EnvolvidoCaso envolvido, CancellationToken cancellationToken);
    Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(Guid casoId, CancellationToken cancellationToken);
    Task<EnvolvidoCaso?> ObterEnvolvidoAsync(Guid casoId, Guid id, CancellationToken cancellationToken);
    Task<bool> AtualizarEnvolvidoAsync(EnvolvidoCaso envolvido, CancellationToken cancellationToken);
    Task<bool> RemoverEnvolvidoAsync(Guid casoId, Guid id, CancellationToken cancellationToken);

    Task<ContextoCaso> CriarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(Guid casoId, CancellationToken cancellationToken);
    Task<ContextoCaso?> ObterContextoAsync(Guid casoId, Guid id, CancellationToken cancellationToken);
    Task<bool> AtualizarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken);
    Task<bool> RemoverContextoAsync(Guid casoId, Guid id, CancellationToken cancellationToken);

    Task<DocumentoCaso> CriarDocumentoAsync(DocumentoCaso documento, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(Guid casoId, CancellationToken cancellationToken);
    Task<DocumentoCaso?> ObterDocumentoAsync(Guid casoId, Guid id, CancellationToken cancellationToken);
    Task<bool> RemoverDocumentoAsync(Guid casoId, Guid id, CancellationToken cancellationToken);
}
