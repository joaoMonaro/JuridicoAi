using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableAssistantGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages");

            migrationBuilder.AddColumn<Guid>(
                name: "geracao_id",
                table: "ai_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_generations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resposta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    politica_versao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    caso_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fontes_ids = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    contexto = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    plano = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    analise_id = table.Column<Guid>(type: "uuid", nullable: true),
                    codigo_erro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    prazo_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_generations", x => x.id);
                    table.UniqueConstraint("AK_ai_generations_id_conversa_id_organization_id", x => new { x.id, x.conversa_id, x.organization_id });
                    table.UniqueConstraint("AK_ai_generations_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_generations_ai_conversations_conversa_id_organization_id",
                        columns: x => new { x.conversa_id, x.organization_id },
                        principalTable: "ai_conversations",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_generations_ai_messages_pedido_id_conversa_id_organizati~",
                        columns: x => new { x.pedido_id, x.conversa_id, x.organization_id },
                        principalTable: "ai_messages",
                        principalColumns: new[] { "id", "conversa_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_generations_ai_messages_resposta_id_conversa_id_organiza~",
                        columns: x => new { x.resposta_id, x.conversa_id, x.organization_id },
                        principalTable: "ai_messages",
                        principalColumns: new[] { "id", "conversa_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_generations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_generations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_generation_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    geracao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chamada = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tokens_reservados = table.Column<int>(type: "integer", nullable: false),
                    tokens_liquidados = table.Column<int>(type: "integer", nullable: false),
                    resultado = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_generation_steps", x => x.id);
                    table.UniqueConstraint("AK_ai_generation_steps_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_generation_steps_ai_generations_geracao_id_organization_~",
                        columns: x => new { x.geracao_id, x.organization_id },
                        principalTable: "ai_generations",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_generation_steps_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages",
                column: "conversa_id",
                unique: true,
                filter: "estado IN ('Gerando', 'AguardandoAnalise', 'AguardandoGeracao')");

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_geracao_id_conversa_id_organization_id",
                table: "ai_messages",
                columns: new[] { "geracao_id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_generation_steps_geracao_id_organization_id",
                table: "ai_generation_steps",
                columns: new[] { "geracao_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_generation_steps_geracao_id_solicitacao_id_chamada",
                table: "ai_generation_steps",
                columns: new[] { "geracao_id", "solicitacao_id", "chamada" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_generation_steps_organization_id",
                table: "ai_generation_steps",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_generations_conversa_id_organization_id",
                table: "ai_generations",
                columns: new[] { "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_generations_estado_lease_ate",
                table: "ai_generations",
                columns: new[] { "estado", "lease_ate" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_generations_organization_id",
                table: "ai_generations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_generations_pedido_id_conversa_id_organization_id",
                table: "ai_generations",
                columns: new[] { "pedido_id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_generations_resposta_id",
                table: "ai_generations",
                column: "resposta_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_generations_resposta_id_conversa_id_organization_id",
                table: "ai_generations",
                columns: new[] { "resposta_id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_generations_user_id",
                table: "ai_generations",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_messages_ai_generations_geracao_id_conversa_id_organizat~",
                table: "ai_messages",
                columns: new[] { "geracao_id", "conversa_id", "organization_id" },
                principalTable: "ai_generations",
                principalColumns: new[] { "id", "conversa_id", "organization_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM ai_generations) OR EXISTS (SELECT 1 FROM ai_generation_steps) THEN
                    RAISE EXCEPTION 'Rollback bloqueado: pedidos e checkpoints de geração devem ser preservados.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_ai_messages_ai_generations_geracao_id_conversa_id_organizat~",
                table: "ai_messages");

            migrationBuilder.DropTable(
                name: "ai_generation_steps");

            migrationBuilder.DropTable(
                name: "ai_generations");

            migrationBuilder.DropIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages");

            migrationBuilder.DropIndex(
                name: "IX_ai_messages_geracao_id_conversa_id_organization_id",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "geracao_id",
                table: "ai_messages");

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages",
                column: "conversa_id",
                unique: true,
                filter: "estado IN ('Gerando', 'AguardandoAnalise')");
        }
    }
}
