using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Abstractions;

public interface IPecaRepository
{
    Task<bool> CasoExisteAsync(Guid casoId, CancellationToken ct);
    Task<Peca> CriarPecaAsync(Peca peca, CancellationToken ct);
    Task<IReadOnlyList<PecaListaDto>> ListarPecasAsync(Guid casoId, CancellationToken ct);
    Task<Peca?> ObterPecaAsync(Guid id, CancellationToken ct);
    Task<bool> AtualizarPecaAsync(Peca peca, int versaoEsperada, CancellationToken ct);
    Task<bool> ExcluirPecaAsync(Guid id, int versaoEsperada, DateTime agora, CancellationToken ct);
    Task<TemplatePeca> CriarTemplateAsync(TemplatePeca template, CancellationToken ct);
    Task<IReadOnlyList<TemplatePeca>> ListarTemplatesAsync(CancellationToken ct);
    Task<TemplatePeca?> ObterTemplateAsync(Guid id, CancellationToken ct);
    Task<bool> AtualizarTemplateAsync(TemplatePeca template, CancellationToken ct);
    Task<bool> ExcluirTemplateAsync(Guid id, DateTime agora, CancellationToken ct);
}
