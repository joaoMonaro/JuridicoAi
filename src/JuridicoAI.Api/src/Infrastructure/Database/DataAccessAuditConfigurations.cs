using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JuridicoAI.Api.Infrastructure.Database;

internal sealed class DataAccessAuditLogConfiguration : IEntityTypeConfiguration<DataAccessAuditLog>
{
    public void Configure(EntityTypeBuilder<DataAccessAuditLog> builder)
    {
        builder.ToTable("data_access_audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(80).IsRequired();
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(60).IsRequired();
        builder.Property(x => x.ResourceId).HasColumnName("resource_id").IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(x => new { x.OrganizationId, x.OccurredAt })
            .HasDatabaseName("ix_data_access_audit_logs_org_occurred_at");
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId })
            .HasDatabaseName("ix_data_access_audit_logs_resource");
        builder.HasIndex(x => x.OccurredAt)
            .HasDatabaseName("ix_data_access_audit_logs_occurred_at");
    }
}
