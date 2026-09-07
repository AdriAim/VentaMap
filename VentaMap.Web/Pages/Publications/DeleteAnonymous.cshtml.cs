using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages.Publications;

public class DeleteAnonymousModel(PublicationService publicationService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var success = await publicationService.DeleteAnonymousPermanentlyAsync(Input.PublicationId, Input.Password);
        StatusMessage = success
            ? "El anuncio fue eliminado definitivamente."
            : "No se pudo eliminar. Revisa ID y contraseña.";
        return Page();
    }

    public class InputModel
    {
        [Required]
        public int PublicationId { get; set; }

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
