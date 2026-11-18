using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiMonthlyFinancialControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_monthly_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_year = table.Column<int>(type: "integer", nullable: false),
                    period_month = table.Column<int>(type: "integer", nullable: false),
                    limit_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    consumed_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    reserved_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_monthly_accounts_pkey", x => x.id);
                    table.UniqueConstraint("ak_ai_monthly_accounts_id_org", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_ai_monthly_accounts_consumed", "consumed_usd >= 0");
                    table.CheckConstraint("ck_ai_monthly_accounts_limit", "limit_usd >= 0");
                    table.CheckConstraint("ck_ai_monthly_accounts_month", "period_month >= 1 AND period_month <= 12");
                    table.CheckConstraint("ck_ai_monthly_accounts_reserved", "reserved_usd >= 0");
                    table.CheckConstraint("ck_ai_monthly_accounts_year", "period_year >= 2020 AND period_year <= 2100");
                    table.ForeignKey(
                        name: "FK_ai_monthly_accounts_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_organization_monthly_limits",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monthly_limit_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_organization_monthly_limits_pkey", x => x.organization_id);
                    table.CheckConstraint("ck_ai_org_monthly_limits_amount", "monthly_limit_usd >= 0");
                    table.CheckConstraint("ck_ai_org_monthly_limits_reason", "length(btrim(reason)) > 0");
                    table.ForeignKey(
                        name: "FK_ai_organization_monthly_limits_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_organization_monthly_limits_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_year = table.Column<int>(type: "integer", nullable: false),
                    period_month = table.Column<int>(type: "integer", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    pricing_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    input_cost_per_million_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    cached_input_cost_per_million_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    output_cost_per_million_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    estimated_input_tokens = table.Column<int>(type: "integer", nullable: false),
                    max_output_tokens = table.Column<int>(type: "integer", nullable: false),
                    reserved_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    settled_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    actual_input_tokens = table.Column<int>(type: "integer", nullable: true),
                    actual_cached_input_tokens = table.Column<int>(type: "integer", nullable: true),
                    actual_output_tokens = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    reconciled_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reconciliation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    settled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_usage_reservations_pkey", x => x.id);
                    table.CheckConstraint("ck_ai_usage_reservations_attempt", "attempt_count > 0");
                    table.CheckConstraint("ck_ai_usage_reservations_month", "period_month >= 1 AND period_month <= 12");
                    table.CheckConstraint("ck_ai_usage_reservations_prices", "input_cost_per_million_usd >= 0 AND cached_input_cost_per_million_usd >= 0 AND output_cost_per_million_usd >= 0");
                    table.CheckConstraint("ck_ai_usage_reservations_reserved", "reserved_usd >= 0");
                    table.CheckConstraint("ck_ai_usage_reservations_settled", "settled_usd IS NULL OR settled_usd >= 0");
                    table.CheckConstraint("ck_ai_usage_reservations_status", "status IN ('Reserved', 'Settled', 'Released', 'Indeterminate')");
                    table.CheckConstraint("ck_ai_usage_reservations_tokens", "estimated_input_tokens >= 0 AND max_output_tokens >= 0");
                    table.CheckConstraint("ck_ai_usage_reservations_year", "period_year >= 2020 AND period_year <= 2100");
                    table.ForeignKey(
                        name: "FK_ai_usage_reservations_ai_monthly_accounts_account_id_organi~",
                        columns: x => new { x.account_id, x.organization_id },
                        principalTable: "ai_monthly_accounts",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_usage_reservations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_usage_reservations_users_reconciled_by_id",
                        column: x => x.reconciled_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_usage_reservations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_financial_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    previous_limit_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    new_limit_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    settled_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_financial_audit_logs_pkey", x => x.id);
                    table.CheckConstraint("ck_ai_financial_audit_action", "action IN ('SetMonthlyLimit', 'ReconcileSettle', 'ReconcileRelease')");
                    table.CheckConstraint("ck_ai_financial_audit_reason", "length(btrim(reason)) > 0");
                    table.ForeignKey(
                        name: "FK_ai_financial_audit_logs_ai_usage_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalTable: "ai_usage_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_financial_audit_logs_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_financial_audit_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_financial_audit_logs_actor_user_id",
                table: "ai_financial_audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_financial_audit_logs_reservation_id",
                table: "ai_financial_audit_logs",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_financial_audit_org_created_at",
                table: "ai_financial_audit_logs",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_ai_monthly_accounts_org_period",
                table: "ai_monthly_accounts",
                columns: new[] { "organization_id", "period_year", "period_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_organization_monthly_limits_updated_by_id",
                table: "ai_organization_monthly_limits",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_reservations_account_id_organization_id",
                table: "ai_usage_reservations",
                columns: new[] { "account_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_reservations_org_period_status",
                table: "ai_usage_reservations",
                columns: new[] { "organization_id", "period_year", "period_month", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_reservations_reconciled_by_id",
                table: "ai_usage_reservations",
                column: "reconciled_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_reservations_status_created_at",
                table: "ai_usage_reservations",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_reservations_user_id",
                table: "ai_usage_reservations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_ai_usage_reservations_solicitacao_attempt",
                table: "ai_usage_reservations",
                columns: new[] { "solicitacao_id", "attempt_count" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_financial_audit_logs");

            migrationBuilder.DropTable(
                name: "ai_organization_monthly_limits");

            migrationBuilder.DropTable(
                name: "ai_usage_reservations");

            migrationBuilder.DropTable(
                name: "ai_monthly_accounts");
        }
    }
}
