using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VentaMap.Pages.Publications;

public class ExpressModel : PageModel
{
    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true
        ? Page()
        : LocalRedirect("/Account/Login?returnUrl=/Publications/Express");
}
