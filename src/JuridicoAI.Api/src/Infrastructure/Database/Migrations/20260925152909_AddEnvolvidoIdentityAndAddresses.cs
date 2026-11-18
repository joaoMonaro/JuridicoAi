using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvolvidoIdentityAndAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cpf_cnpj",
                table: "envolvidos_caso",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_nascimento",
                table: "envolvidos_caso",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "envolvidos_caso",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_civil",
                table: "envolvidos_caso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inscricao_estadual",
                table: "envolvidos_caso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nacionalidade",
                table: "envolvidos_caso",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "natureza",
                table: "envolvidos_caso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NaoInformada");

            migrationBuilder.AddColumn<string>(
                name: "nome_fantasia",
                table: "envolvidos_caso",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profissao",
                table: "envolvidos_caso",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rg",
                table: "envolvidos_caso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rg_orgao_emissor",
                table: "envolvidos_caso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rg_uf",
                table: "envolvidos_caso",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefone",
                table: "envolvidos_caso",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "enderecos_envolvidos_caso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    envolvido_caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    principal = table.Column<bool>(type: "boolean", nullable: false),
                    cep = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    estado_provincia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("enderecos_envolvidos_caso_pkey", x => x.id);
                    table.CheckConstraint("ck_enderecos_envolvidos_cidade", "length(btrim(cidade)) > 0");
                    table.CheckConstraint("ck_enderecos_envolvidos_logradouro", "length(btrim(logradouro)) > 0");
                    table.CheckConstraint("ck_enderecos_envolvidos_pais", "length(pais) = 2");
                    table.CheckConstraint("ck_enderecos_envolvidos_tipo", "tipo IN ('Residencial', 'Comercial', 'Sede', 'Correspondencia', 'Citacao', 'Outro')");
                    table.ForeignKey(
                        name: "enderecos_envolvidos_caso_envolvido_id_fkey",
                        column: x => x.envolvido_caso_id,
                        principalTable: "envolvidos_caso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                WITH documentos AS (
                    SELECT id, regexp_replace(documento, '[^0-9]', '', 'g') AS numero
                    FROM envolvidos_caso
                    WHERE documento IS NOT NULL
                      AND documento ~ '^[[:space:]0-9./-]+$'
                ), cpfs AS (
                    SELECT id, numero,
                           (SELECT SUM(SUBSTRING(numero FROM i FOR 1)::integer * (11 - i))
                              FROM generate_series(1, 9) AS i) AS soma1,
                           (SELECT SUM(SUBSTRING(numero FROM i FOR 1)::integer * (12 - i))
                              FROM generate_series(1, 10) AS i) AS soma2
                    FROM documentos
                    WHERE length(numero) = 11
                      AND numero NOT IN ('00000000000', '11111111111', '22222222222', '33333333333',
                                         '44444444444', '55555555555', '66666666666', '77777777777',
                                         '88888888888', '99999999999')
                ), cpfs_validos AS (
                    SELECT id, numero
                    FROM cpfs
                    WHERE (CASE WHEN soma1 % 11 < 2 THEN 0 ELSE 11 - soma1 % 11 END) = SUBSTRING(numero FROM 10 FOR 1)::integer
                      AND (CASE WHEN soma2 % 11 < 2 THEN 0 ELSE 11 - soma2 % 11 END) = SUBSTRING(numero FROM 11 FOR 1)::integer
                )
                UPDATE envolvidos_caso AS e
                   SET cpf_cnpj = v.numero, natureza = 'PessoaFisica'
                  FROM cpfs_validos AS v
                 WHERE e.id = v.id;

                WITH documentos AS (
                    SELECT id, regexp_replace(documento, '[^0-9]', '', 'g') AS numero
                    FROM envolvidos_caso
                    WHERE documento IS NOT NULL
                      AND documento ~ '^[[:space:]0-9./-]+$'
                ), cnpjs AS (
                    SELECT id, numero,
                           (SELECT SUM(SUBSTRING(numero FROM i FOR 1)::integer *
                                       (ARRAY[5,4,3,2,9,8,7,6,5,4,3,2])[i])
                              FROM generate_series(1, 12) AS i) AS soma1,
                           (SELECT SUM(SUBSTRING(numero FROM i FOR 1)::integer *
                                       (ARRAY[6,5,4,3,2,9,8,7,6,5,4,3,2])[i])
                              FROM generate_series(1, 13) AS i) AS soma2
                    FROM documentos
                    WHERE length(numero) = 14
                      AND numero NOT IN ('00000000000000', '11111111111111', '22222222222222',
                                         '33333333333333', '44444444444444', '55555555555555',
                                         '66666666666666', '77777777777777', '88888888888888',
                                         '99999999999999')
                ), cnpjs_validos AS (
                    SELECT id, numero
                    FROM cnpjs
                    WHERE (CASE WHEN soma1 % 11 < 2 THEN 0 ELSE 11 - soma1 % 11 END) = SUBSTRING(numero FROM 13 FOR 1)::integer
                      AND (CASE WHEN soma2 % 11 < 2 THEN 0 ELSE 11 - soma2 % 11 END) = SUBSTRING(numero FROM 14 FOR 1)::integer
                )
                UPDATE envolvidos_caso AS e
                   SET cpf_cnpj = v.numero, natureza = 'PessoaJuridica'
                  FROM cnpjs_validos AS v
                 WHERE e.id = v.id;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_envolvidos_caso_estado_civil",
                table: "envolvidos_caso",
                sql: "estado_civil IS NULL OR estado_civil IN ('NaoInformado', 'Solteiro', 'Casado', 'UniaoEstavel', 'Separado', 'Divorciado', 'Viuvo')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_envolvidos_caso_natureza",
                table: "envolvidos_caso",
                sql: "natureza IN ('NaoInformada', 'PessoaFisica', 'PessoaJuridica', 'OrgaoPublico')");

            migrationBuilder.CreateIndex(
                name: "ix_enderecos_envolvidos_envolvido_id",
                table: "enderecos_envolvidos_caso",
                column: "envolvido_caso_id");

            migrationBuilder.CreateIndex(
                name: "uq_enderecos_envolvidos_principal_tipo",
                table: "enderecos_envolvidos_caso",
                columns: new[] { "envolvido_caso_id", "tipo" },
                unique: true,
                filter: "principal = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "enderecos_envolvidos_caso");

            migrationBuilder.DropCheckConstraint(
                name: "ck_envolvidos_caso_estado_civil",
                table: "envolvidos_caso");

            migrationBuilder.DropCheckConstraint(
                name: "ck_envolvidos_caso_natureza",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "cpf_cnpj",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "data_nascimento",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "email",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "estado_civil",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "inscricao_estadual",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "nacionalidade",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "natureza",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "nome_fantasia",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "profissao",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "rg",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "rg_orgao_emissor",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "rg_uf",
                table: "envolvidos_caso");

            migrationBuilder.DropColumn(
                name: "telefone",
                table: "envolvidos_caso");
        }
    }
}
