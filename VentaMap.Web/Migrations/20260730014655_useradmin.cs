using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class useradmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanPublish",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanReport",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModerationStatus",
                table: "Publications",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportTrashSentAtUtc",
                table: "Publications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportWarningSentAtUtc",
                table: "Publications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrashedAtUtc",
                table: "Publications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardThreshold",
                table: "PublicationReports",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReporterUserId",
                table: "PublicationReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "PublicationReports",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "PublicationReports",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "PublicationReports",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicationReports_PublicationId_ReporterUserId",
                table: "PublicationReports",
                columns: new[] { "PublicationId", "ReporterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicationReports_ReporterUserId",
                table: "PublicationReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationReports_ReviewedByUserId",
                table: "PublicationReports",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PublicationReports_Users_ReporterUserId",
                table: "PublicationReports",
                column: "ReporterUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PublicationReports_Users_ReviewedByUserId",
                table: "PublicationReports",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PublicationReports_Users_ReporterUserId",
                table: "PublicationReports");

            migrationBuilder.DropForeignKey(
                name: "FK_PublicationReports_Users_ReviewedByUserId",
                table: "PublicationReports");

            migrationBuilder.DropIndex(
                name: "IX_PublicationReports_PublicationId_ReporterUserId",
                table: "PublicationReports");

            migrationBuilder.DropIndex(
                name: "IX_PublicationReports_ReporterUserId",
                table: "PublicationReports");

            migrationBuilder.DropIndex(
                name: "IX_PublicationReports_ReviewedByUserId",
                table: "PublicationReports");

            migrationBuilder.DropColumn(
                name: "CanPublish",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanReport",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "ReportTrashSentAtUtc",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "ReportWarningSentAtUtc",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "TrashedAtUtc",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "CountsTowardThreshold",
                table: "PublicationReports");

            migrationBuilder.DropColumn(
                name: "ReporterUserId",
                table: "PublicationReports");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "PublicationReports");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "PublicationReports");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "PublicationReports");

        }
    }
}
