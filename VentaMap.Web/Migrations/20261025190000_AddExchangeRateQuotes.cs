using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VentaMap.Data;
#nullable disable
namespace VentaMap.Migrations;
[DbContext(typeof(VentaMapDbContext))]
[Migration("20261025190000_AddExchangeRateQuotes")]
public partial class AddExchangeRateQuotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS `ExchangeRateQuotes` (
                `Id` bigint NOT NULL AUTO_INCREMENT,
                `Rate` decimal(18,4) NOT NULL,
                `Source` varchar(30) NOT NULL,
                `ObtainedAtUtc` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`),
                INDEX `IX_ExchangeRateQuotes_ObtainedAtUtc` (`ObtainedAtUtc`)
            ) CHARACTER SET utf8mb4;
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
