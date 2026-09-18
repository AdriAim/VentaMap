using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;

var builder = WebApplication.CreateBuilder(args);
var applyMigrationsOnStartup = GetBooleanSetting(builder.Configuration, "Database:ApplyMigrationsOnStartup");
var runSeedDataOnStartup = GetBooleanSetting(builder.Configuration, "Database:RunSeedDataOnStartup");
var configuredUrls = builder.Configuration["urls"]
    ?? builder.Configuration["ASPNETCORE_URLS"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? string.Empty;
var hasHttpsUrlConfigured = configuredUrls
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
var sharedApplicationName = builder.Configuration["Authentication:SharedApplicationName"] ?? "VentaMap";
var maxUploadRequestBytes = builder.Configuration.GetValue<long?>("Uploads:MaxRequestBodyBytes") ?? 100L * 1024 * 1024;

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadRequestBytes;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadRequestBytes;
});
builder.Services.AddDbContext<VentaMapDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration["Authentication:CookieName"] ?? ".VentaMap.Auth.v2";
        options.Cookie.Domain = builder.Configuration["Authentication:CookieDomain"];
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.Events.OnTicketReceived = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<VentaMapDbContext>();
                var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrWhiteSpace(email))
                {
                    return;
                }

                var name = context.Principal?.Identity?.Name
                    ?? context.Principal?.FindFirstValue(ClaimTypes.Name)
                    ?? email;

                var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email.ToLower());
                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        Name = name,
                        Email = email.ToLower(),
                        Phone = string.Empty,
                        RespondsEmails = false,
                        AcceptsCalls = true,
                        RespondsWhatsApp = true,
                        ContactPreference = "Calls|WhatsApp",
                        PasswordHash = string.Empty,
                        AuthProvider = "Google"
                    };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.Name),
                    new(ClaimTypes.Email, user.Email),
                    new("phone", user.Phone ?? string.Empty),
                    new("contact-preference", BuildContactPreference(user.RespondsEmails, user.AcceptsCalls, user.RespondsWhatsApp)),
                    new("provider", user.AuthProvider),
                    new("is-admin", user.IsAdmin ? "true" : "false")
                };

                context.Principal = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
                if (context.Properties is not null)
                {
                    context.Properties.IsPersistent = true;
                }

                context.Response.Cookies.Delete(NavigationLocalityService.CookieName);
                context.Response.Cookies.Delete(PublicationGroupPreferenceService.CookieName);
            };
        });
}

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName(sharedApplicationName);
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<CloudflareR2ImageStorageService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PublicationService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddHttpClient<MercadoPagoService>();
builder.Services.AddScoped<PublicationAnalyticsService>();
builder.Services.AddScoped<PublicationGroupTypeService>();
builder.Services.AddScoped<PublicationCategoryService>();
builder.Services.AddScoped<PublicationCategoryFieldService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<FavoriteService>();
builder.Services.AddScoped<SharedPublicationListService>();
builder.Services.AddScoped<SuggestionService>();
builder.Services.AddScoped<VentaMapParameterService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<NavigationLocalityService>();
builder.Services.AddScoped<PublicationGroupPreferenceService>();
builder.Services.AddHttpClient<ArgentineLocalityLookupService>();
builder.Services.AddHostedService<PublicationExpirationWorker>();

var app = builder.Build();
app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VentaMapDbContext>();
    await EnsureDatabaseSchemaAsync(db, app.Environment, applyMigrationsOnStartup);
    await EnsureUserCompatibilityColumnsAsync(db);
    await SeedArgentineLocalitiesAsync(db);

    if (runSeedDataOnStartup)
    {
        await SeedData.InitializeAsync(db);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    if (hasHttpsUrlConfigured)
    {
        app.UseHsts();
    }
}

if (hasHttpsUrlConfigured)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.MapGet("/MisPublicaciones", () => Results.Redirect("/MisAnuncios", permanent: true));
app.Run();

