using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VentaMap.Data;

#nullable disable

namespace VentaMap.Migrations;

[DbContext(typeof(VentaMapDbContext))]
[Migration("20261007170000_RemoveCompanyPublicationExpirations")]
public partial class RemoveCompanyPublicationExpirations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `Publications` AS p
            INNER JOIN `Users` AS u ON u.`Id` = p.`UserId`
            SET p.`ExpiresAtUtc` = NULL,
                p.`ExpirationNoticeSentAtUtc` = NULL
            WHERE u.`IsCompany` = TRUE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
