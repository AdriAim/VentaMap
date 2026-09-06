using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Ventagram.ChatService.Data;
using Ventagram.ChatService.Hubs;
using Ventagram.ChatService.Services;

var builder = WebApplication.CreateBuilder(args);
var configuredUrls = builder.Configuration["urls"]
    ?? builder.Configuration["ASPNETCORE_URLS"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? string.Empty;
var hasHttpsUrlConfigured = configuredUrls
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var sharedApplicationName = builder.Configuration["Authentication:SharedApplicationName"] ?? "Ventagram";
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName(sharedApplicationName);
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

if (allowedCorsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ChatCors", policy =>
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

var chatConnectionString = builder.Configuration.GetConnectionString("ChatConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:ChatConnection.");
var ventagramConnectionString = builder.Configuration.GetConnectionString("VentagramConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:VentagramConnection.");

await EnsureMySqlDatabaseExistsAsync(chatConnectionString);

builder.Services.AddDbContext<ChatDbContext>(options =>
{
    options.UseMySql(chatConnectionString, ServerVersion.AutoDetect(chatConnectionString));
});

builder.Services.AddDbContext<VentagramLookupDbContext>(options =>
{
    options.UseMySql(ventagramConnectionString, ServerVersion.AutoDetect(ventagramConnectionString));
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration["Authentication:CookieName"] ?? ".Ventagram.Auth.v2";
        options.Cookie.Domain = builder.Configuration["Authentication:CookieDomain"];
        options.LoginPath = builder.Configuration["Authentication:LoginPath"] ?? "/Account/Login";
        options.LogoutPath = builder.Configuration["Authentication:LogoutPath"] ?? "/Account/Logout";
        options.AccessDeniedPath = builder.Configuration["Authentication:AccessDeniedPath"] ?? "/Account/Login";
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                var publicationBaseUrl = (builder.Configuration["Chat:PublicationBaseUrl"] ?? string.Empty).TrimEnd('/');
                if (string.IsNullOrWhiteSpace(publicationBaseUrl))
                {
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                }

                var returnUrl = Uri.EscapeDataString($"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");
                context.Response.Redirect($"{publicationBaseUrl}/Account/Login?returnUrl={returnUrl}");
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ChatAppService>();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddHostedService<ChatEmailReminderWorker>();

var app = builder.Build();
app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    await EnsureChatTablesAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    if (hasHttpsUrlConfigured)
    {
        app.UseHsts();
    }
}

if (hasHttpsUrlConfigured)
{
    app.UseHttpsRedirection();
}
app.UseRouting();
if (allowedCorsOrigins.Length > 0)
{
    app.UseCors("ChatCors");
}
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "Ventagram.ChatService",
    status = "ok"
}));

app.MapGet("/error", () => Results.Problem("Se produjo un error en el servicio de chat."));

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

static bool IsApiRequest(HttpRequest request)
{
    return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
        || string.Equals(request.Headers["X-Requested-With"], "fetch", StringComparison.OrdinalIgnoreCase);
}

static async Task EnsureMySqlDatabaseExistsAsync(string connectionString)
{
    var builder = new MySqlConnectionStringBuilder(connectionString);
    var databaseName = builder.Database;
    if (string.IsNullOrWhiteSpace(databaseName))
    {
        throw new InvalidOperationException("La conexion de chat debe incluir el nombre de la base de datos.");
    }

    var adminBuilder = new MySqlConnectionStringBuilder(connectionString)
    {
        Database = string.Empty
    };

    await using var connection = new MySqlConnection(adminBuilder.ConnectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
    await command.ExecuteNonQueryAsync();
}

static async Task EnsureChatTablesAsync(ChatDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var createConversations = connection.CreateCommand();
        createConversations.CommandText = """
            CREATE TABLE IF NOT EXISTS ChatConversations (
                Id INT NOT NULL AUTO_INCREMENT,
                PublicationId INT NOT NULL,
                BuyerUserId INT NOT NULL,
                SellerUserId INT NOT NULL,
                CreatedAtUtc DATETIME(6) NOT NULL,
                LastMessageAtUtc DATETIME(6) NULL,
                LastMessagePreview VARCHAR(220) NULL,
                PRIMARY KEY (Id),
                UNIQUE KEY UX_ChatConversations_Publication_Buyer_Seller (PublicationId, BuyerUserId, SellerUserId),
                KEY IX_ChatConversations_Publication (PublicationId),
                KEY IX_ChatConversations_Buyer_LastMessage (BuyerUserId, LastMessageAtUtc),
                KEY IX_ChatConversations_Seller_LastMessage (SellerUserId, LastMessageAtUtc)
            )
            """;
        await createConversations.ExecuteNonQueryAsync();

        await using var createMessages = connection.CreateCommand();
        createMessages.CommandText = """
            CREATE TABLE IF NOT EXISTS ChatMessages (
                Id INT NOT NULL AUTO_INCREMENT,
                ConversationId INT NOT NULL,
                SenderUserId INT NOT NULL,
                Body VARCHAR(2000) NOT NULL,
                CreatedAtUtc DATETIME(6) NOT NULL,
                ReadAtUtc DATETIME(6) NULL,
                PRIMARY KEY (Id),
                KEY IX_ChatMessages_Conversation_Created (ConversationId, CreatedAtUtc),
                KEY IX_ChatMessages_Conversation_Read (ConversationId, ReadAtUtc),
                KEY IX_ChatMessages_Sender (SenderUserId)
            )
            """;
        await createMessages.ExecuteNonQueryAsync();

        await EnsureColumnAsync(connection, "ChatMessages", "EmailReminderSentAtUtc", "DATETIME(6) NULL");
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
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
    alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}";
    await alter.ExecuteNonQueryAsync();
}
