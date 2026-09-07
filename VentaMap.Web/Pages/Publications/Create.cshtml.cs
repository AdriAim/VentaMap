using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VentaMap.Pages.Publications;

public class CreateModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return LocalRedirect("/Account/Login?returnUrl=/Publications/Create");
        }

        return Page();
    }
}
