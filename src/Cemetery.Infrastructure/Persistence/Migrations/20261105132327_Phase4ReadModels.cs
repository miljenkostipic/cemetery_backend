using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4ReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "charges_year_key",
                schema: "cemetery",
                table: "charges");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "cemetery",
                table: "sections",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "redirect_url",
                schema: "cemetery",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "holder_id",
                schema: "cemetery",
                table: "monument_permits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "rest_until",
                schema: "cemetery",
                table: "grave_sites",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "cemetery",
                table: "grave_sites",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "available");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "cemetery",
                table: "grave_sites",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "used",
                schema: "cemetery",
                table: "grave_sites",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "holder_id",
                schema: "cemetery",
                table: "charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "entrances",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entrances", x => x.id);
                    table.ForeignKey(
                        name: "fk_entrances_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grave_rights",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grave_rights", x => x.id);
                    table.ForeignKey(
                        name: "fk_grave_rights_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "holders",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    given_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    family_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    oib = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holders", x => x.id);
                    table.UniqueConstraint("ak_holders_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "paths",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cemetery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    line = table.Column<LineString>(type: "geometry(LineString,4326)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paths", x => x.id);
                    table.ForeignKey(
                        name: "fk_paths_cemeteries_tenant_id_cemetery_id",
                        columns: x => new { x.tenant_id, x.cemetery_id },
                        principalSchema: "cemetery",
                        principalTable: "cemeteries",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "site_documents",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_right_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    file_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    byte_size = table.Column<int>(type: "integer", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_site_documents_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stored_files",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grave_right_shares",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    holder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    percent = table.Column<int>(type: "integer", nullable: false),
                    grave_right_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grave_right_shares", x => x.id);
                    table.ForeignKey(
                        name: "fk_grave_right_shares_grave_rights_grave_right_id",
                        column: x => x.grave_right_id,
                        principalSchema: "cemetery",
                        principalTable: "grave_rights",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "cemetery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grave_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    holder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    until = table.Column<DateOnly>(type: "date", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservations", x => x.id);
                    table.ForeignKey(
                        name: "fk_reservations_grave_sites_tenant_id_grave_site_id",
                        columns: x => new { x.tenant_id, x.grave_site_id },
                        principalSchema: "cemetery",
                        principalTable: "grave_sites",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reservations_holders_tenant_id_holder_id",
                        columns: x => new { x.tenant_id, x.holder_id },
                        principalSchema: "cemetery",
                        principalTable: "holders",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "charges_holder_year_key",
                schema: "cemetery",
                table: "charges",
                columns: new[] { "tenant_id", "cemetery_id", "grave_site_id", "fee_schedule_id", "year", "holder_id" },
                unique: true,
                filter: "holder_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "charges_year_key",
                schema: "cemetery",
                table: "charges",
                columns: new[] { "tenant_id", "cemetery_id", "grave_site_id", "fee_schedule_id", "year" },
                unique: true,
                filter: "holder_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_entrances_location",
                schema: "cemetery",
                table: "entrances",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_entrances_tenant_id_cemetery_id",
                schema: "cemetery",
                table: "entrances",
                columns: new[] { "tenant_id", "cemetery_id" });

            migrationBuilder.CreateIndex(
                name: "ix_grave_right_shares_grave_right_id_holder_id",
                schema: "cemetery",
                table: "grave_right_shares",
                columns: new[] { "grave_right_id", "holder_id" });

            migrationBuilder.CreateIndex(
                name: "grave_rights_one_active",
                schema: "cemetery",
                table: "grave_rights",
                columns: new[] { "tenant_id", "grave_site_id" },
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "holders_email_key",
                schema: "cemetery",
                table: "holders",
                columns: new[] { "tenant_id", "email" },
                unique: true,
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_holders_tenant_id_family_name_given_name_id",
                schema: "cemetery",
                table: "holders",
                columns: new[] { "tenant_id", "family_name", "given_name", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_paths_tenant_id_cemetery_id",
                schema: "cemetery",
                table: "paths",
                columns: new[] { "tenant_id", "cemetery_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_tenant_id_holder_id",
                schema: "cemetery",
                table: "reservations",
                columns: new[] { "tenant_id", "holder_id" });

            migrationBuilder.CreateIndex(
                name: "reservations_one_active",
                schema: "cemetery",
                table: "reservations",
                columns: new[] { "tenant_id", "grave_site_id" },
                unique: true,
                filter: "active");

            migrationBuilder.CreateIndex(
                name: "ix_site_documents_tenant_id_grave_site_id",
                schema: "cemetery",
                table: "site_documents",
                columns: new[] { "tenant_id", "grave_site_id" });

            migrationBuilder.Sql(RowLevelSecurity.Rights);
            migrationBuilder.Sql(Phase4Sql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Phase4Sql.Down);
            migrationBuilder.DropTable(
                name: "entrances",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "grave_right_shares",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "paths",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "site_documents",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "stored_files",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "grave_rights",
                schema: "cemetery");

            migrationBuilder.DropTable(
                name: "holders",
                schema: "cemetery");

            migrationBuilder.DropIndex(
                name: "charges_holder_year_key",
                schema: "cemetery",
                table: "charges");

            migrationBuilder.DropIndex(
                name: "charges_year_key",
                schema: "cemetery",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "cemetery",
                table: "sections");

            migrationBuilder.DropColumn(
                name: "redirect_url",
                schema: "cemetery",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "holder_id",
                schema: "cemetery",
                table: "monument_permits");

            migrationBuilder.DropColumn(
                name: "rest_until",
                schema: "cemetery",
                table: "grave_sites");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "cemetery",
                table: "grave_sites");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "cemetery",
                table: "grave_sites");

            migrationBuilder.DropColumn(
                name: "used",
                schema: "cemetery",
                table: "grave_sites");

            migrationBuilder.DropColumn(
                name: "holder_id",
                schema: "cemetery",
                table: "charges");

            migrationBuilder.CreateIndex(
                name: "charges_year_key",
                schema: "cemetery",
                table: "charges",
                columns: new[] { "tenant_id", "cemetery_id", "grave_site_id", "fee_schedule_id", "year" },
                unique: true);
        }
    }
}
