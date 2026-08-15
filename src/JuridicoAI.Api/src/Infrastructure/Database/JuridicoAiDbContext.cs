using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Database;

public sealed class JuridicoAiDbContext(DbContextOptions<JuridicoAiDbContext> options)
    : DbContext(options)
{
    public DbSet<Caso> Casos => Set<Caso>();
    public DbSet<EnvolvidoCaso> EnvolvidosCaso => Set<EnvolvidoCaso>();
    public DbSet<ContextoCaso> ContextosCaso => Set<ContextoCaso>();
    public DbSet<DocumentoCaso> DocumentosCaso => Set<DocumentoCaso>();
    public DbSet<Peca> Pecas => Set<Peca>();
    public DbSet<TemplatePeca> TemplatesPeca => Set<TemplatePeca>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JuridicoAiDbContext).Assembly);
}
