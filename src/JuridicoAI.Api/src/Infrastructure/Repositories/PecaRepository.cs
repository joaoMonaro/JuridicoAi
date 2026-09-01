using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class PecaRepository(JuridicoAiDbContext context) : IPecaRepository
{
    public Task<bool> CasoExisteAsync(Guid casoId, Guid organizationId, CancellationToken ct) => context.Casos.AnyAsync(x => x.Id == casoId && x.OrganizationId == organizationId, ct);

    public async Task<Peca> CriarPecaAsync(Peca peca, CancellationToken ct)
    { context.Pecas.Add(peca); await context.SaveChangesAsync(ct); return peca; }

    public async Task<IReadOnlyList<PecaListaDto>> ListarPecasAsync(Guid casoId, Guid organizationId, CancellationToken ct) =>
        await context.Pecas.AsNoTracking().Where(x => x.CasoId == casoId && x.OrganizationId == organizationId && x.DataExclusao == null)
            .OrderByDescending(x => x.DataAtualizacao)
            .Select(x => new PecaListaDto(x.Id, x.Titulo, x.Status, x.Versao, x.TemplateId, x.DataAtualizacao))
            .ToListAsync(ct);

    public Task<Peca?> ObterPecaAsync(Guid id, Guid organizationId, CancellationToken ct) => context.Pecas.AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.DataExclusao == null, ct);

    public async Task<bool> AtualizarPecaAsync(Peca peca, int versaoEsperada, CancellationToken ct) =>
        await context.Pecas.Where(x => x.Id == peca.Id && x.OrganizationId == peca.OrganizationId && x.DataExclusao == null && x.Versao == versaoEsperada)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Titulo, peca.Titulo).SetProperty(x => x.ConteudoJson, peca.ConteudoJson)
                .SetProperty(x => x.PageConfig, peca.PageConfig).SetProperty(x => x.HeaderJson, peca.HeaderJson)
                .SetProperty(x => x.FooterJson, peca.FooterJson).SetProperty(x => x.Styles, peca.Styles)
                .SetProperty(x => x.Status, peca.Status).SetProperty(x => x.Versao, x => x.Versao + 1)
                .SetProperty(x => x.UpdatedById, peca.UpdatedById)
                .SetProperty(x => x.DataAtualizacao, peca.DataAtualizacao), ct) == 1;

    public async Task<bool> ExcluirPecaAsync(Guid id, Guid organizationId, Guid updatedById, int versaoEsperada, DateTime agora, CancellationToken ct) =>
        await context.Pecas.Where(x => x.Id == id && x.OrganizationId == organizationId && x.DataExclusao == null && x.Versao == versaoEsperada)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DataExclusao, agora)
                .SetProperty(x => x.UpdatedById, updatedById)
                .SetProperty(x => x.DataAtualizacao, agora).SetProperty(x => x.Versao, x => x.Versao + 1), ct) == 1;

    public async Task<TemplatePeca> CriarTemplateAsync(TemplatePeca template, CancellationToken ct)
    { context.TemplatesPeca.Add(template); await context.SaveChangesAsync(ct); return template; }

    public async Task<IReadOnlyList<TemplatePeca>> ListarTemplatesAsync(Guid organizationId, CancellationToken ct) =>
        await context.TemplatesPeca.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.DataExclusao == null)
            .OrderBy(x => x.Nome).ToListAsync(ct);

    public Task<TemplatePeca?> ObterTemplateAsync(Guid id, Guid organizationId, CancellationToken ct) => context.TemplatesPeca.AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.DataExclusao == null, ct);

    public async Task<bool> AtualizarTemplateAsync(TemplatePeca item, CancellationToken ct) =>
        await context.TemplatesPeca.Where(x => x.Id == item.Id && x.OrganizationId == item.OrganizationId && x.DataExclusao == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Nome, item.Nome)
                .SetProperty(x => x.ConteudoJson, item.ConteudoJson).SetProperty(x => x.PageConfig, item.PageConfig)
                .SetProperty(x => x.HeaderJson, item.HeaderJson).SetProperty(x => x.FooterJson, item.FooterJson)
                .SetProperty(x => x.Styles, item.Styles).SetProperty(x => x.UpdatedById, item.UpdatedById)
                .SetProperty(x => x.DataAtualizacao, item.DataAtualizacao), ct) == 1;

    public async Task<bool> ExcluirTemplateAsync(Guid id, Guid organizationId, Guid updatedById, DateTime agora, CancellationToken ct) =>
        await context.TemplatesPeca.Where(x => x.Id == id && x.OrganizationId == organizationId && x.DataExclusao == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DataExclusao, agora).SetProperty(x => x.UpdatedById, updatedById)
                .SetProperty(x => x.DataAtualizacao, agora), ct) == 1;
}
