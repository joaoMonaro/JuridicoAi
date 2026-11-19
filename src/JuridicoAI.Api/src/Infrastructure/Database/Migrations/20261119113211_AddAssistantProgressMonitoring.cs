using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantProgressMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ultimo_progresso_em",
                table: "ai_generations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ultimo_progresso_em",
                table: "ai_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ultimo_progresso_em",
                table: "ai_document_analyses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ultimo_progresso_em",
                table: "ai_deletion_requests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ultimo_progresso_em",
                table: "ai_generations");

            migrationBuilder.DropColumn(
                name: "ultimo_progresso_em",
                table: "ai_documents");

            migrationBuilder.DropColumn(
                name: "ultimo_progresso_em",
                table: "ai_document_analyses");

            migrationBuilder.DropColumn(
                name: "ultimo_progresso_em",
                table: "ai_deletion_requests");
        }
    }
}
