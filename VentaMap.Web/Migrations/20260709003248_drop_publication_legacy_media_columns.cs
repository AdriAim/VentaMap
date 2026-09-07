using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VentaMap.Data;

#nullable disable

namespace VentaMap.Migrations
{
    [DbContext(typeof(VentaMapDbContext))]
    [Migration("20260709003248_drop_publication_legacy_media_columns")]
    public partial class drop_publication_legacy_media_columns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @sql = (
                    SELECT IF(
                        EXISTS (
                            SELECT 1
                            FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_SCHEMA = DATABASE()
                              AND TABLE_NAME = 'Publications'
                              AND COLUMN_NAME = 'ImagesCsv'
                        ),
                        'ALTER TABLE Publications DROP COLUMN ImagesCsv',
                        'SELECT 1'
                    )
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                    SELECT IF(
                        EXISTS (
                            SELECT 1
                            FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_SCHEMA = DATABASE()
                              AND TABLE_NAME = 'Publications'
                              AND COLUMN_NAME = 'VideoUrl'
                        ),
                        'ALTER TABLE Publications DROP COLUMN VideoUrl',
                        'SELECT 1'
                    )
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagesCsv",
                table: "Publications",
                type: "longtext",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Publications",
                type: "varchar(400)",
                maxLength: 400,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
