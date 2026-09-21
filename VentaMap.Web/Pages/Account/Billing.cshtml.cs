using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages.Account;

[Authorize]
public class BillingModel(CurrentUserAccessor currentUserAccessor, VentaMapDbContext db, BillingService billingService, PricingService pricingService, VentaMapParameterService parameters) : PageModel
{
    public ApplicationUser? UserAccount { get; private set; }
    public List<BillingCharge> Charges { get; private set; } = [];
    public List<CompanyMonthlyPeriod> MonthlyPeriods { get; private set; } = [];
    public bool IsMercadoPagoConfigured { get; private set; }
    public bool HasOverdueMonthlyDebt { get; private set; }
    public string CurrentPersonPublicationAmountText { get; private set; } = string.Empty;
    public string CurrentCompanyMonthlyAmountText { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Billing") });

        UserAccount = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (UserAccount is null) return RedirectToPage("/Account/Login");
        if (!UserAccount.IsCompany) return RedirectToPage("/MyPublications");
        var billingEnabled = UserAccount.IsBillingExempt == 2
            || await parameters.GetBoolAsync(VentaMapParameterService.PaidSiteEnabled, fallback: false);
        if (!billingEnabled) return RedirectToPage("/MyPublications");

        MonthlyPeriods = await billingService.GetCompanyMonthlyPeriodsAsync(UserAccount);
        Charges = await billingService.GetChargesAsync(userId);
        HasOverdueMonthlyDebt = await billingService.IsPublishingBlockedAsync(UserAccount);
        CurrentPersonPublicationAmountText = PricingService.FormatAmount(await pricingService.GetCurrentPersonPublicationAmountAsync());
        CurrentCompanyMonthlyAmountText = PricingService.FormatAmount(await pricingService.GetCurrentCompanyMonthlyAmountAsync());
        IsMercadoPagoConfigured = !string.IsNullOrWhiteSpace(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["MercadoPago:AccessToken"]);
        return Page();
    }
}
