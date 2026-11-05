using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class SiteExperienceAnalyticsService(VentaMapDbContext db, CurrentUserAccessor currentUserAccessor)
{
    private static readonly HashSet<string> AllowedEvents = new(StringComparer.Ordinal)
    {
        "entry_button_clicked", "map_pin_selected", "locality_modal_shown", "locality_completed", "locality_closed", "locality_abandoned"
    };

    private static readonly HashSet<string> AllowedButtons = new(StringComparer.Ordinal)
    {
        "explore_properties", "properties_map", "explore_vehicles", "explore_all",
        "publish_now", "view_listings", "catalog_more_info", "catalog_promo", "map_pin_selected"
    };

    public async Task<bool> TrackAsync(HttpContext context, string? eventName, string? button, string? route)
    {
        if (currentUserAccessor.IsDebugUser || !AllowedEvents.Contains(eventName ?? string.Empty)) return false;

        var visitorId = context.Request.Cookies[SiteAnalyticsService.VisitorCookieName];
        var sessionId = context.Request.Cookies[SiteAnalyticsService.SessionCookieName];
        if (string.IsNullOrWhiteSpace(visitorId) || string.IsNullOrWhiteSpace(sessionId)) return false;

        var normalizedButton = string.IsNullOrWhiteSpace(button) ? string.Empty : button.Trim();
        if ((eventName == "entry_button_clicked" || eventName == "map_pin_selected") && !AllowedButtons.Contains(normalizedButton)) return false;
        if (eventName != "entry_button_clicked" && eventName != "map_pin_selected") normalizedButton = string.Empty;

        var item = new SiteExperienceEvent
        {
            VisitorHash = Hash(visitorId!),
            SessionHash = Hash(sessionId!),
            EventName = eventName!,
            Button = normalizedButton,
            AcquisitionChannel = NormalizeChannel(context.Request.Cookies[SiteAnalyticsService.AcquisitionChannelCookieName]),
            Route = NormalizeRoute(route),
            OccurredAtUtc = DateTime.UtcNow
        };
        db.SiteExperienceEvents.Add(item);
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            // El índice único descarta dobles clics y reintentos de sendBeacon/fetch.
            return false;
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string NormalizeChannel(string? channel) => channel is "social_campaign" or "shared_link" ? channel : "other";

    private static string NormalizeRoute(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Relative, out var uri)) return "/";
        var path = uri.OriginalString.Split('?', '#')[0].Trim();
        return path.StartsWith('/') && path.Length <= 512 ? path : "/";
    }
}
