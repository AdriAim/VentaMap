using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ventagram.Migrations
{
    public partial class move_operation_to_publication : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Publications'
                      AND COLUMN_NAME = 'OperationType'
                );
                SET @sql = IF(
                    @column_exists = 0,
                    'ALTER TABLE `Publications` ADD COLUMN `OperationType` tinyint unsigned NULL;',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql(
                """
                UPDATE `Publications` p
                INNER JOIN `PublicationFieldValues` pfv ON pfv.`PublicationId` = p.`Id`
                INNER JOIN `PublicationCategoryFields` pcf ON pcf.`Id` = pfv.`CategoryFieldId`
                SET p.`OperationType` = CASE LOWER(TRIM(COALESCE(pfv.`ValueText`, '')))
                    WHEN 'venta' THEN 1
                    WHEN 'alquiler' THEN 2
                    WHEN 'temporario' THEN 3
                    WHEN 'permuta' THEN 4
                    WHEN 'financiacion' THEN 5
                    ELSE p.`OperationType`
                END
                WHERE pcf.`InternalName` = 'operacion'
                  AND p.`OperationType` IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE `Publications` p
                SET p.`OperationType` = CASE
                    WHEN p.`OperationType` IS NOT NULL THEN p.`OperationType`
                    WHEN LOWER(CONCAT_WS(' ', COALESCE(p.`Title`, ''), COALESCE(p.`ShortDescription`, ''), COALESCE(p.`LongDescription`, ''))) LIKE '%tempor%' THEN 3
                    WHEN LOWER(CONCAT_WS(' ', COALESCE(p.`Title`, ''), COALESCE(p.`ShortDescription`, ''), COALESCE(p.`LongDescription`, ''))) LIKE '%alquiler%' THEN 2
                    WHEN LOWER(CONCAT_WS(' ', COALESCE(p.`Title`, ''), COALESCE(p.`ShortDescription`, ''), COALESCE(p.`LongDescription`, ''))) LIKE '%financi%' THEN 5
                    WHEN LOWER(CONCAT_WS(' ', COALESCE(p.`Title`, ''), COALESCE(p.`ShortDescription`, ''), COALESCE(p.`LongDescription`, ''))) LIKE '%permut%' THEN 4
                    ELSE 1
                END
                WHERE p.`OperationType` IS NULL;
                """);

            migrationBuilder.Sql(
                """
                DELETE pfv
                FROM `PublicationFieldValues` pfv
                INNER JOIN `PublicationCategoryFields` pcf ON pcf.`Id` = pfv.`CategoryFieldId`
                WHERE pcf.`InternalName` = 'operacion';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Publications'
                      AND COLUMN_NAME = 'OperationType'
                );
                SET @sql = IF(
                    @column_exists = 1,
                    'ALTER TABLE `Publications` DROP COLUMN `OperationType`;',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }
    }
}
