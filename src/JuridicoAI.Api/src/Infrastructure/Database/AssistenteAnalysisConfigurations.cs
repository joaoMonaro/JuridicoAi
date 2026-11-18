using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JuridicoAI.Api.Infrastructure.Database;

internal sealed class AssistenteAnaliseConfiguration : IEntityTypeConfiguration<AssistenteAnaliseDocumental>
{
    public void Configure(EntityTypeBuilder<AssistenteAnaliseDocumental> b)
    {
        WorkspaceConfiguration.Base(b, "ai_document_analyses");
        b.Property(x => x.Manifesto).HasColumnType("jsonb");
        b.Property(x => x.Fontes).HasColumnType("jsonb");
        b.Property(x => x.Resumo).HasColumnType("jsonb");
        b.Property(x => x.PoliticaVersao).HasMaxLength(100);
        b.Property(x => x.Modelo).HasMaxLength(100);
        b.Property(x => x.Estado).HasMaxLength(30);
        b.Property(x => x.CodigoErro).HasMaxLength(100);
        b.HasIndex(x => x.RespostaId).IsUnique();
        b.HasIndex(x => new { x.Estado, x.LeaseAte });
        b.HasIndex(x => new { x.ConversaId, x.ManifestoSha256 });
        b.HasOne<AssistenteConversa>().WithMany().HasForeignKey(x => new { x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteMensagemPersistida>().WithMany().HasForeignKey(x => new { x.PedidoId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteMensagemPersistida>().WithMany().HasForeignKey(x => new { x.RespostaId, x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class AssistenteAnaliseLoteConfiguration : IEntityTypeConfiguration<AssistenteAnaliseLote>
{
    public void Configure(EntityTypeBuilder<AssistenteAnaliseLote> b)
    {
        WorkspaceConfiguration.Base(b, "ai_document_analysis_batches");
        b.Property(x => x.Entradas).HasColumnType("jsonb");
        b.Property(x => x.Referencias).HasColumnType("jsonb");
        b.Property(x => x.Registros).HasColumnType("jsonb");
        b.Property(x => x.Estado).HasMaxLength(30);
        b.Property(x => x.Tipo).HasMaxLength(30);
        b.HasIndex(x => new { x.AnaliseId, x.Ordem }).IsUnique();
        b.HasOne<AssistenteAnaliseDocumental>().WithMany().HasForeignKey(x => new { x.AnaliseId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
