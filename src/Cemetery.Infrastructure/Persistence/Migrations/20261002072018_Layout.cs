using System;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Layout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cemeteries",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schematic = table.Column<bool>(type: "boolean", nullable: false),
                    plan_image_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    plan_bounds = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cemeteries", x => x.id);
                    table.UniqueConstraint("ak_cemeteries_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "sections",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    outline = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sections", x => x.id);
                    table.UniqueConstraint("ak_sections_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_sections_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grave_rows",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grave_rows", x => x.id);
                    table.UniqueConstraint("ak_grave_rows_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_grave_rows_sections_tenant_id_section_id",
                        columns: x => new { x.tenant_id, x.section_id },
                        principalSchema: "cemetery",
                        principalTable: "sections",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grave_sites",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    outline = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: true),
                    closed = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grave_sites", x => x.id);
                    table.ForeignKey(
                        name: "fk_grave_sites_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grave_sites_grave_rows_tenant_id_row_id",
                        columns: x => new { x.tenant_id, x.row_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_rows",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grave_sites_sections_tenant_id_section_id",
                        columns: x => new { x.tenant_id, x.section_id },
                        principalSchema: "cemetery",
                        principalTable: "sections",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cemeteries_tenant_id",
                schema: "cemetery",
                table: "cemeteries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "grave_rows_tenant_label_key",
                schema: "cemetery",
                table: "grave_rows",
                columns: new[] { "tenant_id", "section_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "grave_sites_tenant_code_key",
                schema: "cemetery",
                table: "grave_sites",
                columns: new[] { "tenant_id", "cemetery_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grave_sites_outline",
                schema: "cemetery",
                table: "grave_sites",
                column: "outline")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_grave_sites_tenant_id_row_id",
                schema: "cemetery",
                table: "grave_sites",
                columns: new[] { "tenant_id", "row_id" });

            migrationBuilder.CreateIndex(
                name: "ix_grave_sites_tenant_id_section_id",
                schema: "cemetery",
                table: "grave_sites",
                columns: new[] { "tenant_id", "section_id" });

            migrationBuilder.CreateIndex(
                name: "sections_tenant_code_key",
                schema: "cemetery",
                table: "sections",
                columns: new[] { "tenant_id", "cemetery_id", "code" },
                unique: true);

            migrationBuilder.Sql(RowLevelSecurity.Layout);
            migrationBuilder.Sql(GraveSiteGeometry.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS grave_sites_no_overlap ON cemetery.grave_sites; DROP FUNCTION IF EXISTS cemetery.grave_sites_reject_overlap();");
            migrationBuilder.DropTable(
                name: "grave_sites",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "grave_rows",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "sections",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "cemeteries",
                schema: "cemetery");
        }
    }
}
