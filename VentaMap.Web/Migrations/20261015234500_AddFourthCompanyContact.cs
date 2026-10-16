using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VentaMap.Data;

#nullable disable

namespace VentaMap.Migrations;

[DbContext(typeof(VentaMapDbContext))]
[Migration("20261015234500_AddFourthCompanyContact")]
public partial class AddFourthCompanyContact : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CompanyContactName4", table: "Users", type: "varchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CompanyContactPhone4", table: "Users", type: "varchar(32)", maxLength: 32, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CompanyContactName4", table: "Users");
        migrationBuilder.DropColumn(name: "CompanyContactPhone4", table: "Users");
    }
}
