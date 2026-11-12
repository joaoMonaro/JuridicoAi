using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class TrackAssistantDocumentCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "cobertura_documental",
                table: "ai_messages",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ai_messages WHERE cobertura_documental IS NOT NULL) THEN
                        RAISE EXCEPTION 'A cobertura documental deve ser preservada. Desabilite RetrievalEnabled para rollback funcional.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "cobertura_documental",
                table: "ai_messages");
        }
    }
}
