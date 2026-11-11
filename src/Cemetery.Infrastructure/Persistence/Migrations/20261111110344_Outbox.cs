using System;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Outbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempts",
                schema: "cemetery",
                table: "webhook_deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "http_status",
                schema: "cemetery",
                table: "webhook_deliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                schema: "cemetery",
                table: "webhook_deliveries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_transaction_id",
                schema: "cemetery",
                table: "payments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "payments_provider_transaction_key",
                schema: "cemetery",
                table: "payments",
                columns: new[] { "tenant_id", "provider_transaction_id" },
                unique: true,
                filter: "provider_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at_occurred_at",
                schema: "cemetery",
                table: "outbox_messages",
                columns: new[] { "processed_at", "occurred_at" });

            migrationBuilder.Sql(RowLevelSecurity.Outbox);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "cemetery");

            migrationBuilder.DropIndex(
                name: "payments_provider_transaction_key",
                schema: "cemetery",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "attempts",
                schema: "cemetery",
                table: "webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "http_status",
                schema: "cemetery",
                table: "webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "last_error",
                schema: "cemetery",
                table: "webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "provider_transaction_id",
                schema: "cemetery",
                table: "payments");
        }
    }
}
