using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class CasoRepository(JuridicoAiDbContext context) : ICasoRepository
{
    public async Task<Caso> CriarCasoAsync(Caso caso, CancellationToken cancellationToken)
    {
        context.Casos.Add(caso);
        await context.SaveChangesAsync(cancellationToken);
        return caso;
    }

    public async Task<IReadOnlyList<Caso>> ListarCasosAsync(CasoStatus? status, CancellationToken cancellationToken)
    {
        var query = context.Casos.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return await query.OrderByDescending(x => x.DataAtualizacao).ToListAsync(cancellationToken);
    }

    public Task<Caso?> ObterCasoAsync(Guid id, CancellationToken cancellationToken) =>
        context.Casos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken cancellationToken) =>
        await context.Casos.Where(x => x.Id == caso.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Titulo, caso.Titulo)
            .SetProperty(x => x.Descricao, caso.Descricao)
            .SetProperty(x => x.Status, caso.Status)
            .SetProperty(x => x.DataAtualizacao, caso.DataAtualizacao), cancellationToken) == 1;

    public async Task<bool> ArquivarCasoAsync(Guid id, DateTime dataAtualizacao, CancellationToken cancellationToken) =>
        await context.Casos.Where(x => x.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Status, CasoStatus.Arquivado)
            .SetProperty(x => x.DataAtualizacao, dataAtualizacao), cancellationToken) == 1;

    public async Task<EnvolvidoCaso> CriarEnvolvidoAsync(EnvolvidoCaso envolvido, CancellationToken cancellationToken)
    {
        context.EnvolvidosCaso.Add(envolvido);
        await context.SaveChangesAsync(cancellationToken);
        return envolvido;
    }

    public async Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(Guid casoId, CancellationToken cancellationToken) =>
        await context.EnvolvidosCaso.AsNoTracking().Where(x => x.CasoId == casoId)
            .OrderBy(x => x.DataCriacao).ToListAsync(cancellationToken);

    public Task<EnvolvidoCaso?> ObterEnvolvidoAsync(Guid casoId, Guid id, CancellationToken cancellationToken) =>
        context.EnvolvidosCaso.AsNoTracking().SingleOrDefaultAsync(x => x.CasoId == casoId && x.Id == id, cancellationToken);

    public async Task<bool> AtualizarEnvolvidoAsync(EnvolvidoCaso envolvido, CancellationToken cancellationToken) =>
        await context.EnvolvidosCaso.Where(x => x.CasoId == envolvido.CasoId && x.Id == envolvido.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Nome, envolvido.Nome)
                .SetProperty(x => x.Tipo, envolvido.Tipo)
                .SetProperty(x => x.Documento, envolvido.Documento)
                .SetProperty(x => x.DadosAdicionais, envolvido.DadosAdicionais)
                .SetProperty(x => x.DataAtualizacao, envolvido.DataAtualizacao), cancellationToken) == 1;

    public Task<bool> RemoverEnvolvidoAsync(Guid casoId, Guid id, CancellationToken cancellationToken) =>
        DeleteAsync(context.EnvolvidosCaso.Where(x => x.CasoId == casoId && x.Id == id), cancellationToken);

    public async Task<ContextoCaso> CriarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken)
    {
        context.ContextosCaso.Add(contexto);
        await context.SaveChangesAsync(cancellationToken);
        return contexto;
    }

    public async Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(Guid casoId, CancellationToken cancellationToken) =>
        await context.ContextosCaso.AsNoTracking().Where(x => x.CasoId == casoId)
            .OrderBy(x => x.DataCriacao).ToListAsync(cancellationToken);

    public Task<ContextoCaso?> ObterContextoAsync(Guid casoId, Guid id, CancellationToken cancellationToken) =>
        context.ContextosCaso.AsNoTracking().SingleOrDefaultAsync(x => x.CasoId == casoId && x.Id == id, cancellationToken);

    public async Task<bool> AtualizarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken) =>
        await context.ContextosCaso.Where(x => x.CasoId == contexto.CasoId && x.Id == contexto.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Tipo, contexto.Tipo)
                .SetProperty(x => x.Titulo, contexto.Titulo)
                .SetProperty(x => x.Conteudo, contexto.Conteudo)
                .SetProperty(x => x.DataAtualizacao, contexto.DataAtualizacao), cancellationToken) == 1;

    public Task<bool> RemoverContextoAsync(Guid casoId, Guid id, CancellationToken cancellationToken) =>
        DeleteAsync(context.ContextosCaso.Where(x => x.CasoId == casoId && x.Id == id), cancellationToken);

    public async Task<DocumentoCaso> CriarDocumentoAsync(DocumentoCaso documento, CancellationToken cancellationToken)
    {
        context.DocumentosCaso.Add(documento);
        await context.SaveChangesAsync(cancellationToken);
        return documento;
    }

    public async Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(Guid casoId, CancellationToken cancellationToken) =>
        await context.DocumentosCaso.AsNoTracking().Where(x => x.CasoId == casoId)
            .OrderBy(x => x.DataUpload).ToListAsync(cancellationToken);

    public Task<DocumentoCaso?> ObterDocumentoAsync(Guid casoId, Guid id, CancellationToken cancellationToken) =>
        context.DocumentosCaso.AsNoTracking().SingleOrDefaultAsync(x => x.CasoId == casoId && x.Id == id, cancellationToken);

    public Task<bool> RemoverDocumentoAsync(Guid casoId, Guid id, CancellationToken cancellationToken) =>
        DeleteAsync(context.DocumentosCaso.Where(x => x.CasoId == casoId && x.Id == id), cancellationToken);

    private static async Task<bool> DeleteAsync<TEntity>(IQueryable<TEntity> query, CancellationToken cancellationToken)
        where TEntity : class => await query.ExecuteDeleteAsync(cancellationToken) == 1;
}
