using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ventagram.Migrations;

public partial class add_user_header_publication_groups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "HeaderPublicationGroupsCsv",
            table: "Users",
            type: "varchar(120)",
            maxLength: 120,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HeaderPublicationGroupsCsv",
            table: "Users");
    }
}
