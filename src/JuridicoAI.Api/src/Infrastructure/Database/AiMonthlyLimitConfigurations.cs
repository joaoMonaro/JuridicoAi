using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JuridicoAI.Api.Infrastructure.Database;

internal sealed class AiOrganizationMonthlyLimitConfiguration : IEntityTypeConfiguration<AiOrganizationMonthlyLimit>
{
    public void Configure(EntityTypeBuilder<AiOrganizationMonthlyLimit> builder)
    {
        builder.ToTable("ai_organization_monthly_limits", table =>
        {
            table.HasCheckConstraint("ck_ai_org_monthly_limits_amount", "monthly_limit_usd >= 0");
            table.HasCheckConstraint("ck_ai_org_monthly_limits_reason", "length(btrim(reason)) > 0");
        });
        builder.HasKey(x => x.OrganizationId).HasName("ai_organization_monthly_limits_pkey");
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id").ValueGeneratedNever();
        builder.Property(x => x.MonthlyLimitUsd).HasColumnName("monthly_limit_usd").HasPrecision(18, 8);
        builder.Property(x => x.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AiMonthlyAccountConfiguration : IEntityTypeConfiguration<AiMonthlyAccount>
{
    public void Configure(EntityTypeBuilder<AiMonthlyAccount> builder)
    {
        builder.ToTable("ai_monthly_accounts", table =>
        {
            table.HasCheckConstraint("ck_ai_monthly_accounts_year", "period_year >= 2020 AND period_year <= 2100");
            table.HasCheckConstraint("ck_ai_monthly_accounts_month", "period_month >= 1 AND period_month <= 12");
            table.HasCheckConstraint("ck_ai_monthly_accounts_limit", "limit_usd >= 0");
            table.HasCheckConstraint("ck_ai_monthly_accounts_consumed", "consumed_usd >= 0");
            table.HasCheckConstraint("ck_ai_monthly_accounts_reserved", "reserved_usd >= 0");
        });
        builder.HasKey(x => x.Id).HasName("ai_monthly_accounts_pkey");
        builder.HasAlternateKey(x => new { x.Id, x.OrganizationId }).HasName("ak_ai_monthly_accounts_id_org");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id");
        builder.Property(x => x.PeriodYear).HasColumnName("period_year");
        builder.Property(x => x.PeriodMonth).HasColumnName("period_month");
        builder.Property(x => x.LimitUsd).HasColumnName("limit_usd").HasPrecision(18, 8);
        builder.Property(x => x.ConsumedUsd).HasColumnName("consumed_usd").HasPrecision(18, 8);
        builder.Property(x => x.ReservedUsd).HasColumnName("reserved_usd").HasPrecision(18, 8);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OrganizationId, x.PeriodYear, x.PeriodMonth })
            .IsUnique()
            .HasDatabaseName("uq_ai_monthly_accounts_org_period");
    }
}

internal sealed class AiUsageReservationConfiguration : IEntityTypeConfiguration<AiUsageReservation>
{
    public void Configure(EntityTypeBuilder<AiUsageReservation> builder)
    {
        builder.ToTable("ai_usage_reservations", table =>
        {
            table.HasCheckConstraint("ck_ai_usage_reservations_attempt", "attempt_count > 0");
            table.HasCheckConstraint("ck_ai_usage_reservations_year", "period_year >= 2020 AND period_year <= 2100");
            table.HasCheckConstraint("ck_ai_usage_reservations_month", "period_month >= 1 AND period_month <= 12");
            table.HasCheckConstraint("ck_ai_usage_reservations_prices", "input_cost_per_million_usd >= 0 AND cached_input_cost_per_million_usd >= 0 AND output_cost_per_million_usd >= 0 AND cache_write_cost_per_million_usd >= 0");
            table.HasCheckConstraint("ck_ai_usage_reservations_tokens", "estimated_input_tokens >= 0 AND max_output_tokens >= 0");
            table.HasCheckConstraint("ck_ai_usage_reservations_reserved", "reserved_usd >= 0");
            table.HasCheckConstraint("ck_ai_usage_reservations_settled", "settled_usd IS NULL OR settled_usd >= 0");
            table.HasCheckConstraint("ck_ai_usage_reservations_status", "status IN ('Reserved', 'Settled', 'Released', 'Indeterminate')");
        });
        builder.HasKey(x => x.Id).HasName("ai_usage_reservations_pkey");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.ClientRequestId).HasColumnName("client_request_id");
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        builder.Property(x => x.PeriodYear).HasColumnName("period_year");
        builder.Property(x => x.PeriodMonth).HasColumnName("period_month");
        builder.Property(x => x.OperationType).HasColumnName("operation_type").HasMaxLength(60);
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
        builder.Property(x => x.PricingVersion).HasColumnName("pricing_version").HasMaxLength(40).IsRequired();
        builder.Property(x => x.InputCostPerMillionUsd).HasColumnName("input_cost_per_million_usd").HasPrecision(18, 8);
        builder.Property(x => x.CachedInputCostPerMillionUsd).HasColumnName("cached_input_cost_per_million_usd").HasPrecision(18, 8);
        builder.Property(x => x.CacheWriteCostPerMillionUsd).HasColumnName("cache_write_cost_per_million_usd").HasPrecision(18, 8);
        builder.Property(x => x.OutputCostPerMillionUsd).HasColumnName("output_cost_per_million_usd").HasPrecision(18, 8);
        builder.Property(x => x.EstimatedInputTokens).HasColumnName("estimated_input_tokens");
        builder.Property(x => x.MaxOutputTokens).HasColumnName("max_output_tokens");
        builder.Property(x => x.ReservedUsd).HasColumnName("reserved_usd").HasPrecision(18, 8);
        builder.Property(x => x.SettledUsd).HasColumnName("settled_usd").HasPrecision(18, 8);
        builder.Property(x => x.ActualInputTokens).HasColumnName("actual_input_tokens");
        builder.Property(x => x.ActualCachedInputTokens).HasColumnName("actual_cached_input_tokens");
        builder.Property(x => x.ActualCacheWriteTokens).HasColumnName("actual_cache_write_tokens");
        builder.Property(x => x.ActualOutputTokens).HasColumnName("actual_output_tokens");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(80);
        builder.Property(x => x.ReconciledById).HasColumnName("reconciled_by_id");
        builder.Property(x => x.ReconciliationReason).HasColumnName("reconciliation_reason").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.SettledAt).HasColumnName("settled_at");

