using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantVersionedMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX ix_ai_messages_user_history_search ON ai_messages USING gin (to_tsvector('portuguese'::regconfig, texto)) WHERE autor = 'Usuario'");
            migrationBuilder.AddColumn<JsonDocument>(
                name: "contexto_historico",
                table: "ai_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_memories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resposta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    ate_sequencia = table.Column<long>(type: "bigint", nullable: false),
                    politica_versao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    registros = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_memories", x => x.id);
                    table.UniqueConstraint("AK_ai_memories_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_memories_ai_conversations_conversa_id_organization_id",
                        columns: x => new { x.conversa_id, x.organization_id },
                        principalTable: "ai_conversations",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_memories_ai_messages_resposta_id_conversa_id_organizatio~",
                        columns: x => new { x.resposta_id, x.conversa_id, x.organization_id },
                        principalTable: "ai_messages",
                        principalColumns: new[] { "id", "conversa_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_memories_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_memories_conversa_id_organization_id",
                table: "ai_memories",
                columns: new[] { "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_memories_conversa_id_versao",
                table: "ai_memories",
                columns: new[] { "conversa_id", "versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_memories_organization_id",
                table: "ai_memories",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_memories_resposta_id",
                table: "ai_memories",
                column: "resposta_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_memories_resposta_id_conversa_id_organization_id",
                table: "ai_memories",
                columns: new[] { "resposta_id", "conversa_id", "organization_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Roll back functionality through the flag. Never drop retained history-derived data implicitly.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ai_memories) OR EXISTS (SELECT 1 FROM ai_messages WHERE contexto_historico IS NOT NULL) THEN
                        RAISE EXCEPTION 'Memória persistida: desative HistoryMemoryEnabled para rollback funcional sem excluir dados.';
                    END IF;
                END $$;
                """);
            migrationBuilder.Sql("DROP INDEX ix_ai_messages_user_history_search");
            migrationBuilder.DropTable(
                name: "ai_memories");

            migrationBuilder.DropColumn(
                name: "contexto_historico",
                table: "ai_messages");
        }
    }
}
