using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages.Account;

public class ResetPasswordModel(AuthService authService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? StatusMessage { get; set; }

    public IActionResult OnGet(string email, string token)
    {
        Input.Email = email;
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await authService.ResetPasswordAsync(Input.Email, Input.Token, Input.Password);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "No se pudo actualizar la contraseña.");
            return Page();
        }

        StatusMessage = "La contraseña fue actualizada. Ya podés ingresar.";
        return Page();
    }

    public class InputModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresá una contraseña nueva.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repetí la contraseña.")]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
