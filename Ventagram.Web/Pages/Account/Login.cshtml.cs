using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ventagram.Services;

namespace Ventagram.Pages.Account;

public class LoginModel(AuthService authService, IConfiguration configuration) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool IsGoogleEnabled => !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]);

    public void OnGet()
    {
    }

    public IActionResult OnGetSession()
    {
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(new { isAuthenticated = User.Identity?.IsAuthenticated ?? false });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await authService.ValidateUserAsync(Input.Email, Input.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email o contraseña inválidos.");
            return Page();
        }

        await authService.SignInAsync(user);
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/Account/Login");
    }

    public IActionResult OnPostGoogle()
    {
        if (!IsGoogleEnabled)
        {
            ModelState.AddModelError(string.Empty, "Google no está configurado.");
            return Page();
        }

        var properties = new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/Account/Login" };
        return Challenge(properties, "Google");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresá tu email.")]
        [EmailAddress(ErrorMessage = "Ingresá un email válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresá tu contraseña.")]
        public string Password { get; set; } = string.Empty;
    }
}
