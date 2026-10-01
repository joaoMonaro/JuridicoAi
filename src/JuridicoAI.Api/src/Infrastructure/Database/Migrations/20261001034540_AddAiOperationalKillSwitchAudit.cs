using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiOperationalKillSwitchAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_feature_change_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    feature = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_feature_change_audit_pkey", x => x.id);
                    table.CheckConstraint("ck_ai_feature_change_audit_scope", "scope IN ('Global', 'Organization')");
                    table.CheckConstraint("ck_ai_feature_change_audit_target", "(scope = 'Global' AND organization_id IS NULL AND feature = 'AI_ENABLED') OR (scope = 'Organization' AND organization_id IS NOT NULL AND feature IN ('AI_ASSISTANT', 'AI_EDIT', 'AI_REVIEW', 'AI_DOCUMENT_GENERATION'))");
                    table.ForeignKey(
                        name: "FK_ai_feature_change_audit_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_feature_change_audit_users_changed_by_id",
                        column: x => x.changed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_operational_controls",
                columns: table => new
                {
                    feature = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_operational_controls_pkey", x => x.feature);
                    table.CheckConstraint("ck_ai_operational_controls_feature", "feature IN ('AI_ENABLED')");
                    table.ForeignKey(
                        name: "FK_ai_operational_controls_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_feature_change_audit_changed_at",
                table: "ai_feature_change_audit",
                column: "changed_at");

            migrationBuilder.CreateIndex(
                name: "IX_ai_feature_change_audit_changed_by_id",
                table: "ai_feature_change_audit",
                column: "changed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_feature_change_audit_organization_changed_at",
                table: "ai_feature_change_audit",
                columns: new[] { "organization_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_operational_controls_updated_by_id",
                table: "ai_operational_controls",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_feature_change_audit");

            migrationBuilder.DropTable(
                name: "ai_operational_controls");
        }
    }
}
