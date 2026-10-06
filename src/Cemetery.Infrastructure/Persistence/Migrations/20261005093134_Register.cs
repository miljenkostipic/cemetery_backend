using System;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Register : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rest_period_years",
                schema: "cemetery",
                table: "cemeteries",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_grave_sites_tenant_id_id",
                schema: "cemetery",
                table: "grave_sites",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deceased",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    given_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    family_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    born_on = table.Column<DateOnly>(type: "date", nullable: true),
                    died_on = table.Column<DateOnly>(type: "date", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deceased", x => x.id);
                    table.UniqueConstraint("ak_deceased_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_deceased_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deceased_revisions",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deceased_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    given_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    family_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    born_on = table.Column<DateOnly>(type: "date", nullable: true),
                    died_on = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deceased_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_deceased_revisions_deceased_tenant_id_deceased_id",
                        columns: x => new { x.tenant_id, x.deceased_id },
                        principalSchema: "cemetery",
                        principalTable: "deceased",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interments",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deceased_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    buried_on = table.Column<DateOnly>(type: "date", nullable: false),
                    exhumed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    transferred_on = table.Column<DateOnly>(type: "date", nullable: true),
                    superseded_on = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interments", x => x.id);
                    table.ForeignKey(
                        name: "fk_interments_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interments_deceased_tenant_id_deceased_id",
                        columns: x => new { x.tenant_id, x.deceased_id },
                        principalSchema: "cemetery",
                        principalTable: "deceased",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interments_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_tenant_id_subject_id",
                schema: "cemetery",
                table: "audit_entries",
                columns: new[] { "tenant_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_deceased_tenant_id_cemetery_id",
                schema: "cemetery",
                table: "deceased",
                columns: new[] { "tenant_id", "cemetery_id" });

            migrationBuilder.CreateIndex(
                name: "deceased_revisions_version_key",
                schema: "cemetery",
                table: "deceased_revisions",
                columns: new[] { "tenant_id", "deceased_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "interments_open_deceased_key",
                schema: "cemetery",
                table: "interments",
                columns: new[] { "tenant_id", "deceased_id" },
                unique: true,
                filter: "exhumed_on IS NULL AND transferred_on IS NULL AND superseded_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "interments_open_position_key",
                schema: "cemetery",
                table: "interments",
                columns: new[] { "tenant_id", "grave_site_id", "position" },
                unique: true,
                filter: "exhumed_on IS NULL AND transferred_on IS NULL AND superseded_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_interments_tenant_id_cemetery_id",
                schema: "cemetery",
                table: "interments",
                columns: new[] { "tenant_id", "cemetery_id" });

            migrationBuilder.Sql(RowLevelSecurity.Register);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "deceased_revisions",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "interments",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "deceased",
                schema: "cemetery");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_grave_sites_tenant_id_id",
                schema: "cemetery",
                table: "grave_sites");

            migrationBuilder.DropColumn(
                name: "rest_period_years",
                schema: "cemetery",
                table: "cemeteries");
        }
    }
}
