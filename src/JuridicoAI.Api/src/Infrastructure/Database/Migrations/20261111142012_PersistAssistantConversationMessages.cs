using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PersistAssistantConversationMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "proposta_id",
                table: "ai_messages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "client_request_id",
                table: "ai_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "codigo_erro",
                table: "ai_messages",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "ai_messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Concluida");

            migrationBuilder.AddColumn<DateTime>(
                name: "expira_em",
                table: "ai_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_sha256",
                table: "ai_messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resposta_aid",
                table: "ai_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "resultado",
                table: "ai_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "solicitacao",
                table: "ai_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ai_messages_id_conversa_id_organization_id",
                table: "ai_messages",
                columns: new[] { "id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages",
                column: "conversa_id",
                unique: true,
                filter: "estado = 'Gerando'");

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id_client_request_id",
                table: "ai_messages",
                columns: new[] { "conversa_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id_resposta_aid",
                table: "ai_messages",
                columns: new[] { "conversa_id", "resposta_aid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_resposta_aid_conversa_id_organization_id",
                table: "ai_messages",
                columns: new[] { "resposta_aid", "conversa_id", "organization_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_ai_messages_ai_messages_resposta_aid_conversa_id_organizati~",
                table: "ai_messages",
                columns: new[] { "resposta_aid", "conversa_id", "organization_id" },
                principalTable: "ai_messages",
                principalColumns: new[] { "id", "conversa_id", "organization_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ai_messages WHERE solicitacao IS NOT NULL OR resposta_aid IS NOT NULL) THEN
                        RAISE EXCEPTION 'O histórico conversacional não pode ser convertido sem perda. Desative ConversationV2Enabled para rollback funcional.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_ai_messages_ai_messages_resposta_aid_conversa_id_organizati~",
                table: "ai_messages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ai_messages_id_conversa_id_organization_id",
                table: "ai_messages");

            migrationBuilder.DropIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages");

            migrationBuilder.DropIndex(
                name: "IX_ai_messages_conversa_id_client_request_id",
                table: "ai_messages");

            migrationBuilder.DropIndex(
                name: "IX_ai_messages_conversa_id_resposta_aid",
                table: "ai_messages");

            migrationBuilder.DropIndex(
                name: "IX_ai_messages_resposta_aid_conversa_id_organization_id",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "client_request_id",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "codigo_erro",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "expira_em",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "request_sha256",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "resposta_aid",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "resultado",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "solicitacao",
                table: "ai_messages");

            migrationBuilder.AlterColumn<Guid>(
                name: "proposta_id",
                table: "ai_messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
