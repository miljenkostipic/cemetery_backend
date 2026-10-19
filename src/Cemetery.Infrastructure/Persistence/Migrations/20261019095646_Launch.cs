using System;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Launch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gdpr_exports",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gdpr_exports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matica_links",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    death_certificate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matica_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(64)", maxLength: 64, nullable: false),
                    secret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_subscriptions", x => x.id);
                    table.UniqueConstraint("ak_webhook_subscriptions_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_webhook_deliveries_webhook_subscriptions_tenant_id_subscrip",
                        columns: x => new { x.tenant_id, x.subscription_id },
                        principalSchema: "cemetery",
                        principalTable: "webhook_subscriptions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gdpr_exports_tenant_id_requested_at",
                schema: "cemetery",
                table: "gdpr_exports",
                columns: new[] { "tenant_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_matica_links_tenant_id_cemetery_id",
                schema: "cemetery",
                table: "matica_links",
                columns: new[] { "tenant_id", "cemetery_id" });

            migrationBuilder.CreateIndex(
                name: "matica_links_certificate_key",
                schema: "cemetery",
                table: "matica_links",
                columns: new[] { "tenant_id", "death_certificate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "matica_links_interment_key",
                schema: "cemetery",
                table: "matica_links",
                columns: new[] { "tenant_id", "interment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_tenant_id_subscription_id",
                schema: "cemetery",
                table: "webhook_deliveries",
                columns: new[] { "tenant_id", "subscription_id" });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_subscriptions_tenant_id_event",
                schema: "cemetery",
                table: "webhook_subscriptions",
                columns: new[] { "tenant_id", "event" });

            migrationBuilder.Sql(RowLevelSecurity.Launch);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gdpr_exports",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "matica_links",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "webhook_deliveries",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions",
                schema: "cemetery");
        }
    }
}
