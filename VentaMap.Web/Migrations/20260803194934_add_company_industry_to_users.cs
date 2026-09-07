using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class add_company_industry_to_users : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddNullableVarcharColumnIfMissing(migrationBuilder, "Users", "CompanyIndustry", 120);
            AddNullableVarcharColumnIfMissing(migrationBuilder, "Users", "CompanyLogoUrl", 260);
            AddNullableVarcharColumnIfMissing(migrationBuilder, "Users", "CompanyName", 160);
            AddNullableVarcharColumnIfMissing(migrationBuilder, "Users", "CompanySlug", 180);
            AddNullableVarcharColumnIfMissing(migrationBuilder, "Users", "CompanyTagline", 180);
            AddBoolColumnIfMissing(migrationBuilder, "Users", "IsCompany", defaultValue: false);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS `SiteSuggestions` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `UserId` int NULL,
                    `SenderName` varchar(120) CHARACTER SET utf8mb4 NULL,
                    `SenderEmail` varchar(160) CHARACTER SET utf8mb4 NULL,
                    `Message` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    CONSTRAINT `PK_SiteSuggestions` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_SiteSuggestions_Users_UserId`
                        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL
                ) CHARACTER SET=utf8mb4;
                """);

            CreateUniqueIndexIfMissing(migrationBuilder, "Users", "IX_Users_CompanyName", "`CompanyName`");
            CreateUniqueIndexIfMissing(migrationBuilder, "Users", "IX_Users_CompanySlug", "`CompanySlug`");
            CreateIndexIfMissing(migrationBuilder, "SiteSuggestions", "IX_SiteSuggestions_CreatedAtUtc", "`CreatedAtUtc`");
            CreateIndexIfMissing(migrationBuilder, "SiteSuggestions", "IX_SiteSuggestions_UserId", "`UserId`");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "SiteSuggestions", "IX_SiteSuggestions_UserId");
            DropIndexIfExists(migrationBuilder, "SiteSuggestions", "IX_SiteSuggestions_CreatedAtUtc");
            DropIndexIfExists(migrationBuilder, "Users", "IX_Users_CompanySlug");
            DropIndexIfExists(migrationBuilder, "Users", "IX_Users_CompanyName");

            migrationBuilder.Sql("DROP TABLE IF EXISTS `SiteSuggestions`;");

            DropColumnIfExists(migrationBuilder, "Users", "IsCompany");
            DropColumnIfExists(migrationBuilder, "Users", "CompanyTagline");
            DropColumnIfExists(migrationBuilder, "Users", "CompanySlug");
            DropColumnIfExists(migrationBuilder, "Users", "CompanyName");
            DropColumnIfExists(migrationBuilder, "Users", "CompanyLogoUrl");
            DropColumnIfExists(migrationBuilder, "Users", "CompanyIndustry");
        }

        private static void AddNullableVarcharColumnIfMissing(MigrationBuilder migrationBuilder, string tableName, string columnName, int length)
        {
            migrationBuilder.Sql(
                $"""
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = '{tableName}'
                      AND COLUMN_NAME = '{columnName}'
                );
                SET @sql = IF(
                    @column_exists = 0,
                    'ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` varchar({length}) CHARACTER SET utf8mb4 NULL;',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        private static void AddBoolColumnIfMissing(MigrationBuilder migrationBuilder, string tableName, string columnName, bool defaultValue)
        {
            var mysqlDefaultValue = defaultValue ? "1" : "0";

            migrationBuilder.Sql(
                $"""
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = '{tableName}'
                      AND COLUMN_NAME = '{columnName}'
                );
                SET @sql = IF(
                    @column_exists = 0,
                    'ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` tinyint(1) NOT NULL DEFAULT {mysqlDefaultValue};',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        private static void CreateUniqueIndexIfMissing(MigrationBuilder migrationBuilder, string tableName, string indexName, string columnsSql)
        {
            CreateIndexIfMissing(migrationBuilder, tableName, indexName, columnsSql, unique: true);
        }

        private static void CreateIndexIfMissing(MigrationBuilder migrationBuilder, string tableName, string indexName, string columnsSql, bool unique = false)
        {
            var createKeyword = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";

            migrationBuilder.Sql(
                $"""
                SET @index_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = '{tableName}'
                      AND INDEX_NAME = '{indexName}'
                );
                SET @sql = IF(
                    @index_exists = 0,
                    '{createKeyword} `{indexName}` ON `{tableName}` ({columnsSql});',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string tableName, string columnName)
        {
            migrationBuilder.Sql(
                $"""
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = '{tableName}'
                      AND COLUMN_NAME = '{columnName}'
                );
                SET @sql = IF(
                    @column_exists = 1,
                    'ALTER TABLE `{tableName}` DROP COLUMN `{columnName}`;',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string tableName, string indexName)
        {
            migrationBuilder.Sql(
                $"""
                SET @index_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = '{tableName}'
                      AND INDEX_NAME = '{indexName}'
                );
                SET @sql = IF(
                    @index_exists = 1,
                    'DROP INDEX `{indexName}` ON `{tableName}`;',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }
    }
}
