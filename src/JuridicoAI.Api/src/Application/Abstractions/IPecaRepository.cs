using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Abstractions;

public interface IPecaRepository
{
    Task<bool> CasoExisteAsync(Guid casoId, Guid organizationId, CancellationToken ct);
    Task<Peca> CriarPecaAsync(Peca peca, CancellationToken ct);
    Task<IReadOnlyList<PecaListaDto>> ListarPecasAsync(Guid casoId, Guid organizationId, CancellationToken ct);
    Task<Peca?> ObterPecaAsync(Guid id, Guid organizationId, CancellationToken ct);
    Task<bool> AtualizarPecaAsync(Peca peca, int versaoEsperada, CancellationToken ct);
    Task<bool> ExcluirPecaAsync(Guid id, Guid organizationId, Guid updatedById, int versaoEsperada, DateTime agora, CancellationToken ct);
    Task<TemplatePeca> CriarTemplateAsync(TemplatePeca template, CancellationToken ct);
    Task<IReadOnlyList<TemplatePeca>> ListarTemplatesAsync(Guid organizationId, CancellationToken ct);
    Task<TemplatePeca?> ObterTemplateAsync(Guid id, Guid organizationId, CancellationToken ct);
    Task<bool> AtualizarTemplateAsync(TemplatePeca template, CancellationToken ct);
    Task<bool> ExcluirTemplateAsync(Guid id, Guid organizationId, Guid updatedById, DateTime agora, CancellationToken ct);
}
