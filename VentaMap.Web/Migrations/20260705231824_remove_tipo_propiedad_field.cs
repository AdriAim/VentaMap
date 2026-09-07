using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class remove_tipo_propiedad_field : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE pfv
                FROM PublicationFieldValues pfv
                INNER JOIN PublicationCategoryFields pcf ON pcf.Id = pfv.CategoryFieldId
                WHERE pcf.InternalName = 'tipo_propiedad'
                """);

            migrationBuilder.Sql("""
                DELETE FROM PublicationCategoryFields
                WHERE InternalName = 'tipo_propiedad'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
