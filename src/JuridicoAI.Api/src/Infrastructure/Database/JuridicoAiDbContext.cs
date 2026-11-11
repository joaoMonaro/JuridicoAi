using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Database;

public sealed class JuridicoAiDbContext(DbContextOptions<JuridicoAiDbContext> options)
    : DbContext(options)
{
    public DbSet<Caso> Casos => Set<Caso>();
    public DbSet<EnvolvidoCaso> EnvolvidosCaso => Set<EnvolvidoCaso>();
    public DbSet<EnderecoEnvolvidoCaso> EnderecosEnvolvidosCaso => Set<EnderecoEnvolvidoCaso>();
    public DbSet<ContextoCaso> ContextosCaso => Set<ContextoCaso>();
    public DbSet<DocumentoCaso> DocumentosCaso => Set<DocumentoCaso>();
    public DbSet<Peca> Pecas => Set<Peca>();
    public DbSet<AssistenteConversa> AssistenteConversas => Set<AssistenteConversa>();
    public DbSet<AssistenteDocumento> AssistenteDocumentos => Set<AssistenteDocumento>();
    public DbSet<AssistenteDocumentoTrecho> AssistenteDocumentoTrechos => Set<AssistenteDocumentoTrecho>();
    public DbSet<AssistenteMensagemPersistida> AssistenteMensagens => Set<AssistenteMensagemPersistida>();
    public DbSet<AssistentePropostaPersistida> AssistentePropostas => Set<AssistentePropostaPersistida>();
    public DbSet<AssistenteAplicacaoRecibo> AssistenteRecibos => Set<AssistenteAplicacaoRecibo>();
    public DbSet<TemplatePeca> TemplatesPeca => Set<TemplatePeca>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<OrganizationFeature> OrganizationFeatures => Set<OrganizationFeature>();
    public DbSet<AiOperationalControl> AiOperationalControls => Set<AiOperationalControl>();
    public DbSet<AiFeatureChangeAudit> AiFeatureChangeAudit => Set<AiFeatureChangeAudit>();
    public DbSet<AiUsageLedgerEntry> AiUsageLedger => Set<AiUsageLedgerEntry>();
    public DbSet<AiTransparencyAcknowledgement> AiTransparencyAcknowledgements => Set<AiTransparencyAcknowledgement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JuridicoAiDbContext).Assembly);
}
