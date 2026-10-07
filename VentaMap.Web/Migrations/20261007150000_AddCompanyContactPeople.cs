using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VentaMap.Data;

#nullable disable

namespace VentaMap.Migrations;

[DbContext(typeof(VentaMapDbContext))]
[Migration("20261007150000_AddCompanyContactPeople")]
public partial class AddCompanyContactPeople : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CompanyContactName", table: "Users", type: "varchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CompanyContactName2", table: "Users", type: "varchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CompanyContactPhone2", table: "Users", type: "varchar(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CompanyContactName3", table: "Users", type: "varchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CompanyContactPhone3", table: "Users", type: "varchar(32)", maxLength: 32, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CompanyContactName", table: "Users");
        migrationBuilder.DropColumn(name: "CompanyContactName2", table: "Users");
        migrationBuilder.DropColumn(name: "CompanyContactPhone2", table: "Users");
        migrationBuilder.DropColumn(name: "CompanyContactName3", table: "Users");
        migrationBuilder.DropColumn(name: "CompanyContactPhone3", table: "Users");
    }
}