        builder.HasOne<AiMonthlyAccount>().WithMany()
            .HasForeignKey(x => new { x.AccountId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReconciledById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SolicitacaoId, x.AttemptCount })
            .IsUnique()
            .HasDatabaseName("uq_ai_usage_reservations_solicitacao_attempt");
        builder.HasIndex(x => new { x.OrganizationId, x.PeriodYear, x.PeriodMonth, x.Status })
            .HasDatabaseName("ix_ai_usage_reservations_org_period_status");
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_ai_usage_reservations_status_created_at");
    }
}

internal sealed class AiFinancialAuditLogConfiguration : IEntityTypeConfiguration<AiFinancialAuditLog>
{
    public void Configure(EntityTypeBuilder<AiFinancialAuditLog> builder)
    {
        builder.ToTable("ai_financial_audit_logs", table =>
        {
            table.HasCheckConstraint("ck_ai_financial_audit_action", "action IN ('SetMonthlyLimit', 'ReconcileSettle', 'ReconcileRelease', 'MarkIndeterminate')");
            table.HasCheckConstraint("ck_ai_financial_audit_reason", "length(btrim(reason)) > 0");
        });
        builder.HasKey(x => x.Id).HasName("ai_financial_audit_logs_pkey");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReservationId).HasColumnName("reservation_id");
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(x => x.PreviousLimitUsd).HasColumnName("previous_limit_usd").HasPrecision(18, 8);
        builder.Property(x => x.NewLimitUsd).HasColumnName("new_limit_usd").HasPrecision(18, 8);
        builder.Property(x => x.SettledUsd).HasColumnName("settled_usd").HasPrecision(18, 8);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiUsageReservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAt }).HasDatabaseName("ix_ai_financial_audit_org_created_at");
    }
}
