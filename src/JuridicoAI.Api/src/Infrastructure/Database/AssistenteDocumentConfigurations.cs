using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JuridicoAI.Api.Infrastructure.Database;

internal sealed class AssistenteDocumentoConfiguration : IEntityTypeConfiguration<AssistenteDocumento>
{
    public void Configure(EntityTypeBuilder<AssistenteDocumento> b)
    {
        WorkspaceConfiguration.Base(b, "ai_documents");
        b.ToTable("ai_documents", t => t.HasCheckConstraint("ck_ai_document_origin",
            "(conversa_id IS NOT NULL AND documento_caso_id IS NULL AND client_upload_id IS NOT NULL) OR (conversa_id IS NULL AND documento_caso_id IS NOT NULL AND client_upload_id IS NULL)"));
        b.Property(x => x.Nome).HasMaxLength(255);
        b.Property(x => x.StorageId).HasMaxLength(512);
        b.Property(x => x.Formato).HasMaxLength(10);
        b.Property(x => x.Estado).HasMaxLength(20);
        b.Property(x => x.CodigoErro).HasMaxLength(80);
        b.HasIndex(x => new { x.ConversaId, x.ClientUploadId }).IsUnique();
        b.HasIndex(x => new { x.DocumentoCasoId, x.StorageId }).IsUnique();
        b.HasIndex(x => new { x.Estado, x.LeaseAte });
        b.HasOne<AssistenteConversa>().WithMany().HasForeignKey(x => new { x.ConversaId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Caso>().WithMany().HasForeignKey(x => new { x.CasoId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        // No FK to DocumentoCaso: deleting a shared source revokes retrieval,
        // without blocking that deletion or deleting retained conversation data.
    }
}
internal sealed class AssistenteDocumentoTrechoConfiguration : IEntityTypeConfiguration<AssistenteDocumentoTrecho>
{
    public void Configure(EntityTypeBuilder<AssistenteDocumentoTrecho> b)
    {
        WorkspaceConfiguration.Base(b, "ai_document_chunks");
        b.Property(x => x.Metricas).HasColumnType("jsonb");
        b.Property(x => x.Qualidade).HasMaxLength(20);
        b.Property(x => x.Motivo).HasMaxLength(80);
        b.Property(x => x.Localizador).HasMaxLength(300);
        b.Property(x => x.Busca).HasComputedColumnSql("to_tsvector('portuguese', CASE WHEN qualidade = 'Confiavel' THEN texto ELSE '' END)", stored: true);
        b.HasIndex(x => x.Busca).HasMethod("GIN");
        b.HasIndex(x => new { x.DocumentoId, x.Ordem }).IsUnique();
        b.HasOne<AssistenteDocumento>().WithMany().HasForeignKey(x => new { x.DocumentoId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
