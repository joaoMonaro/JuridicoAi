using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JuridicoAI.Api.src.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class TrackAiCacheWrites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_usage_reservations_prices",
                table: "ai_usage_reservations");

            migrationBuilder.AddColumn<int>(
                name: "actual_cache_write_tokens",
                table: "ai_usage_reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cache_write_cost_per_million_usd",
                table: "ai_usage_reservations",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "cache_write_tokens",
                table: "ai_usage_ledger",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_usage_reservations_prices",
                table: "ai_usage_reservations",
                sql: "input_cost_per_million_usd >= 0 AND cached_input_cost_per_million_usd >= 0 AND output_cost_per_million_usd >= 0 AND cache_write_cost_per_million_usd >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_usage_reservations_prices",
                table: "ai_usage_reservations");

            migrationBuilder.DropColumn(
                name: "actual_cache_write_tokens",
                table: "ai_usage_reservations");

            migrationBuilder.DropColumn(
                name: "cache_write_cost_per_million_usd",
                table: "ai_usage_reservations");

            migrationBuilder.DropColumn(
                name: "cache_write_tokens",
                table: "ai_usage_ledger");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_usage_reservations_prices",
                table: "ai_usage_reservations",
                sql: "input_cost_per_million_usd >= 0 AND cached_input_cost_per_million_usd >= 0 AND output_cost_per_million_usd >= 0");
        }
    }
}
