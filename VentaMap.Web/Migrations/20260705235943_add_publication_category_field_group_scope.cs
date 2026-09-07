using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class add_publication_category_field_group_scope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PublicationCategoryFields_PublicationCategories_CategoryId",
                table: "PublicationCategoryFields");

            migrationBuilder.DropIndex(
                name: "IX_PublicationCategoryFields_CategoryId_InternalName",
                table: "PublicationCategoryFields");

            migrationBuilder.DropIndex(
                name: "IX_PublicationCategoryFields_CategoryId_IsActive_SortOrder",
                table: "PublicationCategoryFields");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "PublicationCategoryFields",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<byte>(
                name: "GroupId",
                table: "PublicationCategoryFields",
                type: "tinyint unsigned",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE PublicationCategoryFields cf
                INNER JOIN PublicationCategories c ON c.Id = cf.CategoryId
                SET cf.GroupId = c.`Group`,
                    cf.CategoryId = NULL
                WHERE cf.CategoryId IS NOT NULL AND cf.GroupId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PublicationCategoryFields_CategoryId",
                table: "PublicationCategoryFields",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationCategoryFields_GroupId_CategoryId_InternalName",
                table: "PublicationCategoryFields",
                columns: new[] { "GroupId", "CategoryId", "InternalName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicationCategoryFields_GroupId_CategoryId_IsActive_SortOr~",
                table: "PublicationCategoryFields",
                columns: new[] { "GroupId", "CategoryId", "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_PublicationCategoryFields_PublicationCategories_CategoryId",
                table: "PublicationCategoryFields",
                column: "CategoryId",
                principalTable: "PublicationCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PublicationCategoryFields_PublicationCategories_CategoryId",
                table: "PublicationCategoryFields");

            migrationBuilder.DropIndex(
                name: "IX_PublicationCategoryFields_CategoryId",
                table: "PublicationCategoryFields");

            migrationBuilder.DropIndex(
                name: "IX_PublicationCategoryFields_GroupId_CategoryId_InternalName",
                table: "PublicationCategoryFields");

            migrationBuilder.DropIndex(
                name: "IX_PublicationCategoryFields_GroupId_CategoryId_IsActive_SortOr~",
                table: "PublicationCategoryFields");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "PublicationCategoryFields");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "PublicationCategoryFields",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicationCategoryFields_CategoryId_InternalName",
                table: "PublicationCategoryFields",
                columns: new[] { "CategoryId", "InternalName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicationCategoryFields_CategoryId_IsActive_SortOrder",
                table: "PublicationCategoryFields",
                columns: new[] { "CategoryId", "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_PublicationCategoryFields_PublicationCategories_CategoryId",
                table: "PublicationCategoryFields",
                column: "CategoryId",
                principalTable: "PublicationCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
