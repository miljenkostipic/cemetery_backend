using System;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "office_requests",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_office_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_office_requests_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_office_requests_tenant_id_created_on",
                schema: "cemetery",
                table: "office_requests",
                columns: new[] { "tenant_id", "created_on" });

            migrationBuilder.CreateIndex(
                name: "ix_office_requests_tenant_id_grave_site_id",
                schema: "cemetery",
                table: "office_requests",
                columns: new[] { "tenant_id", "grave_site_id" });

            migrationBuilder.Sql(RowLevelSecurity.Requests);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "office_requests",
                schema: "cemetery");
        }
    }
}
