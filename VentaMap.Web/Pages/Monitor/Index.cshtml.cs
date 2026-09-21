using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages.Monitor;

public class IndexModel(AuthService authService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (!string.Equals(Input.Username.Trim(), MonitorAccess.Username, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña inválidos.");
            return Page();
        }

        var user = await authService.ValidateUserAsync(MonitorAccess.Email, Input.Password);
        if (!MonitorAccess.IsMonitor(user))
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña inválidos.");
            return Page();
        }

        await authService.SignInAsync(user!);
        return RedirectToPage("/Monitor/Panel");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresá el usuario.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresá la contraseña.")]
        public string Password { get; set; } = string.Empty;
    }
}
