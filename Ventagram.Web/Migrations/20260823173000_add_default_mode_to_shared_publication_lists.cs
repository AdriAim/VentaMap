using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ventagram.Migrations;

public partial class add_default_mode_to_shared_publication_lists : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DefaultMode",
            table: "SharedPublicationLists",
            type: "varchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Galeria")
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DefaultMode",
            table: "SharedPublicationLists");
    }
}
