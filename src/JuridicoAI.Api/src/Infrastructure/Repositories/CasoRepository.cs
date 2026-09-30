using System.Data;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
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

    public async Task<IReadOnlyList<Caso>> ListarCasosAsync(Guid organizationId, CasoStatusEnum? status, CancellationToken cancellationToken)
    {
        var query = context.Casos.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return await query.OrderByDescending(x => x.DataAtualizacao).ToListAsync(cancellationToken);
    }

    public Task<Caso?> ObterCasoAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        context.Casos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, cancellationToken);

    public async Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken cancellationToken) =>
        await context.Casos.Where(x => x.Id == caso.Id && x.OrganizationId == caso.OrganizationId).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Titulo, caso.Titulo)
            .SetProperty(x => x.Descricao, caso.Descricao)
            .SetProperty(x => x.Status, caso.Status)
            .SetProperty(x => x.UpdatedById, caso.UpdatedById)
            .SetProperty(x => x.DataAtualizacao, caso.DataAtualizacao), cancellationToken) == 1;

    public async Task<bool> ArquivarCasoAsync(Guid id, Guid organizationId, Guid updatedById, DateTime dataAtualizacao, CancellationToken cancellationToken) =>
        await context.Casos.Where(x => x.Id == id && x.OrganizationId == organizationId).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Status, CasoStatusEnum.Arquivado)
            .SetProperty(x => x.UpdatedById, updatedById)
            .SetProperty(x => x.DataAtualizacao, dataAtualizacao), cancellationToken) == 1;

    public async Task<EnvolvidoCaso> CriarEnvolvidoAsync(EnvolvidoCaso envolvido, CancellationToken cancellationToken)
    {
        context.EnvolvidosCaso.Add(envolvido);
        await context.SaveChangesAsync(cancellationToken);
        return envolvido;
    }

    public async Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken) =>
        await context.EnvolvidosCaso.AsNoTracking().Where(x => x.CasoId == casoId && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId))
            .OrderBy(x => x.DataCriacao).ToListAsync(cancellationToken);

    public Task<EnvolvidoCaso?> ObterEnvolvidoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        context.EnvolvidosCaso.AsNoTracking().SingleOrDefaultAsync(x => x.CasoId == casoId && x.Id == id && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId), cancellationToken);

    public async Task<bool> AtualizarEnvolvidoAsync(EnvolvidoCaso envolvido, Guid organizationId, CancellationToken cancellationToken) =>
        await context.EnvolvidosCaso.Where(x => x.CasoId == envolvido.CasoId && x.Id == envolvido.Id &&
                                                context.Casos.Any(c => c.Id == envolvido.CasoId && c.OrganizationId == organizationId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Nome, envolvido.Nome)
                .SetProperty(x => x.Tipo, envolvido.Tipo)
                .SetProperty(x => x.Natureza, envolvido.Natureza)
                .SetProperty(x => x.NomeFantasia, envolvido.NomeFantasia)
                .SetProperty(x => x.CpfCnpj, envolvido.CpfCnpj)
                .SetProperty(x => x.Rg, envolvido.Rg)
                .SetProperty(x => x.RgOrgaoEmissor, envolvido.RgOrgaoEmissor)
                .SetProperty(x => x.RgUf, envolvido.RgUf)
                .SetProperty(x => x.Nacionalidade, envolvido.Nacionalidade)
                .SetProperty(x => x.EstadoCivil, envolvido.EstadoCivil)
                .SetProperty(x => x.Profissao, envolvido.Profissao)
                .SetProperty(x => x.DataNascimento, envolvido.DataNascimento)
                .SetProperty(x => x.InscricaoEstadual, envolvido.InscricaoEstadual)
                .SetProperty(x => x.Email, envolvido.Email)
                .SetProperty(x => x.Telefone, envolvido.Telefone)
                .SetProperty(x => x.DataAtualizacao, envolvido.DataAtualizacao), cancellationToken) == 1;

    public Task<bool> RemoverEnvolvidoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        DeleteAsync(context.EnvolvidosCaso.Where(x => x.CasoId == casoId && x.Id == id && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId)), cancellationToken);

    public async Task<EnderecoEnvolvidoCaso> CriarEnderecoAsync(
        Guid casoId,
        Guid organizationId,
        EnderecoEnvolvidoCaso endereco,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var envolvidoExiste = await context.EnvolvidosCaso.AnyAsync(x => x.Id == endereco.EnvolvidoCasoId &&
            x.CasoId == casoId && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId), cancellationToken);
        if (!envolvidoExiste) throw new InvalidOperationException("Envolvido nao encontrado durante a criacao do endereco.");

        if (endereco.Principal)
            await DesmarcarPrincipalAsync(endereco.EnvolvidoCasoId, endereco.Tipo, null, endereco.DataAtualizacao, cancellationToken);

        context.EnderecosEnvolvidosCaso.Add(endereco);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return endereco;
    }

    public async Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosAsync(
        Guid casoId, Guid envolvidoId, Guid organizationId, CancellationToken cancellationToken) =>
        await EnderecosEscopados(casoId, envolvidoId, organizationId)
            .OrderByDescending(x => x.Principal).ThenBy(x => x.Tipo).ThenBy(x => x.DataCriacao)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EnderecoEnvolvidoCaso>> ListarEnderecosCasoAsync(
        Guid casoId, Guid organizationId, CancellationToken cancellationToken) =>
        await context.EnderecosEnvolvidosCaso.AsNoTracking()
            .Where(x => context.EnvolvidosCaso.Any(e => e.Id == x.EnvolvidoCasoId && e.CasoId == casoId) &&
                        context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId))
            .OrderBy(x => x.EnvolvidoCasoId).ThenByDescending(x => x.Principal).ThenBy(x => x.Tipo)
            .ToListAsync(cancellationToken);

    public Task<EnderecoEnvolvidoCaso?> ObterEnderecoAsync(
        Guid casoId, Guid envolvidoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        EnderecosEscopados(casoId, envolvidoId, organizationId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> AtualizarEnderecoAsync(
        Guid casoId,
        Guid organizationId,
        EnderecoEnvolvidoCaso endereco,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var enderecoExiste = await EnderecosEscopados(casoId, endereco.EnvolvidoCasoId, organizationId)
            .AnyAsync(x => x.Id == endereco.Id, cancellationToken);
        if (!enderecoExiste) return false;

        if (endereco.Principal)
            await DesmarcarPrincipalAsync(endereco.EnvolvidoCasoId, endereco.Tipo, endereco.Id, endereco.DataAtualizacao, cancellationToken);

        var atualizado = await EnderecosEscopados(casoId, endereco.EnvolvidoCasoId, organizationId)
            .Where(x => x.Id == endereco.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Tipo, endereco.Tipo)
                .SetProperty(x => x.Principal, endereco.Principal)
                .SetProperty(x => x.Cep, endereco.Cep)
                .SetProperty(x => x.Logradouro, endereco.Logradouro)
                .SetProperty(x => x.Numero, endereco.Numero)
                .SetProperty(x => x.Complemento, endereco.Complemento)
                .SetProperty(x => x.Bairro, endereco.Bairro)
                .SetProperty(x => x.Cidade, endereco.Cidade)
                .SetProperty(x => x.Uf, endereco.Uf)
                .SetProperty(x => x.EstadoProvincia, endereco.EstadoProvincia)
                .SetProperty(x => x.Pais, endereco.Pais)
                .SetProperty(x => x.DataAtualizacao, endereco.DataAtualizacao), cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return atualizado;
    }

    public Task<bool> RemoverEnderecoAsync(
        Guid casoId, Guid envolvidoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        DeleteAsync(EnderecosEscopados(casoId, envolvidoId, organizationId).Where(x => x.Id == id), cancellationToken);

    public async Task<ContextoCaso> CriarContextoAsync(ContextoCaso contexto, CancellationToken cancellationToken)
    {
        context.ContextosCaso.Add(contexto);
        await context.SaveChangesAsync(cancellationToken);
        return contexto;
    }

    public async Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken) =>
        await context.ContextosCaso.AsNoTracking().Where(x => x.CasoId == casoId && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId))
            .OrderBy(x => x.DataCriacao).ToListAsync(cancellationToken);

    public Task<ContextoCaso?> ObterContextoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        context.ContextosCaso.AsNoTracking().SingleOrDefaultAsync(x => x.CasoId == casoId && x.Id == id && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId), cancellationToken);

    public async Task<bool> AtualizarContextoAsync(
        ContextoCaso contexto,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await context.ContextosCaso.Where(x => x.CasoId == contexto.CasoId && x.Id == contexto.Id &&
            context.Casos.Any(c => c.Id == contexto.CasoId && c.OrganizationId == organizationId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Tipo, contexto.Tipo)
                .SetProperty(x => x.Titulo, contexto.Titulo)
                .SetProperty(x => x.Conteudo, contexto.Conteudo)
                .SetProperty(x => x.DataAtualizacao, contexto.DataAtualizacao), cancellationToken) == 1;

    public Task<bool> RemoverContextoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        DeleteAsync(context.ContextosCaso.Where(x => x.CasoId == casoId && x.Id == id && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId)), cancellationToken);

    public async Task<DocumentoCaso> CriarDocumentoAsync(DocumentoCaso documento, CancellationToken cancellationToken)
    {
        context.DocumentosCaso.Add(documento);
        await context.SaveChangesAsync(cancellationToken);
        return documento;
    }

    public async Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(Guid casoId, Guid organizationId, CancellationToken cancellationToken) =>
        await context.DocumentosCaso.AsNoTracking().Where(x => x.CasoId == casoId && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId))
            .OrderBy(x => x.DataUpload).ToListAsync(cancellationToken);

    public Task<DocumentoCaso?> ObterDocumentoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        context.DocumentosCaso.AsNoTracking().SingleOrDefaultAsync(x => x.CasoId == casoId && x.Id == id && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId), cancellationToken);

    public Task<bool> RemoverDocumentoAsync(Guid casoId, Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        DeleteAsync(context.DocumentosCaso.Where(x => x.CasoId == casoId && x.Id == id && context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId)), cancellationToken);

    private IQueryable<EnderecoEnvolvidoCaso> EnderecosEscopados(Guid casoId, Guid envolvidoId, Guid organizationId) =>
        context.EnderecosEnvolvidosCaso.AsNoTracking().Where(x => x.EnvolvidoCasoId == envolvidoId &&
            context.EnvolvidosCaso.Any(e => e.Id == envolvidoId && e.CasoId == casoId) &&
            context.Casos.Any(c => c.Id == casoId && c.OrganizationId == organizationId));

    private Task<int> DesmarcarPrincipalAsync(
        Guid envolvidoId,
        TipoEnderecoEnum tipo,
        Guid? ignorarId,
        DateTime dataAtualizacao,
        CancellationToken cancellationToken) =>
        context.EnderecosEnvolvidosCaso
            .Where(x => x.EnvolvidoCasoId == envolvidoId && x.Tipo == tipo && x.Principal &&
                        (!ignorarId.HasValue || x.Id != ignorarId.Value))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Principal, false)
                .SetProperty(x => x.DataAtualizacao, dataAtualizacao), cancellationToken);

    private static async Task<bool> DeleteAsync<TEntity>(IQueryable<TEntity> query, CancellationToken cancellationToken)
        where TEntity : class => await query.ExecuteDeleteAsync(cancellationToken) == 1;
}
