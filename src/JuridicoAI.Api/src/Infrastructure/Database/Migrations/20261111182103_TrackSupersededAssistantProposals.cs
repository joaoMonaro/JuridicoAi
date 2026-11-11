using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class TrackSupersededAssistantProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "substituida_por_id",
                table: "ai_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_proposals_substituida_por_id_conversa_id_organization_id",
                table: "ai_proposals",
                columns: new[] { "substituida_por_id", "conversa_id", "organization_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_ai_proposals_ai_proposals_substituida_por_id_conversa_id_or~",
                table: "ai_proposals",
                columns: new[] { "substituida_por_id", "conversa_id", "organization_id" },
                principalTable: "ai_proposals",
                principalColumns: new[] { "id", "conversa_id", "organization_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ai_proposals WHERE substituida_por_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'O vínculo de propostas substituídas deve ser preservado. Use AutomaticConversationEnabled=false para rollback funcional.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_ai_proposals_ai_proposals_substituida_por_id_conversa_id_or~",
                table: "ai_proposals");

            migrationBuilder.DropIndex(
                name: "IX_ai_proposals_substituida_por_id_conversa_id_organization_id",
                table: "ai_proposals");

            migrationBuilder.DropColumn(
                name: "substituida_por_id",
                table: "ai_proposals");
        }
    }
}
