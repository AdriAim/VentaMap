using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages.Account;

public class LogoutModel(AuthService authService) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await authService.SignOutAsync();
        return RedirectToPage("/Index");
    }
}
