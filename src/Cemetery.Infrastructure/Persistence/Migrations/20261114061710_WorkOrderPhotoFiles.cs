using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cemetery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderPhotoFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "file_id",
                schema: "cemetery",
                table: "work_order_photos",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            // Existing photo bytes move into stored_files under the photo's own id, which DatabaseFileStorage
            // (and R2FileStorage's legacy path) reads back. Forced row-level security would hide every row
            // from a non-superuser owner with no tenant set, so it is lifted for the copy only.
            migrationBuilder.Sql("""
                ALTER TABLE cemetery.work_order_photos NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE cemetery.stored_files NO FORCE ROW LEVEL SECURITY;
                INSERT INTO cemetery.stored_files (id, tenant_id, content)
                SELECT id, tenant_id, content FROM cemetery.work_order_photos;
                UPDATE cemetery.work_order_photos SET file_id = id::text;
                ALTER TABLE cemetery.work_order_photos FORCE ROW LEVEL SECURITY;
                ALTER TABLE cemetery.stored_files FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropColumn(
                name: "content",
                schema: "cemetery",
                table: "work_order_photos");

            migrationBuilder.AlterColumn<string>(
                name: "storage_key",
                schema: "cemetery",
                table: "site_documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "thumb_file_id",
                schema: "cemetery",
                table: "grave_site_photos",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "full_file_id",
                schema: "cemetery",
                table: "grave_site_photos",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "content",
                schema: "cemetery",
                table: "work_order_photos",
                type: "bytea",
                nullable: false,
                defaultValue: Array.Empty<byte>());

            // Only photos still kept in the database can come back; R2 objects stay where they are.
            migrationBuilder.Sql("""
                ALTER TABLE cemetery.work_order_photos NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE cemetery.stored_files NO FORCE ROW LEVEL SECURITY;
                UPDATE cemetery.work_order_photos AS photo SET content = file.content
                FROM cemetery.stored_files AS file WHERE file.id::text = photo.file_id;
                DELETE FROM cemetery.stored_files AS file
                USING cemetery.work_order_photos AS photo WHERE file.id::text = photo.file_id;
                ALTER TABLE cemetery.work_order_photos FORCE ROW LEVEL SECURITY;
                ALTER TABLE cemetery.stored_files FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropColumn(
                name: "file_id",
                schema: "cemetery",
                table: "work_order_photos");

            migrationBuilder.AlterColumn<string>(
                name: "storage_key",
                schema: "cemetery",
                table: "site_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "thumb_file_id",
                schema: "cemetery",
                table: "grave_site_photos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "full_file_id",
                schema: "cemetery",
                table: "grave_site_photos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);
        }
    }
}
