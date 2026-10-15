using System;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Operations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chapel_bookings",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    place = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chapel_bookings", x => x.id);
                    table.ForeignKey(
                        name: "fk_chapel_bookings_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "funerals",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deceased_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    place = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funerals", x => x.id);
                    table.UniqueConstraint("ak_funerals_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_funerals_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_funerals_deceased_tenant_id_deceased_id",
                        columns: x => new { x.tenant_id, x.deceased_id },
                        principalSchema: "cemetery",
                        principalTable: "deceased",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_funerals_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "monument_permits",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    holder_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monument_permits", x => x.id);
                    table.UniqueConstraint("ak_monument_permits_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_monument_permits_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_monument_permits_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scheduled_on = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    permit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    completion_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_orders", x => x.id);
                    table.UniqueConstraint("ak_work_orders_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_work_orders_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_orders_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_orders_monument_permits_tenant_id_permit_id",
                        columns: x => new { x.tenant_id, x.permit_id },
                        principalSchema: "cemetery",
                        principalTable: "monument_permits",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_order_photos",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_order_photos_work_orders_tenant_id_work_order_id",
                        columns: x => new { x.tenant_id, x.work_order_id },
                        principalSchema: "cemetery",
                        principalTable: "work_orders",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chapel_bookings_tenant_id_cemetery_id_starts_at",
                schema: "cemetery",
                table: "chapel_bookings",
                columns: new[] { "tenant_id", "cemetery_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_funerals_tenant_id_cemetery_id_starts_at",
                schema: "cemetery",
                table: "funerals",
                columns: new[] { "tenant_id", "cemetery_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_funerals_tenant_id_deceased_id",
                schema: "cemetery",
                table: "funerals",
                columns: new[] { "tenant_id", "deceased_id" });

            migrationBuilder.CreateIndex(
                name: "ix_funerals_tenant_id_grave_site_id",
                schema: "cemetery",
                table: "funerals",
                columns: new[] { "tenant_id", "grave_site_id" });

            migrationBuilder.CreateIndex(
                name: "ix_monument_permits_tenant_id_cemetery_id_grave_site_id",
                schema: "cemetery",
                table: "monument_permits",
                columns: new[] { "tenant_id", "cemetery_id", "grave_site_id" });

            migrationBuilder.CreateIndex(
                name: "ix_monument_permits_tenant_id_grave_site_id",
                schema: "cemetery",
                table: "monument_permits",
                columns: new[] { "tenant_id", "grave_site_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_order_photos_tenant_id_work_order_id",
                schema: "cemetery",
                table: "work_order_photos",
                columns: new[] { "tenant_id", "work_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_id_cemetery_id",
                schema: "cemetery",
                table: "work_orders",
                columns: new[] { "tenant_id", "cemetery_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_id_grave_site_id",
                schema: "cemetery",
                table: "work_orders",
                columns: new[] { "tenant_id", "grave_site_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_id_permit_id",
                schema: "cemetery",
                table: "work_orders",
                columns: new[] { "tenant_id", "permit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_id_scheduled_on_status",
                schema: "cemetery",
                table: "work_orders",
                columns: new[] { "tenant_id", "scheduled_on", "status" });

            migrationBuilder.Sql(RowLevelSecurity.Operations);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chapel_bookings",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "funerals",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "work_order_photos",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "work_orders",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "monument_permits",
                schema: "cemetery");
        }
    }
}
