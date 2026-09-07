using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class add_publication_category_field_unit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "PublicationCategoryFields",
                type: "varchar(24)",
                maxLength: 24,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE PublicationCategoryFields
                SET Unit = CASE InternalName
                    WHEN 'superficie_total_m2' THEN 'm2'
                    WHEN 'superficie_cubierta_m2' THEN 'm2'
                    WHEN 'antiguedad_anios' THEN 'anios'
                    WHEN 'expensas' THEN 'ARS'
                    WHEN 'anio' THEN 'anio'
                    WHEN 'kilometros' THEN 'km'
                    WHEN 'stock' THEN 'unid'
                    ELSE Unit
                END
                WHERE Unit IS NULL OR Unit = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "PublicationCategoryFields");
        }
    }
}
