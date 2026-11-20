using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMainSessionRevocationSecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "revocation_token_hash",
                table: "sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // O token legado atual é o primeiro segredo estável. Seu valor em
            // texto não precisa ser recuperado: somente o hash é copiado.
            migrationBuilder.Sql("UPDATE sessions SET revocation_token_hash = refresh_token_hash");

            migrationBuilder.AlterColumn<string>(
                name: "revocation_token_hash",
                table: "sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_sessions_revocation_token_hash",
                table: "sessions",
                column: "revocation_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM sessions
                    WHERE revogada_em IS NULL AND expira_em > NOW()
                      AND revocation_token_hash <> refresh_token_hash) THEN
                    RAISE EXCEPTION 'Não é possível remover a credencial de revogação de sessões ativas com tokens compostos.';
                  END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "uq_sessions_revocation_token_hash",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "revocation_token_hash",
                table: "sessions");
        }
    }
}
