using System;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GraveSitePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grave_site_photos",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_file_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    thumb_file_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    taken_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_cover = table.Column<bool>(type: "boolean", nullable: false),
                    show_on_memorial = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grave_site_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_grave_site_photos_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grave_site_photos_tenant_id_grave_site_id",
                schema: "cemetery",
                table: "grave_site_photos",
                columns: new[] { "tenant_id", "grave_site_id" });

            migrationBuilder.Sql(RowLevelSecurity.Photos);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grave_site_photos",
                schema: "cemetery");
        }
    }
}
