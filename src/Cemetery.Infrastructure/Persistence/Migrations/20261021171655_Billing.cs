using System;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Billing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fee_schedules",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fee_schedules", x => x.id);
                    table.UniqueConstraint("ak_fee_schedules_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_fee_schedules_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchant_accounts",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    merchant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    secret = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charges",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    holder_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    latest_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charges", x => x.id);
                    table.UniqueConstraint("ak_charges_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_charges_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_charges_fee_schedules_tenant_id_fee_schedule_id",
                        columns: x => new { x.tenant_id, x.fee_schedule_id },
                        principalSchema: "cemetery",
                        principalTable: "fee_schedules",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_charges_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "charges_year_key",
                schema: "cemetery",
                table: "charges",
                columns: new[] { "tenant_id", "cemetery_id", "grave_site_id", "fee_schedule_id", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_charges_tenant_id_fee_schedule_id",
                schema: "cemetery",
                table: "charges",
                columns: new[] { "tenant_id", "fee_schedule_id" });

            migrationBuilder.CreateIndex(
                name: "ix_charges_tenant_id_grave_site_id",
                schema: "cemetery",
                table: "charges",
                columns: new[] { "tenant_id", "grave_site_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fee_schedules_tenant_id_cemetery_id",
                schema: "cemetery",
                table: "fee_schedules",
                columns: new[] { "tenant_id", "cemetery_id" });

            migrationBuilder.CreateIndex(
                name: "merchant_accounts_provider_key",
                schema: "cemetery",
                table: "merchant_accounts",
                columns: new[] { "tenant_id", "provider" },
                unique: true);

            migrationBuilder.Sql(RowLevelSecurity.Billing);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charges",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "merchant_accounts",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "fee_schedules",
                schema: "cemetery");
        }
    }
}
