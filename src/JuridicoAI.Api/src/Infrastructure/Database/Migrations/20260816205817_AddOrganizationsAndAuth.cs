using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsAndAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_templates_peca_listagem",
                table: "templates_peca");

            migrationBuilder.DropIndex(
                name: "ix_pecas_caso_listagem",
                table: "pecas");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "templates_peca",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "templates_peca",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "templates_peca",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "pecas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "pecas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "pecas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "casos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "casos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "casos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                    table.CheckConstraint("ck_organizations_nome", "length(btrim(nome)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_memberships_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revogada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultimo_uso_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserva dados privados anteriores à introdução de multitenancy.
            // O usuário legado é inativo e possui hash deliberadamente inutilizável.
            migrationBuilder.Sql("""
                INSERT INTO organizations (id, nome, status, data_criacao, data_atualizacao)
                VALUES ('00000000-0000-0000-0000-000000000000', 'Organization Legada', 'Active', NOW(), NOW());
                INSERT INTO users (id, nome, email, password_hash, status, data_criacao, data_atualizacao)
                VALUES ('00000000-0000-0000-0000-000000000000', 'Usuário Legado', 'legacy-disabled@local.invalid', '$disabled$', 'Inactive', NOW(), NOW());
                INSERT INTO memberships (id, organization_id, user_id, role, data_criacao, data_atualizacao)
                VALUES ('00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000',
                        '00000000-0000-0000-0000-000000000000', 'Owner', NOW(), NOW());
                ALTER TABLE casos ALTER COLUMN organization_id DROP DEFAULT;
                ALTER TABLE casos ALTER COLUMN created_by_id DROP DEFAULT;
                ALTER TABLE casos ALTER COLUMN updated_by_id DROP DEFAULT;
                ALTER TABLE pecas ALTER COLUMN organization_id DROP DEFAULT;
                ALTER TABLE pecas ALTER COLUMN created_by_id DROP DEFAULT;
                ALTER TABLE pecas ALTER COLUMN updated_by_id DROP DEFAULT;
                ALTER TABLE templates_peca ALTER COLUMN organization_id DROP DEFAULT;
                ALTER TABLE templates_peca ALTER COLUMN created_by_id DROP DEFAULT;
                ALTER TABLE templates_peca ALTER COLUMN updated_by_id DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_templates_peca_created_by_id",
                table: "templates_peca",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_templates_peca_listagem",
                table: "templates_peca",
                columns: new[] { "organization_id", "data_exclusao", "nome" });

            migrationBuilder.CreateIndex(
                name: "IX_templates_peca_updated_by_id",
                table: "templates_peca",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_pecas_caso_id",
                table: "pecas",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_pecas_caso_listagem",
                table: "pecas",
                columns: new[] { "organization_id", "caso_id", "data_exclusao", "data_atualizacao" });

            migrationBuilder.CreateIndex(
                name: "IX_pecas_created_by_id",
                table: "pecas",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_pecas_updated_by_id",
                table: "pecas",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_casos_created_by_id",
                table: "casos",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_organization_status",
                table: "casos",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_casos_updated_by_id",
                table: "casos",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_user_id",
                table: "memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_memberships_organization_user",
                table: "memberships",
                columns: new[] { "organization_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_organization_id",
                table: "sessions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_organization",
                table: "sessions",
                columns: new[] { "user_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "uq_sessions_refresh_token_hash",
                table: "sessions",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_casos_organizations_organization_id",
                table: "casos",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_casos_users_created_by_id",
                table: "casos",
                column: "created_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_casos_users_updated_by_id",
                table: "casos",
                column: "updated_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pecas_organizations_organization_id",
                table: "pecas",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pecas_users_created_by_id",
                table: "pecas",
                column: "created_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pecas_users_updated_by_id",
                table: "pecas",
                column: "updated_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_templates_peca_organizations_organization_id",
                table: "templates_peca",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_templates_peca_users_created_by_id",
                table: "templates_peca",
                column: "created_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_templates_peca_users_updated_by_id",
                table: "templates_peca",
                column: "updated_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_casos_organizations_organization_id",
                table: "casos");

            migrationBuilder.DropForeignKey(
                name: "FK_casos_users_created_by_id",
                table: "casos");

            migrationBuilder.DropForeignKey(
                name: "FK_casos_users_updated_by_id",
                table: "casos");

            migrationBuilder.DropForeignKey(
                name: "FK_pecas_organizations_organization_id",
                table: "pecas");

            migrationBuilder.DropForeignKey(
                name: "FK_pecas_users_created_by_id",
                table: "pecas");

            migrationBuilder.DropForeignKey(
                name: "FK_pecas_users_updated_by_id",
                table: "pecas");

            migrationBuilder.DropForeignKey(
                name: "FK_templates_peca_organizations_organization_id",
                table: "templates_peca");

            migrationBuilder.DropForeignKey(
                name: "FK_templates_peca_users_created_by_id",
                table: "templates_peca");

            migrationBuilder.DropForeignKey(
                name: "FK_templates_peca_users_updated_by_id",
                table: "templates_peca");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_templates_peca_created_by_id",
                table: "templates_peca");

            migrationBuilder.DropIndex(
                name: "ix_templates_peca_listagem",
                table: "templates_peca");

            migrationBuilder.DropIndex(
                name: "IX_templates_peca_updated_by_id",
                table: "templates_peca");

            migrationBuilder.DropIndex(
                name: "IX_pecas_caso_id",
                table: "pecas");

            migrationBuilder.DropIndex(
                name: "ix_pecas_caso_listagem",
                table: "pecas");

            migrationBuilder.DropIndex(
                name: "IX_pecas_created_by_id",
                table: "pecas");

            migrationBuilder.DropIndex(
                name: "IX_pecas_updated_by_id",
                table: "pecas");

            migrationBuilder.DropIndex(
                name: "IX_casos_created_by_id",
                table: "casos");

            migrationBuilder.DropIndex(
                name: "ix_casos_organization_status",
                table: "casos");

            migrationBuilder.DropIndex(
                name: "IX_casos_updated_by_id",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "templates_peca");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "templates_peca");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "templates_peca");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "pecas");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "pecas");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "pecas");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "casos");

            migrationBuilder.CreateIndex(
                name: "ix_templates_peca_listagem",
                table: "templates_peca",
                columns: new[] { "data_exclusao", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_pecas_caso_listagem",
                table: "pecas",
                columns: new[] { "caso_id", "data_exclusao", "data_atualizacao" });
        }
    }
}
