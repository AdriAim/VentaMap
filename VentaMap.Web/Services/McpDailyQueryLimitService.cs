using System.Collections.Concurrent;

namespace VentaMap.Services;

/// <summary>
/// Counts actual MCP searches (not transport handshakes) for each caller during
/// the local calendar day. It is intentionally in-memory: public MCP traffic is
/// best-effort protected here while the short burst limiter protects the endpoint.
/// </summary>
public sealed class McpDailyQueryLimitService(IHttpContextAccessor httpContextAccessor)
{
    public const int DailyLimit = 20;

    private readonly ConcurrentDictionary<string, int> queryCounts = new();

    public bool TryConsumeQuery()
    {
        var context = httpContextAccessor.HttpContext;
        var caller = context?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var argentinaNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTime.UtcNow,
            "America/Argentina/Buenos_Aires");
        var today = DateOnly.FromDateTime(argentinaNow);
        var key = $"{today:yyyy-MM-dd}:{caller}";

        while (true)
        {
            var current = queryCounts.GetOrAdd(key, 0);
            if (current >= DailyLimit)
            {
                return false;
            }

            if (queryCounts.TryUpdate(key, current + 1, current))
            {
                return true;
            }
        }
    }
}
