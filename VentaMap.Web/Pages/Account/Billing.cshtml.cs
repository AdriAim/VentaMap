using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages.Account;

[Authorize]
public class BillingModel(CurrentUserAccessor currentUserAccessor, VentaMapDbContext db, BillingService billingService) : PageModel
{
    public ApplicationUser? UserAccount { get; private set; }
    public List<BillingCharge> Charges { get; private set; } = [];
    public bool IsMercadoPagoConfigured { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Billing") });

        UserAccount = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (UserAccount is null) return RedirectToPage("/Account/Login");
        if (!UserAccount.IsCompany) return RedirectToPage("/MyPublications");

        await billingService.EnsureCompanyCurrentMonthChargeAsync(UserAccount);
        Charges = await billingService.GetChargesAsync(userId);
        IsMercadoPagoConfigured = !string.IsNullOrWhiteSpace(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["MercadoPago:AccessToken"]);
        return Page();
    }
}
