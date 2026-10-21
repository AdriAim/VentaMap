using System.Security.Cryptography;
using System.Text;

namespace VentaMap.Services;

public class SiteAnalyticsService(SiteVisitQueue queue, CurrentUserAccessor currentUserAccessor)
{
    private const string VisitorCookieName = "ventamap.site-visitor";
    private const string VisitDayCookieName = "ventamap.site-visit-day";

    public void TrackVisit(HttpContext context)
    {
        if (currentUserAccessor.IsDebugUser)
        {
            return;
        }

        var visitorId = context.Request.Cookies[VisitorCookieName];
        if (string.IsNullOrWhiteSpace(visitorId))
        {
            visitorId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            context.Response.Cookies.Append(VisitorCookieName, visitorId, CookieOptions(context));
        }

        var argentinaDay = DateTime.UtcNow.AddHours(-3).Date;
        var dayText = argentinaDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (string.Equals(context.Request.Cookies[VisitDayCookieName], dayText, StringComparison.Ordinal))
        {
            return;
        }

        context.Response.Cookies.Append(VisitDayCookieName, dayText, CookieOptions(context));
        var visitorHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(visitorId)));
        var attribution = GetAttribution(context.Request);
        queue.TryEnqueue(new SiteVisitEvent(
            visitorHash,
            argentinaDay,
            attribution.Channel,
            attribution.Source,
            attribution.Medium,
            attribution.Campaign,
            attribution.ReferrerHost));
    }

    private static VisitAttribution GetAttribution(HttpRequest request)
    {
        var source = Clean(request.Query["utm_source"], 120);
        var medium = Clean(request.Query["utm_medium"], 120);
        var campaign = Clean(request.Query["utm_campaign"], 160);
        var sharedLink = string.Equals(Clean(request.Query["vm_source"], 32), "share", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "ventamap-share", StringComparison.OrdinalIgnoreCase)
            || string.Equals(medium, "shared_link", StringComparison.OrdinalIgnoreCase);
        var isSocialCampaign = IsSocialSource(source)
            && (!string.IsNullOrWhiteSpace(campaign)
                || string.Equals(medium, "paid_social", StringComparison.OrdinalIgnoreCase)
                || string.Equals(medium, "cpc", StringComparison.OrdinalIgnoreCase));

        var referrerHost = GetReferrerHost(request.Headers.Referer);
        return new VisitAttribution(
            sharedLink ? "shared_link" : isSocialCampaign ? "social_campaign" : "other",
            source,
            medium,
            campaign,
            referrerHost);
    }

    private static bool IsSocialSource(string? source) => source?.Trim().ToLowerInvariant() is
        "instagram" or "ig" or "facebook" or "fb" or "meta" or "messenger" or "whatsapp" or "x" or "twitter" or "tiktok" or "linkedin" or "youtube";

    private static string? Clean(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, maxLength)];
    }

    private static string? GetReferrerHost(string? referrer) =>
        Uri.TryCreate(referrer, UriKind.Absolute, out var uri) ? Clean(uri.Host, 255) : null;

    private static CookieOptions CookieOptions(HttpContext context) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddYears(1),
        Path = "/"
    };

    private sealed record VisitAttribution(string Channel, string? Source, string? Medium, string? Campaign, string? ReferrerHost);
}
