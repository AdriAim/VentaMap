using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ventagram.Data;

#nullable disable

namespace Ventagram.Migrations;

[DbContext(typeof(VentagramDbContext))]
[Migration("20260905194500_add_user_message_bubble_color")]
public partial class add_user_message_bubble_color : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MessageBubbleColor",
            table: "Users",
            type: "varchar(12)",
            maxLength: 12,
            nullable: false,
            defaultValue: "rose")
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MessageBubbleColor",
            table: "Users");
    }
}
