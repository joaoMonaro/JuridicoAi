using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: true),
                    cached_input_tokens = table.Column<int>(type: "integer", nullable: true),
                    output_tokens = table.Column<int>(type: "integer", nullable: true),
                    estimated_cost_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    pricing_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_usage_ledger_pkey", x => x.id);
                    table.CheckConstraint("ck_ai_usage_attempt_count", "attempt_count > 0");
                    table.CheckConstraint("ck_ai_usage_cost", "estimated_cost_usd IS NULL OR estimated_cost_usd >= 0");
                    table.CheckConstraint("ck_ai_usage_duration", "duration_ms >= 0");
                    table.ForeignKey(
                        name: "FK_ai_usage_ledger_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_usage_ledger_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_features",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_features_pkey", x => new { x.organization_id, x.feature });
                    table.CheckConstraint("ck_organization_features_feature", "feature IN ('AI_ASSISTANT', 'AI_EDIT', 'AI_REVIEW', 'AI_DOCUMENT_GENERATION')");
                    table.ForeignKey(
                        name: "FK_organization_features_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_features_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_correlation_id",
                table: "ai_usage_ledger",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_ledger_user_id",
                table: "ai_usage_ledger",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_organization_created_at",
                table: "ai_usage_ledger",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_ai_usage_solicitacao_attempt",
                table: "ai_usage_ledger",
                columns: new[] { "solicitacao_id", "attempt_count" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organization_features_enabled",
                table: "organization_features",
                columns: new[] { "organization_id", "enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_features_updated_by_id",
                table: "organization_features",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage_ledger");

            migrationBuilder.DropTable(
                name: "organization_features");
        }
    }
}
