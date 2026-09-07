using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations
{
    /// <inheritdoc />
    public partial class add_publication_media_table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS `PublicationMedia` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `PublicationId` int NOT NULL,
                    `SortOrder` int NOT NULL,
                    `MediaType` tinyint unsigned NOT NULL,
                    `Url` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
                    `IsPrimary` tinyint(1) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    CONSTRAINT `PK_PublicationMedia` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_PublicationMedia_Publications_PublicationId`
                        FOREIGN KEY (`PublicationId`) REFERENCES `Publications` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4;
                """);

            CreateIndexIfMissing(
                migrationBuilder,
                "PublicationMedia",
                "IX_PublicationMedia_PublicationId_MediaType_IsPrimary",
                "`PublicationId`, `MediaType`, `IsPrimary`");

            CreateUniqueIndexIfMissing(
                migrationBuilder,
                "PublicationMedia",
                "IX_PublicationMedia_PublicationId_SortOrder",
                "`PublicationId`, `SortOrder`");

            migrationBuilder.Sql(
                """
                SET @video_column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Publications'
                      AND COLUMN_NAME = 'VideoUrl'
                );
                SET @sql = IF(
                    @video_column_exists = 1,
                    'INSERT INTO PublicationMedia (PublicationId, SortOrder, MediaType, Url, IsPrimary, CreatedAtUtc)
                     SELECT
                         p.Id,
                         1,
                         2,
                         TRIM(p.VideoUrl),
                         1,
                         p.CreatedAtUtc
                     FROM Publications p
                     WHERE p.VideoUrl IS NOT NULL
                       AND TRIM(p.VideoUrl) <> ''''
                       AND NOT EXISTS (
                         SELECT 1
                         FROM PublicationMedia pm
                         WHERE pm.PublicationId = p.Id
                           AND pm.SortOrder = 1
                     );',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql(
                """
                SET @images_column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Publications'
                      AND COLUMN_NAME = 'ImagesCsv'
                );
                SET @video_column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Publications'
                      AND COLUMN_NAME = 'VideoUrl'
                );
                SET @sql = IF(
                    @images_column_exists = 1,
                    IF(
                        @video_column_exists = 1,
                        'INSERT INTO PublicationMedia (PublicationId, SortOrder, MediaType, Url, IsPrimary, CreatedAtUtc)
                         SELECT
                             p.Id,
                             jt.ImageOrdinal + CASE WHEN p.VideoUrl IS NOT NULL AND TRIM(p.VideoUrl) <> '''' THEN 1 ELSE 0 END,
                             1,
                             TRIM(jt.ImageUrl),
                             CASE
                                 WHEN (p.VideoUrl IS NULL OR TRIM(p.VideoUrl) = '''') AND jt.ImageOrdinal = 1 THEN 1
                                 ELSE 0
                             END,
                             p.CreatedAtUtc
                         FROM Publications p
                         JOIN JSON_TABLE(
                             CONCAT(
                                 ''["'',
                                 REPLACE(
                                     REPLACE(
                                         REPLACE(COALESCE(p.ImagesCsv, ''''), ''\\'', ''\\\\''),
                                         ''"'',
                                         ''\\"''
                                     ),
                                     '','',
                                     ''","''
                                 ),
                                 ''"]''
                             ),
                             ''$[*]'' COLUMNS (
                                 ImageOrdinal FOR ORDINALITY,
                                 ImageUrl VARCHAR(1000) PATH ''$''
                             )
                         ) AS jt
                         WHERE TRIM(COALESCE(jt.ImageUrl, '''')) <> ''''
                           AND NOT EXISTS (
                             SELECT 1
                             FROM PublicationMedia pm
                             WHERE pm.PublicationId = p.Id
                               AND pm.SortOrder = jt.ImageOrdinal + CASE WHEN p.VideoUrl IS NOT NULL AND TRIM(p.VideoUrl) <> '''' THEN 1 ELSE 0 END
                         );',
                        'INSERT INTO PublicationMedia (PublicationId, SortOrder, MediaType, Url, IsPrimary, CreatedAtUtc)
                         SELECT
                             p.Id,
                             jt.ImageOrdinal,
                             1,
                             TRIM(jt.ImageUrl),
                             CASE WHEN jt.ImageOrdinal = 1 THEN 1 ELSE 0 END,
                             p.CreatedAtUtc
                         FROM Publications p
                         JOIN JSON_TABLE(
                             CONCAT(
                                 ''["'',
                                 REPLACE(
                                     REPLACE(
                                         REPLACE(COALESCE(p.ImagesCsv, ''''), ''\\'', ''\\\\''),
                                         ''"'',
                                         ''\\"''
                                     ),
                                     '','',
                                     ''","''
                                 ),
                                 ''"]''
                             ),
                             ''$[*]'' COLUMNS (
                                 ImageOrdinal FOR ORDINALITY,
                                 ImageUrl VARCHAR(1000) PATH ''$''
                             )
                         ) AS jt
                         WHERE TRIM(COALESCE(jt.ImageUrl, '''')) <> ''''
                           AND NOT EXISTS (
                             SELECT 1
                             FROM PublicationMedia pm
                             WHERE pm.PublicationId = p.Id
                               AND pm.SortOrder = jt.ImageOrdinal
                         );'
                    ),
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            DropColumnIfExists(migrationBuilder, "Publications", "ImagesCsv");
            DropColumnIfExists(migrationBuilder, "Publications", "VideoUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            AddLongTextColumnIfMissing(migrationBuilder, "Publications", "ImagesCsv", defaultValue: "");
            AddNullableVarcharColumnIfMissing(migrationBuilder, "Publications", "VideoUrl", 400);
            migrationBuilder.Sql("DROP TABLE IF EXISTS `PublicationMedia`;");
        }

        private static void AddLongTextColumnIfMissing(MigrationBuilder migrationBuilder, string tableName, string columnName, string defaultValue)
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
                    'ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` longtext NOT NULL DEFAULT ''{defaultValue}'';',
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
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
    }
}
