using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages.Publications;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class RepublishModel(PublicationService publicationService, CurrentUserAccessor currentUserAccessor) : PageModel
{
    public int PublicationId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (currentUserAccessor.UserId is not int userId) return Challenge();
        var publication = await publicationService.GetOwnedByIdAsync(id, userId);
        if (publication is null) return NotFound();
        if (publication.IsActive || publication.Status != PublicationStatus.Expired)
        {
            TempData["ErrorMessage"] = "Este anuncio ya está activo o no está disponible para republicar desde este enlace.";
            return RedirectToPage("/MyPublications");
        }

        PublicationId = id;
        return Page();
    }
}
