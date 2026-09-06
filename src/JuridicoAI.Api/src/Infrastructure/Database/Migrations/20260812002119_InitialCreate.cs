using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("casos_pkey", x => x.id);
                    table.CheckConstraint("ck_casos_status", "status IN ('Ativo', 'Arquivado')");
                    table.CheckConstraint("ck_casos_titulo", "length(btrim(titulo)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "contextos_caso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    conteudo = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("contextos_caso_pkey", x => x.id);
                    table.CheckConstraint("ck_contextos_caso_conteudo", "length(btrim(conteudo)) > 0");
                    table.CheckConstraint("ck_contextos_caso_tipo", "tipo IN ('RelatoInicial', 'ResumoDosFatos', 'Cronologia', 'Observacao', 'Estrategia', 'Outro')");
                    table.ForeignKey(
                        name: "contextos_caso_caso_id_fkey",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos_caso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_original = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    nome_armazenado = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    data_upload = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("documentos_caso_pkey", x => x.id);
                    table.CheckConstraint("ck_documentos_caso_tamanho", "tamanho_bytes > 0");
                    table.ForeignKey(
                        name: "documentos_caso_caso_id_fkey",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "envolvidos_caso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    dados_adicionais = table.Column<string>(type: "jsonb", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("envolvidos_caso_pkey", x => x.id);
                    table.CheckConstraint("ck_envolvidos_caso_nome", "length(btrim(nome)) > 0");
                    table.CheckConstraint("ck_envolvidos_caso_tipo", "tipo IN ('Cliente', 'ParteContraria', 'Advogado', 'Testemunha', 'TerceiroInteressado', 'Outro')");
                    table.ForeignKey(
                        name: "envolvidos_caso_caso_id_fkey",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_casos_status",
                table: "casos",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_contextos_caso_caso_id",
                table: "contextos_caso",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_caso_caso_id",
                table: "documentos_caso",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "uq_documentos_caso_storage_id",
                table: "documentos_caso",
                column: "storage_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_envolvidos_caso_caso_id",
                table: "envolvidos_caso",
                column: "caso_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contextos_caso");

            migrationBuilder.DropTable(
                name: "documentos_caso");

            migrationBuilder.DropTable(
                name: "envolvidos_caso");

            migrationBuilder.DropTable(
                name: "casos");
        }
    }
}
