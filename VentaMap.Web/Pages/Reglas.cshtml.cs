using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages;

public class ReglasModel(VentaMapParameterService parameters) : PageModel
{
    public int PersonalPublicationDurationDays { get; private set; } = 60;

    public async Task OnGetAsync()
    {
        PersonalPublicationDurationDays = await parameters.GetIntAsync(
            VentaMapParameterService.PersonalPublicationDurationDays, fallback: 60, min: 1, max: 365);
    }
}
