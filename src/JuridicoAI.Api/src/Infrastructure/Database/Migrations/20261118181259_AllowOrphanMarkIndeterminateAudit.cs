using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AllowOrphanMarkIndeterminateAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_financial_audit_action",
                table: "ai_financial_audit_logs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_financial_audit_action",
                table: "ai_financial_audit_logs",
                sql: "action IN ('SetMonthlyLimit', 'ReconcileSettle', 'ReconcileRelease', 'MarkIndeterminate')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_financial_audit_action",
                table: "ai_financial_audit_logs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_financial_audit_action",
                table: "ai_financial_audit_logs",
                sql: "action IN ('SetMonthlyLimit', 'ReconcileSettle', 'ReconcileRelease')");
        }
    }
}
