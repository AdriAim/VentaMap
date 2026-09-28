using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class TrackPublicationRepublications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalCreatedAtUtc",
                table: "Publications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PublicationRepublications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PublicationId = table.Column<int>(type: "int", nullable: false),
                    RepublishedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationRepublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicationRepublications_Publications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "Publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");




            migrationBuilder.CreateIndex(
                name: "IX_PublicationRepublications_PublicationId",
                table: "PublicationRepublications",
                column: "PublicationId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationRepublications_RepublishedAtUtc",
                table: "PublicationRepublications",
                column: "RepublishedAtUtc");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicationRepublications");

            migrationBuilder.DropColumn(
                name: "OriginalCreatedAtUtc",
                table: "Publications");
        }
    }
}
