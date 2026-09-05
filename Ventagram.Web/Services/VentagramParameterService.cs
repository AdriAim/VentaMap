using Microsoft.EntityFrameworkCore;
using Ventagram.Data;

namespace Ventagram.Services;

public class VentagramParameterService(VentagramDbContext db)
{
    public const string ReviewsEnabled = "Reviews.Enabled";
    public const string AdvertiserReviewsEnabled = "Reviews.Advertiser.Enabled";
    public const string CounterpartyReviewsEnabled = "Reviews.Counterparty.Enabled";
    public const string ReviewEmailsEnabled = "Reviews.EmailNotifications.Enabled";
    public const string DisplayExistingReviewsEnabled = "Reviews.DisplayExisting.Enabled";
    public const string ReviewPublicationDelayDays = "Reviews.PublicationDelayDays";
    public const string ReviewResponseDeadlineDays = "Reviews.ResponseDeadlineDays";
    public const string PaidSiteEnabled = "PaidSite.Enabled";

    private Dictionary<string, string>? values;

    public async Task<bool> GetBoolAsync(string key, bool fallback = true)
    {
        var value = await GetValueAsync(key);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public async Task<int> GetIntAsync(string key, int fallback, int min = 0, int max = 365)
    {
        var value = await GetValueAsync(key);
        return int.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;
    }

    private async Task<string?> GetValueAsync(string key)
    {
        values ??= await db.VentagramParameters
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        return values.GetValueOrDefault(key);
    }
}
