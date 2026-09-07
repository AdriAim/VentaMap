using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages;

public class PreciosModel(VentaMapParameterService parameters) : PageModel
{
    public bool IsPaidSiteEnabled { get; private set; }

    public async Task OnGetAsync()
    {
        IsPaidSiteEnabled = await parameters.GetBoolAsync(VentaMapParameterService.PaidSiteEnabled, fallback: false);
    }
}
