using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using VentaMap.Data;

#nullable disable

namespace VentaMap.Migrations
{
    [DbContext(typeof(VentaMapDbContext))]
    [Migration("20261003133000_AddPublicationCreationDevices")]
    public partial class AddPublicationCreationDevices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicationCreationDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PublicationId = table.Column<int>(type: "int", nullable: false),
                    DeviceType = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    OperatingSystem = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Browser = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationCreationDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicationCreationDevices_Publications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "Publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationCreationDevices_PublicationId",
                table: "PublicationCreationDevices",
                column: "PublicationId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PublicationCreationDevices");
        }
    }
}
