using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "hide_recent_deaths_days",
                schema: "cemetery",
                table: "organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "publishes_register",
                schema: "cemetery",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "hidden_from_public",
                schema: "cemetery",
                table: "grave_sites",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "epitaph",
                schema: "cemetery",
                table: "deceased",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                schema: "cemetery",
                table: "deceased",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.Sql(Cemetery.Infrastructure.Persistence.PublicCatalogSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Cemetery.Infrastructure.Persistence.PublicCatalogSql.Down);

            migrationBuilder.DropColumn(
                name: "hide_recent_deaths_days",
                schema: "cemetery",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "publishes_register",
                schema: "cemetery",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "hidden_from_public",
                schema: "cemetery",
                table: "grave_sites");

            migrationBuilder.DropColumn(
                name: "epitaph",
                schema: "cemetery",
                table: "deceased");

            migrationBuilder.DropColumn(
                name: "photo_url",
                schema: "cemetery",
                table: "deceased");
        }
    }
}
