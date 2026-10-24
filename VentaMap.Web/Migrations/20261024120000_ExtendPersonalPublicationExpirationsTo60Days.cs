using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VentaMap.Data;

#nullable disable

namespace VentaMap.Migrations;

[DbContext(typeof(VentaMapDbContext))]
[Migration("20261024120000_ExtendPersonalPublicationExpirationsTo60Days")]
public partial class ExtendPersonalPublicationExpirationsTo60Days : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT IGNORE INTO `VentaMapParameters`
                (`Key`, `Value`, `DataType`, `Description`, `UpdatedAtUtc`, `UpdatedByUserId`)
            VALUES
                ('Publications.Personal.DurationDays', '60', 'Integer', 'Dias de vigencia de los anuncios publicados por personas.', UTC_TIMESTAMP(6), NULL);
            """);

        migrationBuilder.Sql("""
            UPDATE `Publications` AS p
            LEFT JOIN `Users` AS u ON u.`Id` = p.`UserId`
            SET p.`ExpiresAtUtc` = DATE_ADD(p.`CreatedAtUtc`, INTERVAL 60 DAY),
                p.`ExpirationNoticeSentAtUtc` = NULL
            WHERE p.`IsActive` = TRUE
              AND (u.`Id` IS NULL OR u.`IsCompany` = FALSE);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
