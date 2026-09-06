using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ventagram.Services;

namespace Ventagram.Pages.Account;

public class ForgotPasswordModel(AuthService authService, IEmailSender emailSender, IConfiguration configuration) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? StatusMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await authService.FindUserByEmailAsync(Input.Email);
        if (user is null)
        {
            StatusMessage = "Si el email existe, enviamos un enlace de recuperacion.";
            return Page();
        }

        var token = authService.GeneratePasswordResetToken(user);
        var resetUrl = Url.Page(
            "/Account/ResetPassword",
            pageHandler: null,
            values: new { email = user.Email, token },
            protocol: Request.Scheme,
            host: Request.Host.Value);

        if (string.IsNullOrWhiteSpace(resetUrl))
        {
            ModelState.AddModelError(string.Empty, "No se pudo generar el enlace de recuperacion.");
            return Page();
        }

        var emailConfigured = !string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"])
            && !string.IsNullOrWhiteSpace(configuration["Email:FromEmail"]);

        if (!emailConfigured)
        {
            ModelState.AddModelError(string.Empty, "El envio de emails no esta configurado.");
            return Page();
        }

        var subject = "Recuperar contraseña de VentaMap";
        var html = $"""
            <p>Hiciste una solicitud para blanquear tu contraseña.</p>
            <p><a href="{resetUrl}">Abrir enlace de recuperacion</a></p>
            <p>Si no solicitaste este cambio, ignora este correo.</p>
            """;
        var text = $"Hiciste una solicitud para blanquear tu contraseña.\nAbrí este enlace: {resetUrl}\nSi no solicitaste este cambio, ignora este correo.";

        bool sent;
        try
        {
            sent = await emailSender.SendAsync(user.Email, subject, html, text);
        }
        catch
        {
            sent = false;
        }

        if (!sent)
        {
            ModelState.AddModelError(string.Empty, "No se pudo enviar el correo de recuperacion.");
            return Page();
        }

        StatusMessage = "Si el email existe, enviamos un enlace de recuperacion.";
        return Page();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresá tu email.")]
        [EmailAddress(ErrorMessage = "Ingresá un email válido.")]
        public string Email { get; set; } = string.Empty;
    }
}
