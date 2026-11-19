using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantDeletionRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "journaled_at",
                table: "ai_deletion_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_restore_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    in_progress = table.Column<bool>(type: "boolean", nullable: false),
                    journal_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_restore_state", x => x.id);
                    table.CheckConstraint("ck_ai_restore_state_singleton", "id = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM ai_restore_state) OR EXISTS (SELECT 1 FROM ai_deletion_requests WHERE journaled_at IS NOT NULL) THEN
                    RAISE EXCEPTION 'Downgrade bloqueado: existem registros de proteção/restauração de exclusões.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "ai_restore_state");

            migrationBuilder.DropColumn(
                name: "journaled_at",
                table: "ai_deletion_requests");
        }
    }
}
