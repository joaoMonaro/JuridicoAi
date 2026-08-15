using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPecas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "templates_peca",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conteudo_json = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    page_config = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    header_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    footer_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    styles = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_exclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("templates_peca_pkey", x => x.id);
                    table.CheckConstraint("ck_templates_peca_nome", "length(btrim(nome)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "pecas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conteudo_json = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    page_config = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    header_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    footer_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    styles = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_exclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pecas_pkey", x => x.id);
                    table.CheckConstraint("ck_pecas_status", "status IN ('Rascunho', 'Finalizada')");
                    table.CheckConstraint("ck_pecas_titulo", "length(btrim(titulo)) > 0");
                    table.CheckConstraint("ck_pecas_versao", "versao > 0");
                    table.ForeignKey(
                        name: "pecas_caso_id_fkey",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "pecas_template_id_fkey",
                        column: x => x.template_id,
                        principalTable: "templates_peca",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pecas_caso_listagem",
                table: "pecas",
                columns: new[] { "caso_id", "data_exclusao", "data_atualizacao" });

            migrationBuilder.CreateIndex(
                name: "ix_pecas_template_id",
                table: "pecas",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_templates_peca_listagem",
                table: "templates_peca",
                columns: new[] { "data_exclusao", "nome" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pecas");

            migrationBuilder.DropTable(
                name: "templates_peca");
        }
    }
}
