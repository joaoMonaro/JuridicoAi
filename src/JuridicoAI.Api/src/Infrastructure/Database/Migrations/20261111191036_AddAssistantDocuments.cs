using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "fontes_documentais",
                table: "ai_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_casos_id_organization_id",
                table: "casos",
                columns: new[] { "id", "organization_id" });

            migrationBuilder.CreateTable(
                name: "ai_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento_caso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_upload_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    formato = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    storage_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vinculado = table.Column<bool>(type: "boolean", nullable: false),
                    codigo_erro = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    paginas_total = table.Column<int>(type: "integer", nullable: true),
                    paginas_concluidas = table.Column<int>(type: "integer", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    extrator_versao = table.Column<string>(type: "text", nullable: false),
                    qualidade_versao = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_documents", x => x.id);
                    table.UniqueConstraint("AK_ai_documents_id_organization_id", x => new { x.id, x.organization_id });
                    table.CheckConstraint("ck_ai_document_origin", "(conversa_id IS NOT NULL AND documento_caso_id IS NULL AND client_upload_id IS NOT NULL) OR (conversa_id IS NULL AND documento_caso_id IS NOT NULL AND client_upload_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_ai_documents_ai_conversations_conversa_id_organization_id",
                        columns: x => new { x.conversa_id, x.organization_id },
                        principalTable: "ai_conversations",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_documents_casos_caso_id_organization_id",
                        columns: x => new { x.caso_id, x.organization_id },
                        principalTable: "casos",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_documents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_document_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    pagina = table.Column<int>(type: "integer", nullable: true),
                    localizador = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    qualidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    metricas = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    busca = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false, computedColumnSql: "to_tsvector('portuguese', CASE WHEN qualidade = 'Confiavel' THEN texto ELSE '' END)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_document_chunks", x => x.id);
                    table.UniqueConstraint("AK_ai_document_chunks_id_organization_id", x => new { x.id, x.organization_id });
                    table.ForeignKey(
                        name: "FK_ai_document_chunks_ai_documents_documento_id_organization_id",
                        columns: x => new { x.documento_id, x.organization_id },
                        principalTable: "ai_documents",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_chunks_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_chunks_busca",
                table: "ai_document_chunks",
                column: "busca")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_chunks_documento_id_ordem",
                table: "ai_document_chunks",
                columns: new[] { "documento_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_chunks_documento_id_organization_id",
                table: "ai_document_chunks",
                columns: new[] { "documento_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_chunks_organization_id",
                table: "ai_document_chunks",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_documents_caso_id_organization_id",
                table: "ai_documents",
                columns: new[] { "caso_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_documents_conversa_id_client_upload_id",
                table: "ai_documents",
                columns: new[] { "conversa_id", "client_upload_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_documents_conversa_id_organization_id",
                table: "ai_documents",
                columns: new[] { "conversa_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_documents_documento_caso_id_storage_id",
                table: "ai_documents",
                columns: new[] { "documento_caso_id", "storage_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_documents_estado_lease_ate",
                table: "ai_documents",
                columns: new[] { "estado", "lease_ate" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_documents_organization_id",
                table: "ai_documents",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ai_documents) OR EXISTS (SELECT 1 FROM ai_messages WHERE fontes_documentais IS NOT NULL) THEN
                        RAISE EXCEPTION 'Documentos e proveniência devem ser preservados. Use AssistenteDocumentos:Enabled=false para rollback funcional.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "ai_document_chunks");

            migrationBuilder.DropTable(
                name: "ai_documents");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_casos_id_organization_id",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "fontes_documentais",
                table: "ai_messages");
        }
    }
}
