using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiTransparencyAcknowledgements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_transparency_acknowledgements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notice_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_transparency_acknowledgements_pkey", x => x.id);
                    table.CheckConstraint("ck_ai_transparency_notice_version", "length(btrim(notice_version)) > 0");
                    table.ForeignKey(
                        name: "ai_transparency_ack_organization_id_fkey",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_transparency_ack_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_transparency_acknowledgements_user_id",
                table: "ai_transparency_acknowledgements",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_transparency_organization_acknowledged_at",
                table: "ai_transparency_acknowledgements",
                columns: new[] { "organization_id", "acknowledged_at" });

            migrationBuilder.CreateIndex(
                name: "uq_ai_transparency_organization_user_version",
                table: "ai_transparency_acknowledgements",
                columns: new[] { "organization_id", "user_id", "notice_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_transparency_acknowledgements");
        }
    }
}
