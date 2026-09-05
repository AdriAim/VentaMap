using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ventagram.Migrations
{
    /// <inheritdoc />
    public partial class add_publication_views : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddIntColumnIfMissing(migrationBuilder, "Publications", "UniqueViewCount", defaultValue: 0);
            AddIntColumnIfMissing(migrationBuilder, "Publications", "UniqueFavoriteCount", defaultValue: 0);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS `PublicationFavorites` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `PublicationId` int NOT NULL,
                    `UserId` int NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    CONSTRAINT `PK_PublicationFavorites` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_PublicationFavorites_Publications_PublicationId`
                        FOREIGN KEY (`PublicationId`) REFERENCES `Publications` (`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_PublicationFavorites_Users_UserId`
                        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS `PublicationViews` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `PublicationId` int NOT NULL,
                    `ViewerUserId` int NULL,
                    `AnonymousFingerprint` varchar(64) CHARACTER SET utf8mb4 NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    CONSTRAINT `PK_PublicationViews` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_PublicationViews_Publications_PublicationId`
                        FOREIGN KEY (`PublicationId`) REFERENCES `Publications` (`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_PublicationViews_Users_ViewerUserId`
                        FOREIGN KEY (`ViewerUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO `PublicationFavorites` (`PublicationId`, `UserId`, `CreatedAtUtc`)
                SELECT favorites.`PublicationId`, favorites.`UserId`, MIN(favorites.`CreatedAtUtc`) AS `CreatedAtUtc`
                FROM (
                    SELECT fli.`PublicationId`, fl.`UserId`, fli.`CreatedAtUtc`
                    FROM `FavoriteListItems` fli
                    INNER JOIN `FavoriteLists` fl ON fl.`Id` = fli.`FavoriteListId`
                ) favorites
                LEFT JOIN `PublicationFavorites` pf
                    ON pf.`PublicationId` = favorites.`PublicationId`
                   AND pf.`UserId` = favorites.`UserId`
                WHERE pf.`Id` IS NULL
                GROUP BY favorites.`PublicationId`, favorites.`UserId`;
                """);

            CreateUniqueIndexIfMissing(migrationBuilder, "PublicationFavorites", "IX_PublicationFavorites_PublicationId_UserId", "`PublicationId`, `UserId`");
            CreateIndexIfMissing(migrationBuilder, "PublicationFavorites", "IX_PublicationFavorites_UserId", "`UserId`");
            CreateIndexIfMissing(migrationBuilder, "PublicationFavorites", "IX_PublicationFavorites_CreatedAtUtc", "`CreatedAtUtc`");
            CreateUniqueIndexIfMissing(migrationBuilder, "PublicationViews", "IX_PublicationViews_PublicationId_ViewerUserId", "`PublicationId`, `ViewerUserId`");
            CreateUniqueIndexIfMissing(migrationBuilder, "PublicationViews", "IX_PublicationViews_PublicationId_AnonymousFingerprint", "`PublicationId`, `AnonymousFingerprint`");
            CreateIndexIfMissing(migrationBuilder, "PublicationViews", "IX_PublicationViews_ViewerUserId", "`ViewerUserId`");
            CreateIndexIfMissing(migrationBuilder, "PublicationViews", "IX_PublicationViews_CreatedAtUtc", "`CreatedAtUtc`");

            migrationBuilder.Sql(
                """
                UPDATE `Publications` p
                SET `UniqueFavoriteCount` = (
                    SELECT COUNT(*)
                    FROM `PublicationFavorites` pf
                    WHERE pf.`PublicationId` = p.`Id`
                );
                """);

            migrationBuilder.Sql(
                """
                UPDATE `Publications` p
                SET `UniqueViewCount` = (
                    SELECT COUNT(*)
                    FROM `PublicationViews` pv
                    WHERE pv.`PublicationId` = p.`Id`
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "PublicationFavorites", "IX_PublicationFavorites_CreatedAtUtc");
            DropIndexIfExists(migrationBuilder, "PublicationFavorites", "IX_PublicationFavorites_UserId");
            DropIndexIfExists(migrationBuilder, "PublicationFavorites", "IX_PublicationFavorites_PublicationId_UserId");
            DropIndexIfExists(migrationBuilder, "PublicationViews", "IX_PublicationViews_CreatedAtUtc");
            DropIndexIfExists(migrationBuilder, "PublicationViews", "IX_PublicationViews_ViewerUserId");
            DropIndexIfExists(migrationBuilder, "PublicationViews", "IX_PublicationViews_PublicationId_AnonymousFingerprint");
            DropIndexIfExists(migrationBuilder, "PublicationViews", "IX_PublicationViews_PublicationId_ViewerUserId");

            migrationBuilder.Sql("DROP TABLE IF EXISTS `PublicationFavorites`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `PublicationViews`;");
            DropColumnIfExists(migrationBuilder, "Publications", "UniqueFavoriteCount");
            DropColumnIfExists(migrationBuilder, "Publications", "UniqueViewCount");
        }

        private static void AddIntColumnIfMissing(MigrationBuilder migrationBuilder, string tableName, string columnName, int defaultValue)
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
                    'ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` int NOT NULL DEFAULT {defaultValue};',
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
    }
}
