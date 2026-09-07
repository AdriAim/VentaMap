using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Services;

namespace VentaMap.Pages.Account;

[Authorize]
public class ChangePasswordModel(VentaMapDbContext db, CurrentUserAccessor currentUserAccessor) : PageModel
{
    private const string CurrentPasswordField = $"{nameof(Input)}.{nameof(InputModel.CurrentPassword)}";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet()
    {
        return currentUserAccessor.UserId is null
            ? RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangePassword") })
            : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangePassword") });
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangePassword") });
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Esta cuenta no tiene una contrasena local configurada.");
            return Page();
        }

        if (!string.Equals(user.PasswordHash, AuthService.HashPassword(Input.CurrentPassword), StringComparison.Ordinal))
        {
            ModelState.AddModelError(CurrentPasswordField, "La contrasena actual no es correcta.");
            return Page();
        }

        user.PasswordHash = AuthService.HashPassword(Input.NewPassword);
        await db.SaveChangesAsync();

        SuccessMessage = "Contrasena actualizada.";
        return RedirectToPage();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresa tu contrasena actual.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa una nueva contrasena.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repite la nueva contrasena.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Las contrasenas no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
