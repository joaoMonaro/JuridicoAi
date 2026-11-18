using System.Text.RegularExpressions;
using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JuridicoAI.Api.Infrastructure.Database;

internal static class WorkspaceConfiguration
{
    public static void Base<T>(EntityTypeBuilder<T> b, string table) where T : class
    {
        b.ToTable(table);
        b.HasKey("Id");
        b.Property<Guid>("Id").ValueGeneratedNever();
        b.HasAlternateKey("Id", "OrganizationId");
        b.HasOne<Organization>().WithMany().HasForeignKey("OrganizationId").OnDelete(DeleteBehavior.Restrict);
        foreach (var property in b.Metadata.GetProperties())
        {
            property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
            if (property.ClrType == typeof(string) && property.Name.EndsWith("Sha256")) property.SetMaxLength(64);
        }
    }
}

internal sealed class AssistenteConversaConfiguration : IEntityTypeConfiguration<AssistenteConversa>
{
    public void Configure(EntityTypeBuilder<AssistenteConversa> b)
    {
        WorkspaceConfiguration.Base(b, "ai_conversations");
        b.Property(x => x.Titulo).HasMaxLength(200);
        b.Property(x => x.Estado).HasMaxLength(20).HasDefaultValue("Ativa");
        b.HasQueryFilter(x => x.Estado == "Ativa");
        b.HasIndex(x => new { x.UserId, x.AtualizadaEm, x.Id });
        b.HasIndex(x => new { x.OrganizationId, x.UserId, x.PecaId }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Peca>().WithMany().HasForeignKey(x => new { x.PecaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class AssistenteMensagemConfiguration : IEntityTypeConfiguration<AssistenteMensagemPersistida>
{
    public void Configure(EntityTypeBuilder<AssistenteMensagemPersistida> b)
    {
        WorkspaceConfiguration.Base(b, "ai_messages");
        b.Property(x => x.Autor).HasMaxLength(20);
        b.Property(x => x.Estado).HasMaxLength(20).HasDefaultValue("Concluida");
        b.Property(x => x.CodigoErro).HasMaxLength(80);
        b.Property(x => x.Solicitacao).HasColumnType("jsonb");
        b.Property(x => x.Resultado).HasColumnType("jsonb");
        b.Property(x => x.FontesDocumentais).HasColumnType("jsonb");
        b.Property(x => x.CoberturaDocumental).HasColumnType("jsonb");
        b.Property(x => x.ContextoHistorico).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ConversaId, x.ClientRequestId }).IsUnique();
        b.HasIndex(x => x.ConversaId).IsUnique().HasFilter("estado IN ('Gerando', 'AguardandoAnalise', 'AguardandoGeracao')");
        b.HasIndex(x => new { x.ConversaId, x.RespostaAId }).IsUnique();
        b.HasIndex(x => new { x.ConversaId, x.Sequencia }).IsUnique();
        b.HasOne<AssistenteConversa>().WithMany().HasForeignKey(x => new { x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistentePropostaPersistida>().WithMany().HasForeignKey(x => new { x.PropostaId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteGeracao>().WithMany().HasForeignKey(x => new { x.GeracaoId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteAnaliseDocumental>().WithMany().HasForeignKey(x => new { x.AnaliseDocumentalId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteMensagemPersistida>().WithMany().HasForeignKey(x => new { x.RespostaAId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class AssistentePropostaConfiguration : IEntityTypeConfiguration<AssistentePropostaPersistida>
{
    public void Configure(EntityTypeBuilder<AssistentePropostaPersistida> b)
    {
        WorkspaceConfiguration.Base(b, "ai_proposals");
        b.Property(x => x.DocumentoBase).HasColumnType("jsonb");
        b.Property(x => x.Grupos).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ConversaId, x.ClientRequestId }).IsUnique();
        b.HasOne<AssistenteConversa>().WithMany().HasForeignKey(x => new { x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistentePropostaPersistida>().WithMany().HasForeignKey(x => new { x.SubstituidaPorId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class AssistenteReciboConfiguration : IEntityTypeConfiguration<AssistenteAplicacaoRecibo>
{
    public void Configure(EntityTypeBuilder<AssistenteAplicacaoRecibo> b)
    {
        WorkspaceConfiguration.Base(b, "ai_application_receipts");
        b.Property(x => x.ConteudoJson).HasColumnType("jsonb");
        b.Property(x => x.GruposAplicados).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ConversaId, x.RequestId }).IsUnique();
        b.HasOne<AssistentePropostaPersistida>().WithMany().HasForeignKey(x => new { x.PropostaId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteConversa>().WithMany().HasForeignKey(x => new { x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AssistenteMemoriaConfiguration : IEntityTypeConfiguration<AssistenteMemoria>
{
    public void Configure(EntityTypeBuilder<AssistenteMemoria> b)
    {
        WorkspaceConfiguration.Base(b, "ai_memories");
        b.Property(x => x.PoliticaVersao).HasMaxLength(100);
        b.Property(x => x.Registros).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ConversaId, x.Versao }).IsUnique();
        b.HasIndex(x => x.RespostaId).IsUnique();
        b.HasOne<AssistenteConversa>().WithMany().HasForeignKey(x => new { x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteMensagemPersistida>().WithMany().HasForeignKey(x => new { x.RespostaId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
