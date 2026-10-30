using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VentaMap.Pages.Account;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccessDeniedModel : PageModel
{
    public void OnGet()
    {
    }
}
