using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentaMap.Migrations;

public partial class add_verified_operations_and_reviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS `VentaMapParameters` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Key` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
                `Value` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
                `DataType` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
                `Description` varchar(300) CHARACTER SET utf8mb4 NULL,
                `UpdatedAtUtc` datetime(6) NOT NULL,
                `UpdatedByUserId` int NULL,
                CONSTRAINT `PK_VentaMapParameters` PRIMARY KEY (`Id`),
                CONSTRAINT `FK_VentaMapParameters_Users_UpdatedByUserId`
                    FOREIGN KEY (`UpdatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL,
                UNIQUE KEY `IX_VentaMapParameters_Key` (`Key`),
                KEY `IX_VentaMapParameters_UpdatedByUserId` (`UpdatedByUserId`)
            ) CHARACTER SET=utf8mb4;
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS `VerifiedOperations` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `PublicationId` int NOT NULL,
                `OperationType` tinyint unsigned NULL,
                `AdvertiserUserId` int NOT NULL,
                `CounterpartyUserId` int NULL,
                `CounterpartyEmail` varchar(160) CHARACTER SET utf8mb4 NOT NULL,
                `CounterpartyKind` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
                `Status` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
                `ResponseTokenHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
                `ResponseTokenExpiresAtUtc` datetime(6) NOT NULL,
                `CounterpartyReportedProblem` tinyint(1) NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                `CounterpartyRespondedAtUtc` datetime(6) NULL,
                `ConfirmedAtUtc` datetime(6) NULL,
                CONSTRAINT `PK_VerifiedOperations` PRIMARY KEY (`Id`),
                CONSTRAINT `FK_VerifiedOperations_Publications_PublicationId`
                    FOREIGN KEY (`PublicationId`) REFERENCES `Publications` (`Id`) ON DELETE RESTRICT,
                CONSTRAINT `FK_VerifiedOperations_Users_AdvertiserUserId`
                    FOREIGN KEY (`AdvertiserUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
                CONSTRAINT `FK_VerifiedOperations_Users_CounterpartyUserId`
                    FOREIGN KEY (`CounterpartyUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL,
                UNIQUE KEY `IX_VerifiedOperations_ResponseTokenHash` (`ResponseTokenHash`),
                KEY `IX_VerifiedOperations_AdvertiserUserId` (`AdvertiserUserId`),
                KEY `IX_VerifiedOperations_CounterpartyEmail_Status` (`CounterpartyEmail`, `Status`),
                KEY `IX_VerifiedOperations_CounterpartyUserId` (`CounterpartyUserId`),
                KEY `IX_VerifiedOperations_PublicationId_Status` (`PublicationId`, `Status`)
            ) CHARACTER SET=utf8mb4;
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS `OperationReviews` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `VerifiedOperationId` int NOT NULL,
                `ReviewerUserId` int NOT NULL,
                `ReviewedUserId` int NOT NULL,
                `ReviewedRole` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
                `Stars` tinyint unsigned NULL,
                `Comment` varchar(1000) CHARACTER SET utf8mb4 NULL,
                `DeclinedToRate` tinyint(1) NOT NULL,
                `SubmittedAtUtc` datetime(6) NOT NULL,
                `PublishAtUtc` datetime(6) NULL,
                `ModerationStatus` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
                CONSTRAINT `PK_OperationReviews` PRIMARY KEY (`Id`),
                CONSTRAINT `FK_OperationReviews_VerifiedOperations_VerifiedOperationId`
                    FOREIGN KEY (`VerifiedOperationId`) REFERENCES `VerifiedOperations` (`Id`) ON DELETE CASCADE,
                CONSTRAINT `FK_OperationReviews_Users_ReviewedUserId`
                    FOREIGN KEY (`ReviewedUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
                CONSTRAINT `FK_OperationReviews_Users_ReviewerUserId`
                    FOREIGN KEY (`ReviewerUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
                UNIQUE KEY `IX_OperationReviews_VerifiedOperationId_ReviewerUserId` (`VerifiedOperationId`, `ReviewerUserId`),
                KEY `IX_OperationReviews_ReviewedUserId_ReviewedRole_SubmittedAtUtc` (`ReviewedUserId`, `ReviewedRole`, `SubmittedAtUtc`),
                KEY `IX_OperationReviews_ReviewerUserId` (`ReviewerUserId`)
            ) CHARACTER SET=utf8mb4;
            """);

        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `VentaMapParameters`
                (`Key`, `Value`, `DataType`, `Description`, `UpdatedAtUtc`, `UpdatedByUserId`)
            VALUES
                ('Reviews.Enabled', 'true', 'Boolean', 'Activa el sistema de operaciones y reseñas.', UTC_TIMESTAMP(6), NULL),
                ('Reviews.Advertiser.Enabled', 'true', 'Boolean', 'Permite reseñar y mostrar la reputacion de anunciantes.', UTC_TIMESTAMP(6), NULL),
                ('Reviews.Counterparty.Enabled', 'true', 'Boolean', 'Permite reseñar y mostrar la reputacion de contrapartes.', UTC_TIMESTAMP(6), NULL),
                ('Reviews.EmailNotifications.Enabled', 'true', 'Boolean', 'Envia emails de confirmacion de operaciones.', UTC_TIMESTAMP(6), NULL),
                ('Reviews.DisplayExisting.Enabled', 'true', 'Boolean', 'Muestra reseñas publicadas existentes.', UTC_TIMESTAMP(6), NULL),
                ('Reviews.PublicationDelayDays', '7', 'Integer', 'Dias de espera antes de publicar las respuestas.', UTC_TIMESTAMP(6), NULL),
                ('Reviews.ResponseDeadlineDays', '14', 'Integer', 'Dias maximos para esperar la respuesta de la otra persona.', UTC_TIMESTAMP(6), NULL);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `OperationReviews`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `VerifiedOperations`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `VentaMapParameters`;");
    }
}
