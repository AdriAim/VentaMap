using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class add_publication_hide_from_map : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HideFromMap",
                table: "Publications",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideFromMap",
                table: "Publications");
        }
    }
}
