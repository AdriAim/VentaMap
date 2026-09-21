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
        queue.TryEnqueue(new SiteVisitEvent(visitorHash, argentinaDay));
    }

    private static CookieOptions CookieOptions(HttpContext context) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddYears(1),
        Path = "/"
    };
}
