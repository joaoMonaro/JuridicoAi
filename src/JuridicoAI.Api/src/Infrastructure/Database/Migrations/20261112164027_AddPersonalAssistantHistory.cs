using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalAssistantHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_conversations_user_id",
                table: "ai_conversations");

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "ai_conversations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Ativa");

            migrationBuilder.AddColumn<int>(
                name: "exclusao_epoch",
                table: "ai_conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ai_deletion_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    arquivos = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    autorizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concluida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    disponivel_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    codigo_erro = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_deletion_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_deletion_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personal_history_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revocation_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revogada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_history_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_personal_history_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_user_id_atualizada_em_id",
                table: "ai_conversations",
                columns: new[] { "user_id", "atualizada_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_deletion_requests_conversa_id",
                table: "ai_deletion_requests",
                column: "conversa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_deletion_requests_estado_disponivel_em",
                table: "ai_deletion_requests",
                columns: new[] { "estado", "disponivel_em" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_deletion_requests_user_id_autorizada_em_id",
                table: "ai_deletion_requests",
                columns: new[] { "user_id", "autorizada_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_history_sessions_revocation_token_hash",
                table: "personal_history_sessions",
                column: "revocation_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_history_sessions_refresh_token_hash",
                table: "personal_history_sessions",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_history_sessions_user_id",
                table: "personal_history_sessions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM ai_deletion_requests) OR EXISTS (SELECT 1 FROM personal_history_sessions) OR EXISTS (SELECT 1 FROM ai_conversations WHERE estado <> 'Ativa') THEN
                    RAISE EXCEPTION 'Rollback bloqueado: protocolos de exclusão e sessões pessoais devem ser preservados.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "ai_deletion_requests");

            migrationBuilder.DropTable(
                name: "personal_history_sessions");

            migrationBuilder.DropIndex(
                name: "IX_ai_conversations_user_id_atualizada_em_id",
                table: "ai_conversations");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "ai_conversations");

            migrationBuilder.DropColumn(
                name: "exclusao_epoch",
                table: "ai_conversations");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_user_id",
                table: "ai_conversations",
                column: "user_id");
        }
    }
}
