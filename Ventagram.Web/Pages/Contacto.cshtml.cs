using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ventagram.Services;

namespace Ventagram.Pages;

public class ContactoModel(IEmailSender emailSender, IConfiguration configuration) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var emailConfigured = !string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"])
            && !string.IsNullOrWhiteSpace(configuration["Email:FromEmail"]);

        if (!emailConfigured)
        {
            ModelState.AddModelError(string.Empty, "El envio de emails no esta configurado.");
            return Page();
        }

        var safeName = WebUtility.HtmlEncode(Input.Name);
        var safeEmail = WebUtility.HtmlEncode(Input.Email);
        var safeSubject = WebUtility.HtmlEncode(Input.Subject);
        var safeMessage = WebUtility.HtmlEncode(Input.Message).Replace("\n", "<br />");
        var contactRecipient = configuration["Email:ContactRecipient"]?.Trim();

        if (string.IsNullOrWhiteSpace(contactRecipient))
        {
            ModelState.AddModelError(string.Empty, "El destinatario del formulario de contacto no esta configurado.");
            return Page();
        }

        var subject = $"Contacto Ventagram: {Input.Subject.Trim()}";
        var html = $"""
            <p>Nuevo mensaje desde el formulario de contacto de Ventagram.</p>
            <p><strong>Nombre:</strong> {safeName}</p>
            <p><strong>Email:</strong> {safeEmail}</p>
            <p><strong>Asunto:</strong> {safeSubject}</p>
            <p><strong>Mensaje:</strong><br />{safeMessage}</p>
            """;
        var text = $"""
            Nuevo mensaje desde el formulario de contacto de Ventagram.

            Nombre: {Input.Name}
            Email: {Input.Email}
            Asunto: {Input.Subject}

            Mensaje:
            {Input.Message}
            """;

        bool sent;
        try
        {
            sent = await emailSender.SendAsync(contactRecipient, subject, html, text);
        }
        catch
        {
            sent = false;
        }

        if (!sent)
        {
            ModelState.AddModelError(string.Empty, "No se pudo enviar el mensaje de contacto.");
            return Page();
        }

        SuccessMessage = "El mensaje se envió correctamente.";
        return RedirectToPage();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresa tu nombre.")]
        [StringLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu email.")]
        [EmailAddress(ErrorMessage = "Ingresa un email valido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa un asunto.")]
        [StringLength(160, ErrorMessage = "El asunto no puede superar los 160 caracteres.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Escribe tu mensaje.")]
        [StringLength(4000, MinimumLength = 10, ErrorMessage = "El mensaje debe tener entre 10 y 4000 caracteres.")]
        public string Message { get; set; } = string.Empty;
    }
}
