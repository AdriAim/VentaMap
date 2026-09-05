using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ventagram.Data;

#nullable disable

namespace Ventagram.Migrations
{
    [DbContext(typeof(VentagramDbContext))]
    [Migration("20260904120000_limit_publication_short_description")]
    /// <inheritdoc />
    public partial class limit_publication_short_description : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE `Publications`
                SET `ShortDescription` = LEFT(`ShortDescription`, 60)
                WHERE CHAR_LENGTH(`ShortDescription`) > 60;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ShortDescription",
                table: "Publications",
                type: "varchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(260)",
                oldMaxLength: 260);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ShortDescription",
                table: "Publications",
                type: "varchar(260)",
                maxLength: 260,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(60)",
                oldMaxLength: 60);
        }
    }
}
