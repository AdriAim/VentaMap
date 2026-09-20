using Microsoft.EntityFrameworkCore;
using System.Globalization;
using VentaMap.Data;

namespace VentaMap.Services;

public class PricingService(VentaMapDbContext db)
{
    public const decimal DefaultPersonPublicationAmount = 3000m;
    public const decimal DefaultCompanyMonthlyAmount = 50000m;

    public Task<decimal> GetCurrentPersonPublicationAmountAsync()
        => GetAmountAsync(BillingService.PersonPublicationType, DateTime.UtcNow, DefaultPersonPublicationAmount);

    public Task<decimal> GetCurrentCompanyMonthlyAmountAsync()
        => GetAmountAsync(BillingService.CompanyMonthlyPlanType, DateTime.UtcNow, DefaultCompanyMonthlyAmount);

    public Task<decimal> GetPersonPublicationAmountAsync(DateTime effectiveAtUtc)
        => GetAmountAsync(BillingService.PersonPublicationType, effectiveAtUtc, DefaultPersonPublicationAmount);

    public Task<decimal> GetCompanyMonthlyAmountAsync(DateTime effectiveAtUtc)
        => GetAmountAsync(BillingService.CompanyMonthlyPlanType, effectiveAtUtc, DefaultCompanyMonthlyAmount);

    public static string FormatAmount(decimal amount)
        => $"${amount.ToString("N0", CultureInfo.GetCultureInfo("es-AR"))}";

    private async Task<decimal> GetAmountAsync(string chargeType, DateTime effectiveAtUtc, decimal fallback)
    {
        var amount = await db.BillingRates
            .AsNoTracking()
            .Where(x => x.ChargeType == chargeType && x.EffectiveFromUtc <= effectiveAtUtc)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => (decimal?)x.Amount)
            .FirstOrDefaultAsync();
        return amount ?? fallback;
    }
}
