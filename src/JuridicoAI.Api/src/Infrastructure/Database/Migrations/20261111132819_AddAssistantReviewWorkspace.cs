using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantReviewWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_pecas_id_organization_id",
                table: "pecas",
                columns: new[] { "id", "organization_id" });

            migrationBuilder.CreateTable(
                name: "ai_conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    proxima_sequencia = table.Column<long>(type: "bigint", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversations", x => x.id);
                    table.UniqueConstraint("AK_ai_conversations_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_conversations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_conversations_pecas_peca_id_organization_id",
                        columns: x => new { x.peca_id, x.organization_id },
                        principalTable: "pecas",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_conversations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    versao_decisoes = table.Column<int>(type: "integer", nullable: false),
                    versao_peca_aplicavel = table.Column<int>(type: "integer", nullable: false),
                    documento_base = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    grupos = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_proposals", x => x.id);
                    table.UniqueConstraint("AK_ai_proposals_id_conversa_id_organization_id", x => new { x.id, x.conversa_id, x.organization_id });
                    table.UniqueConstraint("AK_ai_proposals_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_proposals_ai_conversations_conversa_id_organization_id",
                        columns: x => new { x.conversa_id, x.organization_id },
                        principalTable: "ai_conversations",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_proposals_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_application_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    versao_anterior = table.Column<int>(type: "integer", nullable: false),
                    versao_resultante = table.Column<int>(type: "integer", nullable: false),
                    conteudo_json = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    grupos_aplicados = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_application_receipts", x => x.id);
                    table.UniqueConstraint("AK_ai_application_receipts_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_application_receipts_ai_conversations_conversa_id_organi~",
                        columns: x => new { x.conversa_id, x.organization_id },
                        principalTable: "ai_conversations",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_application_receipts_ai_proposals_proposta_id_conversa_i~",
                        columns: x => new { x.proposta_id, x.conversa_id, x.organization_id },
                        principalTable: "ai_proposals",
                        principalColumns: new[] { "id", "conversa_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_application_receipts_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<long>(type: "bigint", nullable: false),
                    autor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    proposta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_messages", x => x.id);
                    table.UniqueConstraint("AK_ai_messages_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_messages_ai_conversations_conversa_id_organization_id",
                        columns: x => new { x.conversa_id, x.organization_id },
                        principalTable: "ai_conversations",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_messages_ai_proposals_proposta_id_conversa_id_organizati~",
                        columns: x => new { x.proposta_id, x.conversa_id, x.organization_id },
                        principalTable: "ai_proposals",
                        principalColumns: new[] { "id", "conversa_id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_messages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_application_receipts_conversa_id_organization_id",
                table: "ai_application_receipts",
                columns: new[] { "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_application_receipts_conversa_id_request_id",
                table: "ai_application_receipts",
                columns: new[] { "conversa_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_application_receipts_organization_id",
                table: "ai_application_receipts",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_application_receipts_proposta_id_conversa_id_organizatio~",
                table: "ai_application_receipts",
                columns: new[] { "proposta_id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_organization_id_user_id_peca_id",
                table: "ai_conversations",
                columns: new[] { "organization_id", "user_id", "peca_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_peca_id_organization_id",
                table: "ai_conversations",
                columns: new[] { "peca_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_user_id",
                table: "ai_conversations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id_organization_id",
                table: "ai_messages",
                columns: new[] { "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversa_id_sequencia",
                table: "ai_messages",
                columns: new[] { "conversa_id", "sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_organization_id",
                table: "ai_messages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_proposta_id_conversa_id_organization_id",
                table: "ai_messages",
                columns: new[] { "proposta_id", "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_proposals_conversa_id_client_request_id",
                table: "ai_proposals",
                columns: new[] { "conversa_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_proposals_conversa_id_organization_id",
                table: "ai_proposals",
                columns: new[] { "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_proposals_organization_id",
                table: "ai_proposals",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_application_receipts");

            migrationBuilder.DropTable(
                name: "ai_messages");

            migrationBuilder.DropTable(
                name: "ai_proposals");

            migrationBuilder.DropTable(
                name: "ai_conversations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_pecas_id_organization_id",
                table: "pecas");
        }
    }
}
