using VentaMap.Models;

namespace VentaMap.Services;

public static class MonitorAccess
{
    public const string Username = "monitor";
    public const string Email = "monitor@ventamap.local";
    public const string PasswordHash = "40743F0169B38448C2C9EE5604D0F9C53186AA3E5885CDCDDC90689AEE805BA5";

    public static bool IsMonitor(ApplicationUser? user) =>
        string.Equals(user?.Email, Email, StringComparison.OrdinalIgnoreCase);
}
