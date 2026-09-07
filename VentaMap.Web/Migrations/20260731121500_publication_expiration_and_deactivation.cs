using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VentaMap.Data;

#nullable disable

namespace VentaMap.Migrations
{
    [DbContext(typeof(VentaMapDbContext))]
    [Migration("20260731121500_publication_expiration_and_deactivation")]
    public partial class publication_expiration_and_deactivation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationNoticeSentAtUtc",
                table: "Publications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAtUtc",
                table: "Publications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Publications",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeactivationComment",
                table: "Publications",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE Publications
                SET ExpiresAtUtc = DATE_ADD(CreatedAtUtc, INTERVAL 30 DAY)
                WHERE ExpiresAtUtc IS NULL
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpirationNoticeSentAtUtc",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "DeactivatedAtUtc",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "DeactivationComment",
                table: "Publications");
        }
    }
}
