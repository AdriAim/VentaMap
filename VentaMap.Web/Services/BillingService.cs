using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class BillingService(VentaMapDbContext db, VentaMapParameterService parameters, PricingService pricingService)
{
    public const int CompanyActivePublicationLimit = 50;
    public const int CompanyTrialMonths = 3;
    public const string PersonPublicationType = "PersonPublication";
    public const string CompanyMonthlyPlanType = "CompanyMonthlyPlan";
    public const int CompanyWithoutTrialUserId = 6;
    private static readonly DateTime CompanyWithoutTrialStartMonth = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int ArgentinaUtcOffsetHours = -3;

    public async Task<bool> IsPublishingBlockedAsync(ApplicationUser user)
    {
        if (user.IsCompany)
        {
            if (user.IsBillingExempt == 1) return false;

            await EnsureCompanyCurrentMonthChargeAsync(user);
            var argentinaNow = DateTime.UtcNow.AddHours(ArgentinaUtcOffsetHours);
            var currentMonth = FirstDayOfMonth(argentinaNow);
            return argentinaNow.Day >= 10 && await db.BillingCharges.AnyAsync(x =>
                x.UserId == user.Id
                && x.Type == CompanyMonthlyPlanType
                && x.Status == "Pending"
                && x.BillingMonthUtc <= currentMonth);
        }

        if (!await IsEnabledForAsync(user) || user.IsBillingExempt == 1) return false;
        return await db.BillingCharges.AnyAsync(x => x.UserId == user.Id && x.Status == "Pending");
    }

    public async Task<BillingCharge?> RequirePersonPublicationPackAsync(ApplicationUser user, PublicationCreateRequest input, int? publicationId = null)
    {
        if (!await IsEnabledForAsync(user)) return null;
        if (user.IsCompany || user.IsBillingExempt == 1) return null;

        var photoCount = input.ImagesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        var activeCount = await db.Publications.CountAsync(x => x.UserId == user.Id && x.IsActive);
        return await CreatePersonPackIfNeededAsync(user, activeCount >= 2, photoCount > 3, !string.IsNullOrWhiteSpace(input.VideoUrl), publicationId);
    }

    public async Task<bool> IsPersonPublicationPackRequiredAsync(ApplicationUser user, PublicationCreateRequest input)
    {
        if (!await IsEnabledForAsync(user)) return false;
        if (user.IsCompany || user.IsBillingExempt == 1) return false;
        var photoCount = input.ImagesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        return await db.Publications.CountAsync(x => x.UserId == user.Id && x.IsActive) >= 2
            || photoCount > 3
            || !string.IsNullOrWhiteSpace(input.VideoUrl);
    }

    public async Task<BillingCharge?> RequirePersonRepublishPackAsync(ApplicationUser user, Publication publication)
    {
        if (!await IsEnabledForAsync(user)) return null;
        if (user.IsCompany || user.IsBillingExempt == 1) return null;
        var activeCount = await db.Publications.CountAsync(x => x.UserId == user.Id && x.IsActive);
        var photoCount = publication.MediaItems.Count(x => x.MediaType == PublicationMediaType.Image && !string.IsNullOrWhiteSpace(x.Url));
        var hasVideo = publication.MediaItems.Any(x => x.MediaType == PublicationMediaType.Video && !string.IsNullOrWhiteSpace(x.Url));
        return await CreatePersonPackIfNeededAsync(user, activeCount >= 2, photoCount > 3, hasVideo, publication.Id);
    }

    public async Task<bool> IsPersonRepublishPackRequiredAsync(ApplicationUser user, Publication publication)
    {
        if (!await IsEnabledForAsync(user)) return false;
        if (user.IsCompany || user.IsBillingExempt == 1) return false;
        var photoCount = publication.MediaItems.Count(x => x.MediaType == PublicationMediaType.Image && !string.IsNullOrWhiteSpace(x.Url));
        var hasVideo = publication.MediaItems.Any(x => x.MediaType == PublicationMediaType.Video && !string.IsNullOrWhiteSpace(x.Url));
        return await db.Publications.CountAsync(x => x.UserId == user.Id && x.IsActive) >= 2 || photoCount > 3 || hasVideo;
    }

    public async Task<bool> HasReachedCompanyPublicationLimitAsync(ApplicationUser user)
    {
        if (!await IsEnabledForAsync(user) || !user.IsCompany || user.IsBillingExempt == 1) return false;

        return await db.Publications.CountAsync(x => x.UserId == user.Id && x.IsActive) >= CompanyActivePublicationLimit;
    }

    private async Task<BillingCharge?> CreatePersonPackIfNeededAsync(ApplicationUser user, bool exceedsActiveLimit, bool exceedsPhotoLimit, bool hasVideo, int? publicationId = null)
    {
        if (!exceedsActiveLimit && !exceedsPhotoLimit && !hasVideo) return null;

        // A confirmed payment is a one-use pack. Mercado Pago will mark it Paid;
        // the next eligible publish/republish consumes it.
        if (await db.BillingCharges.AnyAsync(x => x.UserId == user.Id && x.Type == PersonPublicationType && x.Status == "Paid"))
        {
            return null;
        }

        var reasons = new List<string>();
        if (exceedsActiveLimit) reasons.Add("más de 2 anuncios activos");
        if (exceedsPhotoLimit) reasons.Add("más de 3 fotos");
        if (hasVideo) reasons.Add("video");
        var charge = new BillingCharge
        {
            UserId = user.Id,
            PublicationId = publicationId,
            Type = PersonPublicationType,
            Amount = await pricingService.GetCurrentPersonPublicationAmountAsync(),
            BillingMonthUtc = FirstDayOfMonth(DateTime.UtcNow),
            Description = $"Anuncio completo ({string.Join(", ", reasons)})",
            Reference = $"person-pack-{Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.BillingCharges.Add(charge);
        await db.SaveChangesAsync();
        return charge;
    }

    public async Task EnsureCompanyCurrentMonthChargeAsync(ApplicationUser user)
    {
        if (!user.IsCompany || user.IsBillingExempt == 1) return;

        var now = DateTime.UtcNow;
        var firstMonth = await GetCompanyFirstBillingMonthAsync(user);
        if (firstMonth is null) return;

        var currentMonth = FirstDayOfMonth(now.AddHours(ArgentinaUtcOffsetHours));
        var monthsWithCharge = await db.BillingCharges
            .Where(x => x.UserId == user.Id && x.Type == CompanyMonthlyPlanType)
            .Select(x => x.BillingMonthUtc)
            .ToListAsync();

        for (var month = firstMonth.Value; month <= currentMonth; month = month.AddMonths(1))
        {
            var monthNumber = MonthsBetween(firstMonth.Value, month) + 1;
            var isForcedFirstUserMonth = user.Id == CompanyWithoutTrialUserId && month == CompanyWithoutTrialStartMonth;
            if ((user.Id != CompanyWithoutTrialUserId && monthNumber <= CompanyTrialMonths)
                || (!isForcedFirstUserMonth && !await HadActivePublicationDuringMonthAsync(user.Id, month)))
            {
                continue;
            }

            if (monthsWithCharge.Contains(month)) continue;

            var rateEffectiveAt = month == currentMonth ? now : month.AddMonths(1).AddTicks(-1);

            db.BillingCharges.Add(new BillingCharge
            {
                UserId = user.Id,
                Type = CompanyMonthlyPlanType,
                Amount = await pricingService.GetCompanyMonthlyAmountAsync(rateEffectiveAt),
                BillingMonthUtc = month,
                Description = $"Plan empresa mensual (hasta {CompanyActivePublicationLimit} anuncios activos)",
                Reference = $"company-{month:yyyy-MM}",
                CreatedAtUtc = now
            });
        }

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
    }

    public async Task<List<CompanyMonthlyPeriod>> GetCompanyMonthlyPeriodsAsync(ApplicationUser user)
    {
        await EnsureCompanyCurrentMonthChargeAsync(user);
        var firstMonth = await GetCompanyFirstBillingMonthAsync(user);
        if (firstMonth is null) return [];

        var charges = await db.BillingCharges
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Type == CompanyMonthlyPlanType)
            .ToDictionaryAsync(x => x.BillingMonthUtc);
        var currentMonth = FirstDayOfMonth(DateTime.UtcNow.AddHours(ArgentinaUtcOffsetHours));
        var periods = new List<CompanyMonthlyPeriod>();

        for (var month = firstMonth.Value; month <= currentMonth; month = month.AddMonths(1))
        {
            var hasActivePublication = await HadActivePublicationDuringMonthAsync(user.Id, month);
            var monthNumber = MonthsBetween(firstMonth.Value, month) + 1;
            var isFree = user.Id != CompanyWithoutTrialUserId && monthNumber <= CompanyTrialMonths;
            charges.TryGetValue(month, out var charge);
            periods.Add(new CompanyMonthlyPeriod(month, hasActivePublication, isFree, charge));
        }

        periods.Reverse();
        return periods;
    }

    public Task<List<BillingCharge>> GetChargesAsync(int userId) => db.BillingCharges
        .AsNoTracking()
        .Where(x => x.UserId == userId)
        .OrderByDescending(x => x.BillingMonthUtc)
        .ThenByDescending(x => x.CreatedAtUtc)
        .ToListAsync();

    public async Task ConsumePaidPersonPublicationPackAsync(int userId)
    {
        var charge = await db.BillingCharges
            .Where(x => x.UserId == userId && x.Type == PersonPublicationType && x.Status == "Paid")
            .OrderBy(x => x.PaidAtUtc ?? x.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (charge is null) return;
        charge.Status = "Used";
        await db.SaveChangesAsync();
    }

    private Task<bool> IsEnabledAsync() => parameters.GetBoolAsync(VentaMapParameterService.PaidSiteEnabled, fallback: false);

    private Task<bool> IsEnabledForAsync(ApplicationUser user)
        => user.IsBillingExempt == 2 ? Task.FromResult(true) : IsEnabledAsync();

    private static DateTime FirstDayOfMonth(DateTime value) => new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private async Task<DateTime?> GetCompanyFirstBillingMonthAsync(ApplicationUser user)
    {
        if (user.Id == CompanyWithoutTrialUserId) return CompanyWithoutTrialStartMonth;

        var firstPublicationAt = await db.Publications
            .Where(x => x.UserId == user.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .FirstOrDefaultAsync();
        return firstPublicationAt.HasValue ? FirstDayOfMonth(firstPublicationAt.Value) : null;
    }

    private Task<bool> HadActivePublicationDuringMonthAsync(int userId, DateTime month)
    {
        var nextMonth = month.AddMonths(1);
        return db.Publications.AnyAsync(x => x.UserId == userId
            && x.CreatedAtUtc < nextMonth
            && (x.ExpiresAtUtc == null || x.ExpiresAtUtc >= month)
            && (x.DeactivatedAtUtc == null || x.DeactivatedAtUtc >= month));
    }

    private static int MonthsBetween(DateTime firstMonth, DateTime month)
        => (month.Year - firstMonth.Year) * 12 + month.Month - firstMonth.Month;
}

public sealed record CompanyMonthlyPeriod(
    DateTime MonthUtc,
    bool HasActivePublication,
    bool IsFree,
    BillingCharge? Charge);
