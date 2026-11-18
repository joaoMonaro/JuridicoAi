using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableDocumentAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages");

            migrationBuilder.AlterColumn<string>(
                name: "prompt_version",
                table: "ai_usage_ledger",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<Guid>(
                name: "analise_documental_id",
                table: "ai_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_document_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resposta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifesto = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    manifesto_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fontes = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    politica_versao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fonte_indice = table.Column<int>(type: "integer", nullable: false),
                    apos_ordem = table.Column<int>(type: "integer", nullable: false),
                    tokens_finais_reservados = table.Column<int>(type: "integer", nullable: false),
                    solicitacao_final_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resumo = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    codigo_erro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    prazo_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_document_analyses", x => x.id);
                    table.UniqueConstraint("AK_ai_document_analyses_id_conversa_id_organization_id", x => new { x.id, x.conversa_id, x.organization_id });
                    table.UniqueConstraint("AK_ai_document_analyses_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_document_analyses_ai_conversations_conversa_id_organizat~",
                        columns: x => new { x.conversa_id, x.organization_id },
                        principalTable: "ai_conversations",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_analyses_ai_messages_pedido_id_conversa_id_orga~",
                        columns: x => new { x.pedido_id, x.conversa_id, x.organization_id },
                        principalTable: "ai_messages",
                        principalColumns: new[] { "id", "conversa_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_analyses_ai_messages_resposta_id_conversa_id_or~",
                        columns: x => new { x.resposta_id, x.conversa_id, x.organization_id },
                        principalTable: "ai_messages",
                        principalColumns: new[] { "id", "conversa_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_analyses_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_analyses_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_document_analysis_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entradas = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    referencias = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    registros = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    consolidado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ultima_ordem = table.Column<int>(type: "integer", nullable: true),
                    tokens_reservados = table.Column<int>(type: "integer", nullable: false),
                    tokens_liquidados = table.Column<int>(type: "integer", nullable: false),
                    chamadas = table.Column<int>(type: "integer", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_document_analysis_batches", x => x.id);
                    table.UniqueConstraint("AK_ai_document_analysis_batches_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_document_analysis_batches_ai_document_analyses_analise_i~",
                        columns: x => new { x.analise_id, x.organization_id },
                        principalTable: "ai_document_analyses",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_analysis_batches_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_analise_documental_id_conversa_id_organization_~",
                table: "ai_messages",
                columns: new[] { "analise_documental_id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages",
                column: "conversa_id",
                unique: true,
                filter: "estado IN ('Gerando', 'AguardandoAnalise')");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analyses_conversa_id_manifesto_sha256",
                table: "ai_document_analyses",
                columns: new[] { "conversa_id", "manifesto_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analyses_conversa_id_organization_id",
                table: "ai_document_analyses",
                columns: new[] { "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analyses_estado_lease_ate",
                table: "ai_document_analyses",
                columns: new[] { "estado", "lease_ate" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analyses_organization_id",
                table: "ai_document_analyses",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analyses_pedido_id_conversa_id_organization_id",
                table: "ai_document_analyses",
                columns: new[] { "pedido_id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analyses_resposta_id",
                table: "ai_document_analyses",
                column: "resposta_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analyses_resposta_id_conversa_id_organization_id",
                table: "ai_document_analyses",
                columns: new[] { "resposta_id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analyses_user_id",
                table: "ai_document_analyses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analysis_batches_analise_id_ordem",
                table: "ai_document_analysis_batches",
                columns: new[] { "analise_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analysis_batches_analise_id_organization_id",
                table: "ai_document_analysis_batches",
                columns: new[] { "analise_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_analysis_batches_organization_id",
                table: "ai_document_analysis_batches",
                column: "organization_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_messages_ai_document_analyses_analise_documental_id_conv~",
                table: "ai_messages",
                columns: new[] { "analise_documental_id", "conversa_id", "organization_id" },
                principalTable: "ai_document_analyses",
                principalColumns: new[] { "id", "conversa_id", "organization_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ai_document_analyses) OR EXISTS (SELECT 1 FROM ai_document_analysis_batches)
                       OR EXISTS (SELECT 1 FROM ai_messages WHERE analise_documental_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM ai_usage_ledger WHERE length(prompt_version) > 100) THEN
                        RAISE EXCEPTION 'Análise persistida: desative AnalysisEnabled para rollback funcional sem excluir dados.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_ai_messages_ai_document_analyses_analise_documental_id_conv~",
                table: "ai_messages");

            migrationBuilder.DropTable(
                name: "ai_document_analysis_batches");

            migrationBuilder.DropTable(
                name: "ai_document_analyses");

            migrationBuilder.DropIndex(
                name: "IX_ai_messages_analise_documental_id_conversa_id_organization_~",
                table: "ai_messages");

            migrationBuilder.DropIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages");

            migrationBuilder.DropColumn(
                name: "analise_documental_id",
                table: "ai_messages");

            migrationBuilder.AlterColumn<string>(
                name: "prompt_version",
                table: "ai_usage_ledger",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id",
                table: "ai_messages",
                column: "conversa_id",
                unique: true,
                filter: "estado = 'Gerando'");
        }
    }
}
