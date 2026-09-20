using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages;

public class PreciosModel(VentaMapParameterService parameters, PricingService pricingService) : PageModel
{
    public bool IsPaidSiteEnabled { get; private set; }
    public string PersonPublicationAmountText { get; private set; } = string.Empty;
    public string CompanyMonthlyAmountText { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        IsPaidSiteEnabled = await parameters.GetBoolAsync(VentaMapParameterService.PaidSiteEnabled, fallback: false);
        PersonPublicationAmountText = PricingService.FormatAmount(await pricingService.GetCurrentPersonPublicationAmountAsync());
        CompanyMonthlyAmountText = PricingService.FormatAmount(await pricingService.GetCurrentCompanyMonthlyAmountAsync());
    }
}