static async Task SeedArgentineLocalitiesAsync(VentaMapDbContext db)
{
    var existingKeys = new HashSet<string>(
        await db.ArgentineLocalities
            .Select(x => $"{x.Province}|{x.Locality}")
            .ToListAsync(),
        StringComparer.OrdinalIgnoreCase);

    var missing = ArgentineLocalityCatalog.All
        .Where(x => !existingKeys.Contains($"{x.Province}|{x.Locality}"))
        .Select(x => new ArgentineLocality
        {
            Locality = x.Locality,
            Province = x.Province,
            Latitude = x.Latitude,
            Longitude = x.Longitude,
            SortOrder = x.SortOrder,
            IsActive = x.IsActive
        })
        .ToList();

    if (missing.Count == 0)
    {
        return;
    }

    db.ArgentineLocalities.AddRange(missing);
    await db.SaveChangesAsync();
}

static async Task EnsureUserCompatibilityColumnsAsync(VentaMapDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await EnsureColumnAsync(connection, "Users", "RespondsEmails", "tinyint(1) NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, "Users", "AcceptsCalls", "tinyint(1) NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, "Users", "RespondsWhatsApp", "tinyint(1) NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, "Users", "AllowsSiteChat", "tinyint(1) NOT NULL DEFAULT 1");
        await EnsureColumnAsync(connection, "Users", "ContactPreference", "varchar(40) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'CallsWhatsApp'");
        await EnsureColumnAsync(connection, "Users", "IsAdmin", "tinyint(1) NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, "Users", "IsDebugUser", "tinyint(1) NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, "Users", "CanPublish", "tinyint(1) NOT NULL DEFAULT 1");
        await EnsureColumnAsync(connection, "Users", "CanReport", "tinyint(1) NOT NULL DEFAULT 1");
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureDatabaseSchemaAsync(VentaMapDbContext db, IWebHostEnvironment environment, bool applyMigrationsOnStartup)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        var hasUsersTable = await TableExistsAsync(connection, "Users");
        var hasPublicationsTable = await TableExistsAsync(connection, "Publications");
        var hasAppTables = hasUsersTable || hasPublicationsTable || await TableExistsAsync(connection, "ArgentineLocalities");

        if (!hasAppTables)
        {
            await db.Database.MigrateAsync();
        }
        else if (environment.IsDevelopment() || applyMigrationsOnStartup)
        {
            await db.Database.MigrateAsync();
        }

        await EnsurePublicationCategoryFieldSchemaAsync(connection);
        await EnsurePublicationMediaSchemaAsync(connection);
        await EnsurePublicationExpirationSchemaAsync(connection);
        await EnsureBillingSchemaAsync(connection);
        await EnsurePublicationFavoritesSchemaAsync(connection);
        await EnsurePublicationAnalyticsSchemaAsync(connection);
        await EnsurePublicationCountersSchemaAsync(connection);
        await EnsureCompanyAndSuggestionsSchemaAsync(connection);
        await EnsureReviewSchemaAsync(connection);
        await EnsureUserHeaderPublicationGroupsSchemaAsync(connection);
        await EnsureSharedPublicationListsSchemaAsync(connection);
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task<bool> HistoryHasRowsAsync(System.Data.Common.DbConnection connection)
{
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM `__EFMigrationsHistory`";
    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}

static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string tableName)
{
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
        """;

    var tableParameter = command.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    command.Parameters.Add(tableParameter);

    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}

static async Task ExecuteNonQueryAsync(System.Data.Common.DbConnection connection, string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
}

static async Task EnsurePublicationCategoryFieldSchemaAsync(System.Data.Common.DbConnection connection)
{
    await ExecuteNonQueryAsync(connection,
        """
        CREATE TABLE IF NOT EXISTS `PublicationCategoryFields` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `CategoryId` int NULL,
            `InternalName` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
            `Label` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
            `DataType` tinyint unsigned NOT NULL,
            `Required` tinyint(1) NOT NULL,
            `SortOrder` int NOT NULL,
            `IsActive` tinyint(1) NOT NULL,
            `OptionsCsv` varchar(1000) CHARACTER SET utf8mb4 NULL,
            `Unit` varchar(24) CHARACTER SET utf8mb4 NULL,
            `GroupId` tinyint unsigned NULL,
            `InputExample` varchar(180) CHARACTER SET utf8mb4 NULL,
            `ShowInBasicData` tinyint(1) NOT NULL DEFAULT 0,
            CONSTRAINT `PK_PublicationCategoryFields` PRIMARY KEY (`Id`)
        ) CHARACTER SET=utf8mb4;
        """);

    await EnsureColumnAsync(connection, "PublicationCategoryFields", "OptionsCsv", "varchar(1000) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "PublicationCategoryFields", "Unit", "varchar(24) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "PublicationCategoryFields", "GroupId", "tinyint unsigned NULL");
    await EnsureColumnAsync(connection, "PublicationCategoryFields", "InputExample", "varchar(180) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "PublicationCategoryFields", "ShowInBasicData", "tinyint(1) NOT NULL DEFAULT 0");

    await ExecuteNonQueryAsync(connection,
        """
        UPDATE `PublicationCategoryFields`
        SET `Unit` = CASE `InternalName`
            WHEN 'superficie_total_m2' THEN 'm2'
            WHEN 'superficie_cubierta_m2' THEN 'm2'
            WHEN 'antiguedad_anios' THEN 'anios'
            WHEN 'expensas' THEN 'ARS'
            WHEN 'kilometros' THEN 'km'
            WHEN 'stock' THEN 'unid'
            ELSE `Unit`
        END
        WHERE `Unit` IS NULL OR `Unit` = '';
        """);

    await ExecuteNonQueryAsync(connection,
        """
        UPDATE `PublicationCategoryFields`
        SET `ShowInBasicData` = `Required`
        WHERE `ShowInBasicData` IS NULL OR `ShowInBasicData` = 0;
        """);

    if (await TableExistsAsync(connection, "PublicationCategories"))
    {
        await ExecuteNonQueryAsync(connection,
            """
            UPDATE `PublicationCategoryFields` cf
            INNER JOIN `PublicationCategories` c ON c.`Id` = cf.`CategoryId`
            SET cf.`GroupId` = c.`Group`,
                cf.`CategoryId` = NULL
            WHERE cf.`CategoryId` IS NOT NULL AND cf.`GroupId` IS NULL;
            """);
    }

    await ExecuteNonQueryAsync(connection,
        """
        CREATE TABLE IF NOT EXISTS `PublicationFieldValues` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `PublicationId` int NOT NULL,
            `CategoryFieldId` int NOT NULL,
            `ValueText` varchar(500) CHARACTER SET utf8mb4 NULL,
            `ValueNumber` decimal(18,2) NULL,
            `ValueBoolean` tinyint(1) NULL,
            CONSTRAINT `PK_PublicationFieldValues` PRIMARY KEY (`Id`)
        ) CHARACTER SET=utf8mb4;
        """);
}

static async Task EnsurePublicationMediaSchemaAsync(System.Data.Common.DbConnection connection)
{
    await ExecuteNonQueryAsync(connection,
        """
        CREATE TABLE IF NOT EXISTS `PublicationMedia` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `PublicationId` int NOT NULL,
            `SortOrder` int NOT NULL,
            `MediaType` tinyint unsigned NOT NULL,
            `Url` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
            `IsPrimary` tinyint(1) NOT NULL,
            `CreatedAtUtc` datetime(6) NOT NULL,
            CONSTRAINT `PK_PublicationMedia` PRIMARY KEY (`Id`)
        ) CHARACTER SET=utf8mb4;
        """);

    var hasImagesCsv = await ColumnExistsAsync(connection, "Publications", "ImagesCsv");
    var hasVideoUrl = await ColumnExistsAsync(connection, "Publications", "VideoUrl");
    if (!hasImagesCsv && !hasVideoUrl)
    {
        return;
    }

    await ExecuteNonQueryAsync(connection,
        """
        INSERT INTO `PublicationMedia` (`PublicationId`, `SortOrder`, `MediaType`, `Url`, `IsPrimary`, `CreatedAtUtc`)
        SELECT
            p.`Id`,
            1,
            2,
            TRIM(p.`VideoUrl`),
            1,
            p.`CreatedAtUtc`
        FROM `Publications` p
        WHERE EXISTS (
                SELECT 1
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'Publications'
                  AND COLUMN_NAME = 'VideoUrl'
            )
          AND p.`VideoUrl` IS NOT NULL
          AND TRIM(p.`VideoUrl`) <> ''
          AND NOT EXISTS (
              SELECT 1
              FROM `PublicationMedia` pm
              WHERE pm.`PublicationId` = p.`Id`
                AND pm.`SortOrder` = 1
          );
        """);

    if (!hasImagesCsv)
    {
        return;
    }

    var imageInsertSql = hasVideoUrl
        ? """
          INSERT INTO `PublicationMedia` (`PublicationId`, `SortOrder`, `MediaType`, `Url`, `IsPrimary`, `CreatedAtUtc`)
          SELECT
              p.`Id`,
              jt.`ImageOrdinal` + CASE WHEN p.`VideoUrl` IS NOT NULL AND TRIM(p.`VideoUrl`) <> '' THEN 1 ELSE 0 END,
              1,
              TRIM(jt.`ImageUrl`),
              CASE WHEN (p.`VideoUrl` IS NULL OR TRIM(p.`VideoUrl`) = '') AND jt.`ImageOrdinal` = 1 THEN 1 ELSE 0 END,
              p.`CreatedAtUtc`
          FROM `Publications` p
          JOIN JSON_TABLE(
              CONCAT(
                  '["',
                  REPLACE(
                      REPLACE(
                          REPLACE(COALESCE(p.`ImagesCsv`, ''), '\\', '\\\\'),
                          '"',
                          '\\"'
                      ),
                      ',',
                      '","'
                  ),
                  '"]'
              ),
              '$[*]' COLUMNS (
                  `ImageOrdinal` FOR ORDINALITY,
                  `ImageUrl` VARCHAR(1000) PATH '$'
              )
          ) AS jt
          WHERE TRIM(COALESCE(jt.`ImageUrl`, '')) <> ''
            AND NOT EXISTS (
                SELECT 1
                FROM `PublicationMedia` pm
                WHERE pm.`PublicationId` = p.`Id`
                  AND pm.`SortOrder` = jt.`ImageOrdinal` + CASE WHEN p.`VideoUrl` IS NOT NULL AND TRIM(p.`VideoUrl`) <> '' THEN 1 ELSE 0 END
            );
          """
        : """
          INSERT INTO `PublicationMedia` (`PublicationId`, `SortOrder`, `MediaType`, `Url`, `IsPrimary`, `CreatedAtUtc`)
          SELECT
              p.`Id`,
              jt.`ImageOrdinal`,
              1,
              TRIM(jt.`ImageUrl`),
              CASE WHEN jt.`ImageOrdinal` = 1 THEN 1 ELSE 0 END,
              p.`CreatedAtUtc`
          FROM `Publications` p
          JOIN JSON_TABLE(
              CONCAT(
                  '["',
                  REPLACE(
                      REPLACE(
                          REPLACE(COALESCE(p.`ImagesCsv`, ''), '\\', '\\\\'),
                          '"',
                          '\\"'
                      ),
                      ',',
                      '","'
                  ),
                  '"]'
              ),
              '$[*]' COLUMNS (
                  `ImageOrdinal` FOR ORDINALITY,
                  `ImageUrl` VARCHAR(1000) PATH '$'
              )
          ) AS jt
          WHERE TRIM(COALESCE(jt.`ImageUrl`, '')) <> ''
            AND NOT EXISTS (
                SELECT 1
                FROM `PublicationMedia` pm
                WHERE pm.`PublicationId` = p.`Id`
                  AND pm.`SortOrder` = jt.`ImageOrdinal`
            );
          """;
    await ExecuteNonQueryAsync(connection, imageInsertSql);
}

static async Task EnsurePublicationExpirationSchemaAsync(System.Data.Common.DbConnection connection)
{
    await EnsureColumnAsync(connection, "Publications", "OperationType", "tinyint unsigned NULL");
    await EnsureColumnAsync(connection, "Publications", "ExpirationNoticeSentAtUtc", "datetime(6) NULL");
    await EnsureColumnAsync(connection, "Publications", "DeactivatedAtUtc", "datetime(6) NULL");
    await EnsureColumnAsync(connection, "Publications", "DeactivationReason", "varchar(80) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "Publications", "DeactivationComment", "varchar(1000) CHARACTER SET utf8mb4 NULL");

    await ExecuteNonQueryAsync(connection,
        """
        UPDATE `Publications`
        SET `ExpiresAtUtc` = DATE_ADD(`CreatedAtUtc`, INTERVAL 30 DAY)
        WHERE `ExpiresAtUtc` IS NULL;
        """);
}

static async Task EnsureBillingSchemaAsync(System.Data.Common.DbConnection connection)
{
    await EnsureColumnAsync(connection, "Users", "IsBillingExempt", "tinyint(1) NOT NULL DEFAULT 0");

    await ExecuteNonQueryAsync(connection,
        """
        CREATE TABLE IF NOT EXISTS `BillingCharges` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `UserId` int NOT NULL,
            `Type` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
            `Amount` decimal(18,2) NOT NULL,
            `BillingMonthUtc` datetime(6) NOT NULL,
            `Status` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
            `Description` varchar(180) CHARACTER SET utf8mb4 NOT NULL,
            `Reference` varchar(100) CHARACTER SET utf8mb4 NULL,
            `CreatedAtUtc` datetime(6) NOT NULL,
            `PaidAtUtc` datetime(6) NULL,
            `MercadoPagoPaymentId` varchar(180) CHARACTER SET utf8mb4 NULL,
            CONSTRAINT `PK_BillingCharges` PRIMARY KEY (`Id`),
            CONSTRAINT `FK_BillingCharges_Users_UserId`
                FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
        ) CHARACTER SET=utf8mb4;
        """);

    await EnsureIndexAsync(connection, "BillingCharges", "IX_BillingCharges_UserId_Type_Month_Reference", "CREATE UNIQUE INDEX `IX_BillingCharges_UserId_Type_Month_Reference` ON `BillingCharges` (`UserId`, `Type`, `BillingMonthUtc`, `Reference`)");
    await EnsureIndexAsync(connection, "BillingCharges", "IX_BillingCharges_UserId_Status_Month", "CREATE INDEX `IX_BillingCharges_UserId_Status_Month` ON `BillingCharges` (`UserId`, `Status`, `BillingMonthUtc`)");
}

static async Task EnsureCompanyAndSuggestionsSchemaAsync(System.Data.Common.DbConnection connection)
{
    await EnsureColumnAsync(connection, "Users", "CompanyIndustry", "varchar(120) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "Users", "CompanyHeroBackgroundUrl", "varchar(260) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "Users", "CompanyLogoUrl", "varchar(260) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "Users", "CompanyName", "varchar(160) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "Users", "CompanySlug", "varchar(180) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "Users", "CompanyTagline", "varchar(180) CHARACTER SET utf8mb4 NULL");
    await EnsureColumnAsync(connection, "Users", "IsCompany", "tinyint(1) NOT NULL DEFAULT 0");

    await ExecuteNonQueryAsync(connection,
        """
        CREATE TABLE IF NOT EXISTS `SiteSuggestions` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `UserId` int NULL,
            `SenderName` varchar(120) CHARACTER SET utf8mb4 NULL,
            `SenderEmail` varchar(160) CHARACTER SET utf8mb4 NULL,
            `Message` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
            `CreatedAtUtc` datetime(6) NOT NULL,
            CONSTRAINT `PK_SiteSuggestions` PRIMARY KEY (`Id`)
        ) CHARACTER SET=utf8mb4;
        """);
}

static async Task EnsureSharedPublicationListsSchemaAsync(System.Data.Common.DbConnection connection)
{
    await ExecuteNonQueryAsync(connection,
        """
        CREATE TABLE IF NOT EXISTS `SharedPublicationLists` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `UserId` int NOT NULL,
            `Name` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
            `Slug` varchar(160) CHARACTER SET utf8mb4 NOT NULL,
            `DefaultMode` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Galeria',
            `CreatedAtUtc` datetime(6) NOT NULL,
            `UpdatedAtUtc` datetime(6) NOT NULL,
            CONSTRAINT `PK_SharedPublicationLists` PRIMARY KEY (`Id`),
            CONSTRAINT `FK_SharedPublicationLists_Users_UserId`
                FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
        ) CHARACTER SET=utf8mb4;
        """);

    await ExecuteNonQueryAsync(connection,
        """
        CREATE TABLE IF NOT EXISTS `SharedPublicationListItems` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `SharedPublicationListId` int NOT NULL,
            `PublicationId` int NOT NULL,
            `CreatedAtUtc` datetime(6) NOT NULL,
            CONSTRAINT `PK_SharedPublicationListItems` PRIMARY KEY (`Id`),
            CONSTRAINT `FK_SharedListItems_List`
                FOREIGN KEY (`SharedPublicationListId`) REFERENCES `SharedPublicationLists` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_SharedPublicationListItems_Publications_PublicationId`
                FOREIGN KEY (`PublicationId`) REFERENCES `Publications` (`Id`) ON DELETE CASCADE
        ) CHARACTER SET=utf8mb4;
        """);

    await EnsureColumnAsync(
        connection,
        "SharedPublicationLists",
        "DefaultMode",
        "varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Galeria'");

    await EnsureIndexAsync(connection, "SharedPublicationLists", "IX_SharedPublicationLists_Slug", "CREATE UNIQUE INDEX `IX_SharedPublicationLists_Slug` ON `SharedPublicationLists` (`Slug`)");
    await EnsureIndexAsync(connection, "SharedPublicationLists", "IX_SharedPublicationLists_UserId_Name", "CREATE INDEX `IX_SharedPublicationLists_UserId_Name` ON `SharedPublicationLists` (`UserId`, `Name`)");
    await EnsureIndexAsync(connection, "SharedPublicationListItems", "IX_SharedListItems_List_Publication", "CREATE UNIQUE INDEX `IX_SharedListItems_List_Publication` ON `SharedPublicationListItems` (`SharedPublicationListId`, `PublicationId`)");
    await EnsureIndexAsync(connection, "SharedPublicationListItems", "IX_SharedPublicationListItems_PublicationId", "CREATE INDEX `IX_SharedPublicationListItems_PublicationId` ON `SharedPublicationListItems` (`PublicationId`)");
}

static async Task EnsureReviewSchemaAsync(System.Data.Common.DbConnection connection)
{
    await ExecuteNonQueryAsync(connection,
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
                FOREIGN KEY (`UpdatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL
        ) CHARACTER SET=utf8mb4;
        """);

    await ExecuteNonQueryAsync(connection,
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
                FOREIGN KEY (`CounterpartyUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL
        ) CHARACTER SET=utf8mb4;
        """);

    await ExecuteNonQueryAsync(connection,
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
                FOREIGN KEY (`ReviewerUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
        ) CHARACTER SET=utf8mb4;
        """);

    await EnsureIndexAsync(connection, "VentaMapParameters", "IX_VentaMapParameters_Key", "CREATE UNIQUE INDEX `IX_VentaMapParameters_Key` ON `VentaMapParameters` (`Key`)");
    await EnsureIndexAsync(connection, "VentaMapParameters", "IX_VentaMapParameters_UpdatedByUserId", "CREATE INDEX `IX_VentaMapParameters_UpdatedByUserId` ON `VentaMapParameters` (`UpdatedByUserId`)");
    await EnsureIndexAsync(connection, "VerifiedOperations", "IX_VerifiedOperations_ResponseTokenHash", "CREATE UNIQUE INDEX `IX_VerifiedOperations_ResponseTokenHash` ON `VerifiedOperations` (`ResponseTokenHash`)");
    await EnsureIndexAsync(connection, "VerifiedOperations", "IX_VerifiedOperations_AdvertiserUserId", "CREATE INDEX `IX_VerifiedOperations_AdvertiserUserId` ON `VerifiedOperations` (`AdvertiserUserId`)");
    await EnsureIndexAsync(connection, "VerifiedOperations", "IX_VerifiedOperations_CounterpartyEmail_Status", "CREATE INDEX `IX_VerifiedOperations_CounterpartyEmail_Status` ON `VerifiedOperations` (`CounterpartyEmail`, `Status`)");
    await EnsureIndexAsync(connection, "VerifiedOperations", "IX_VerifiedOperations_CounterpartyUserId", "CREATE INDEX `IX_VerifiedOperations_CounterpartyUserId` ON `VerifiedOperations` (`CounterpartyUserId`)");
    await EnsureIndexAsync(connection, "VerifiedOperations", "IX_VerifiedOperations_PublicationId_Status", "CREATE INDEX `IX_VerifiedOperations_PublicationId_Status` ON `VerifiedOperations` (`PublicationId`, `Status`)");
    await EnsureIndexAsync(connection, "OperationReviews", "IX_OperationReviews_VerifiedOperationId_ReviewerUserId", "CREATE UNIQUE INDEX `IX_OperationReviews_VerifiedOperationId_ReviewerUserId` ON `OperationReviews` (`VerifiedOperationId`, `ReviewerUserId`)");
    await EnsureIndexAsync(connection, "OperationReviews", "IX_OperationReviews_ReviewedUserId_ReviewedRole_SubmittedAtUtc", "CREATE INDEX `IX_OperationReviews_ReviewedUserId_ReviewedRole_SubmittedAtUtc` ON `OperationReviews` (`ReviewedUserId`, `ReviewedRole`, `SubmittedAtUtc`)");
    await EnsureIndexAsync(connection, "OperationReviews", "IX_OperationReviews_ReviewerUserId", "CREATE INDEX `IX_OperationReviews_ReviewerUserId` ON `OperationReviews` (`ReviewerUserId`)");

    await ExecuteNonQueryAsync(connection,
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
            ('Reviews.ResponseDeadlineDays', '14', 'Integer', 'Dias maximos para esperar la respuesta de la otra persona.', UTC_TIMESTAMP(6), NULL),
            ('PaidSite.Enabled', 'false', 'Boolean', 'Activa las leyendas, los cargos y los bloqueos de facturación del sitio.', UTC_TIMESTAMP(6), NULL);
        """);
}

static async Task EnsurePublicationCountersSchemaAsync(System.Data.Common.DbConnection connection)
{
    await EnsureColumnAsync(connection, "Publications", "UniqueViewCount", "int NOT NULL DEFAULT 0");
    await EnsureColumnAsync(connection, "Publications", "UniqueFavoriteCount", "int NOT NULL DEFAULT 0");

    await ExecuteNonQueryAsync(connection,
        """
        UPDATE `Publications` p
        SET `UniqueViewCount` = (
            SELECT COUNT(*)
            FROM `PublicationViews` pv
            WHERE pv.`PublicationId` = p.`Id`
        )
        WHERE EXISTS (
            SELECT 1
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'PublicationViews'
        );
        """);

    await ExecuteNonQueryAsync(connection,
        """
        UPDATE `Publications` p
        SET `UniqueFavoriteCount` = (
            SELECT COUNT(*)
            FROM `PublicationFavorites` pf
            WHERE pf.`PublicationId` = p.`Id`
        )
        WHERE EXISTS (
            SELECT 1
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'PublicationFavorites'
        );
        """);
}

static async Task EnsurePublicationFavoritesSchemaAsync(System.Data.Common.DbConnection connection)
{
    await ExecuteNonQueryAsync(connection,
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

    await ExecuteNonQueryAsync(connection,
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

    await EnsureIndexAsync(connection, "PublicationFavorites", "IX_PublicationFavorites_PublicationId_UserId", "CREATE UNIQUE INDEX `IX_PublicationFavorites_PublicationId_UserId` ON `PublicationFavorites` (`PublicationId`, `UserId`)");
    await EnsureIndexAsync(connection, "PublicationFavorites", "IX_PublicationFavorites_CreatedAtUtc", "CREATE INDEX `IX_PublicationFavorites_CreatedAtUtc` ON `PublicationFavorites` (`CreatedAtUtc`)");
    await EnsureIndexAsync(connection, "PublicationFavorites", "IX_PublicationFavorites_UserId", "CREATE INDEX `IX_PublicationFavorites_UserId` ON `PublicationFavorites` (`UserId`)");
}

static async Task EnsurePublicationAnalyticsSchemaAsync(System.Data.Common.DbConnection connection)
{
    await ExecuteNonQueryAsync(connection,
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

    await EnsureIndexAsync(connection, "PublicationViews", "IX_PublicationViews_CreatedAtUtc", "CREATE INDEX `IX_PublicationViews_CreatedAtUtc` ON `PublicationViews` (`CreatedAtUtc`)");
    await EnsureIndexAsync(connection, "PublicationViews", "IX_PublicationViews_PublicationId_ViewerUserId", "CREATE UNIQUE INDEX `IX_PublicationViews_PublicationId_ViewerUserId` ON `PublicationViews` (`PublicationId`, `ViewerUserId`)");
    await EnsureIndexAsync(connection, "PublicationViews", "IX_PublicationViews_PublicationId_AnonymousFingerprint", "CREATE UNIQUE INDEX `IX_PublicationViews_PublicationId_AnonymousFingerprint` ON `PublicationViews` (`PublicationId`, `AnonymousFingerprint`)");
    await EnsureIndexAsync(connection, "PublicationViews", "IX_PublicationViews_ViewerUserId", "CREATE INDEX `IX_PublicationViews_ViewerUserId` ON `PublicationViews` (`ViewerUserId`)");
}

static async Task EnsureColumnAsync(System.Data.Common.DbConnection connection, string tableName, string columnName, string definition)
{
    await using var check = connection.CreateCommand();
    check.CommandText = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND COLUMN_NAME = @columnName
        """;

    var tableParameter = check.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    check.Parameters.Add(tableParameter);

    var columnParameter = check.CreateParameter();
    columnParameter.ParameterName = "@columnName";
    columnParameter.Value = columnName;
    check.Parameters.Add(columnParameter);

    var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
    if (exists)
    {
        return;
    }

    await using var alter = connection.CreateCommand();
    alter.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {definition}";
    await alter.ExecuteNonQueryAsync();
}

static async Task EnsureUserHeaderPublicationGroupsSchemaAsync(System.Data.Common.DbConnection connection)
{
    if (!await TableExistsAsync(connection, "Users"))
    {
        return;
    }

    await EnsureColumnAsync(
        connection,
        "Users",
        "HeaderPublicationGroupsCsv",
        "varchar(120) CHARACTER SET utf8mb4 NULL");
}

static async Task EnsureIndexAsync(System.Data.Common.DbConnection connection, string tableName, string indexName, string createSql)
{
    await using var check = connection.CreateCommand();
    check.CommandText = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND INDEX_NAME = @indexName
        """;

    var tableParameter = check.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    check.Parameters.Add(tableParameter);

    var indexParameter = check.CreateParameter();
    indexParameter.ParameterName = "@indexName";
    indexParameter.Value = indexName;
    check.Parameters.Add(indexParameter);

    var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
    if (exists)
    {
        return;
    }

    await ExecuteNonQueryAsync(connection, createSql);
}

static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string tableName, string columnName)
{
    await using var check = connection.CreateCommand();
    check.CommandText = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND COLUMN_NAME = @columnName
        """;

    var tableParameter = check.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    check.Parameters.Add(tableParameter);

    var columnParameter = check.CreateParameter();
    columnParameter.ParameterName = "@columnName";
    columnParameter.Value = columnName;
    check.Parameters.Add(columnParameter);

    return Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
}

static string BuildContactPreference(bool respondsEmails, bool acceptsCalls, bool respondsWhatsApp)
{
    var preferences = new List<string>();
    if (respondsEmails)
    {
        preferences.Add("Email");
    }

    if (acceptsCalls)
    {
        preferences.Add("Calls");
    }

    if (respondsWhatsApp)
    {
        preferences.Add("WhatsApp");
    }

    return preferences.Count == 0 ? "None" : string.Join("|", preferences);
}

static bool GetBooleanSetting(ConfigurationManager configuration, string key)
{
    var value = configuration[key];
    return bool.TryParse(value, out var parsed) && parsed;
}
