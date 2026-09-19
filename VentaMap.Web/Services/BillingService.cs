using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class BillingService(VentaMapDbContext db, VentaMapParameterService parameters)
{
    public const decimal PersonPublicationAmount = 3000m;
    public const decimal CompanyMonthlyAmount = 50000m;
    public const int CompanyActivePublicationLimit = 50;
    public const int CompanyTrialMonths = 3;
    public const string PersonPublicationType = "PersonPublication";
    public const string CompanyMonthlyPlanType = "CompanyMonthlyPlan";

    public async Task<bool> IsPublishingBlockedAsync(ApplicationUser user)
    {
        if (!await IsEnabledForAsync(user)) return false;
        if (user.IsBillingExempt == 1) return false;
        await EnsureCompanyCurrentMonthChargeAsync(user);

        var unpaid = await db.BillingCharges.CountAsync(x => x.UserId == user.Id && x.Status == "Pending");
        // A person must pay its publication pack before continuing. Companies can
        // keep one unpaid month, but a second one blocks the catalogue.
        return user.IsCompany ? unpaid > 1 : unpaid > 0;
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

        var pending = await db.BillingCharges
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Type == PersonPublicationType && x.Status == "Pending");
        if (pending is not null)
        {
            // Older pending charges (created before publications had a payment
            // status) may not be linked to an ad. Attach one when it becomes
            // available so the payment action is visible in Mis anuncios.
            if (pending.PublicationId is null && publicationId.HasValue)
            {
                pending.PublicationId = publicationId.Value;
                await db.SaveChangesAsync();
            }

            return pending;
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
            Amount = PersonPublicationAmount,
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
        if (!await IsEnabledForAsync(user)) return;
        if (!user.IsCompany || user.IsBillingExempt == 1) return;

        var now = DateTime.UtcNow;
        if (now < user.CreatedAtUtc.AddMonths(CompanyTrialMonths)) return;

        var month = FirstDayOfMonth(now);

        var exists = await db.BillingCharges.AnyAsync(x =>
            x.UserId == user.Id && x.Type == CompanyMonthlyPlanType && x.BillingMonthUtc == month);
        if (exists) return;

        db.BillingCharges.Add(new BillingCharge
        {
            UserId = user.Id,
            Type = CompanyMonthlyPlanType,
            Amount = CompanyMonthlyAmount,
            BillingMonthUtc = month,
            Description = $"Plan empresa mensual (hasta {CompanyActivePublicationLimit} anuncios activos)",
            Reference = $"company-{month:yyyy-MM}",
            CreatedAtUtc = now
        });
        await db.SaveChangesAsync();
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
}
