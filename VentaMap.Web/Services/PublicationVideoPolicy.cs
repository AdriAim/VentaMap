namespace VentaMap.Services;

/// <summary>
/// Defines the publication-video duration entitlement. Keep the exception tied
/// to the account identifier rather than to client-supplied account data.
/// </summary>
public static class PublicationVideoPolicy
{
    public const int JcBrokersUserId = 19;
    public const int DefaultMaxDurationSeconds = 60;
    public const int JcBrokersMaxDurationSeconds = 5 * 60;

    public static int GetMaxDurationSeconds(int? userId) =>
        userId == JcBrokersUserId ? JcBrokersMaxDurationSeconds : DefaultMaxDurationSeconds;

    public static string GetMaximumDurationMessage(int? userId) =>
        userId == JcBrokersUserId
            ? "El video no puede durar más de 5 minutos."
            : "El video no puede durar más de 1 minuto.";
}
