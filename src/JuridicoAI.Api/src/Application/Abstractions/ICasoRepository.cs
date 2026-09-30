using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Application.Abstractions;

public interface ICasoRepository
{
    Task<Caso> CriarCasoAsync(Caso caso, CancellationToken cancellationToken);
    Task<IReadOnlyList<Caso>> ListarCasosAsync(Guid organizationId, CasoStatusEnum? status, CancellationToken cancellationToken);
    Task<Caso?> ObterCasoAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken cancellationToken);
    Task<bool> ArquivarCasoAsync(Guid id, Guid organizationId, Guid updatedById, DateTime dataAtualizacao, CancellationToken cancellationToken);

    Task<EnvolvidoCaso> CriarEnvolvidoAsync(EnvolvidoCaso envolvido, CancellationToken cancellationToken);
    Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken);
    Task<EnvolvidoCaso?> ObterEnvolvidoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> AtualizarEnvolvidoAsync(EnvolvidoCaso envolvido, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> RemoverEnvolvidoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken);

    Task<EnderecoEnvolvidoCaso> CriarEnderecoAsync(Guid casoId, Guid organizationId, EnderecoEnvolvidoCaso endereco, CancellationToken cancellationToken);
    Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosAsync(Guid casoId, Guid envolvidoId, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosCasoAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken);
    Task<EnderecoEnvolvidoCaso?> ObterEnderecoAsync(Guid casoId, Guid envolvidoId, Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> AtualizarEnderecoAsync(Guid casoId, Guid organizationId, EnderecoEnvolvidoCaso endereco, CancellationToken cancellationToken);
    Task<bool> RemoverEnderecoAsync(Guid casoId, Guid envolvidoId, Guid id, Guid organizationId, CancellationToken cancellationToken);

    Task<ContextoCaso> CriarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken);
    Task<ContextoCaso?> ObterContextoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> AtualizarContextoAsync(ContextoCaso contexto, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> RemoverContextoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken);

    Task<DocumentoCaso> CriarDocumentoAsync(DocumentoCaso documento, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken);
    Task<DocumentoCaso?> ObterDocumentoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> RemoverDocumentoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken);
}
