using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class atributos_dinamicos_categoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicationCategoryFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    InternalName = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Label = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OptionsCsv = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationCategoryFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicationCategoryFields_PublicationCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PublicationCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PublicationFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PublicationId = table.Column<int>(type: "int", nullable: false),
                    CategoryFieldId = table.Column<int>(type: "int", nullable: false),
                    ValueText = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValueNumber = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValueBoolean = table.Column<bool>(type: "tinyint(1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicationFieldValues_PublicationCategoryFields_CategoryFie~",
                        column: x => x.CategoryFieldId,
                        principalTable: "PublicationCategoryFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicationFieldValues_Publications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "Publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationCategoryFields_CategoryId_InternalName",
                table: "PublicationCategoryFields",
                columns: new[] { "CategoryId", "InternalName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicationCategoryFields_CategoryId_IsActive_SortOrder",
                table: "PublicationCategoryFields",
                columns: new[] { "CategoryId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicationFieldValues_CategoryFieldId_ValueBoolean",
                table: "PublicationFieldValues",
                columns: new[] { "CategoryFieldId", "ValueBoolean" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicationFieldValues_CategoryFieldId_ValueNumber",
                table: "PublicationFieldValues",
                columns: new[] { "CategoryFieldId", "ValueNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicationFieldValues_CategoryFieldId_ValueText",
                table: "PublicationFieldValues",
                columns: new[] { "CategoryFieldId", "ValueText" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicationFieldValues_PublicationId_CategoryFieldId",
                table: "PublicationFieldValues",
                columns: new[] { "PublicationId", "CategoryFieldId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicationFieldValues");

            migrationBuilder.DropTable(
                name: "PublicationCategoryFields");
        }
    }
}
