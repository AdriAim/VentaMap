using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class add_publication_category_field_display_and_example : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputExample",
                table: "PublicationCategoryFields",
                type: "varchar(180)",
                maxLength: 180,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "ShowInBasicData",
                table: "PublicationCategoryFields",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE PublicationCategoryFields
                SET ShowInBasicData = Required;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputExample",
                table: "PublicationCategoryFields");

            migrationBuilder.DropColumn(
                name: "ShowInBasicData",
                table: "PublicationCategoryFields");
        }
    }
}
