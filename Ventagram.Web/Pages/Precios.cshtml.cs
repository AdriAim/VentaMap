using Microsoft.AspNetCore.Mvc.RazorPages;
using Ventagram.Services;

namespace Ventagram.Pages;

public class PreciosModel(VentagramParameterService parameters) : PageModel
{
    public bool IsPaidSiteEnabled { get; private set; }

    public async Task OnGetAsync()
    {
        IsPaidSiteEnabled = await parameters.GetBoolAsync(VentagramParameterService.PaidSiteEnabled, fallback: false);
    }
}
