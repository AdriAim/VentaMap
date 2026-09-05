using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;
using Ventagram.Services;
using Ventagram.ViewModels;

namespace Ventagram.Pages.SharedLists;

[Authorize]
public class SelectModel(
    PublicationService publicationService,
    CurrentUserAccessor currentUserAccessor,
    VentagramDbContext db,
    SharedPublicationListService sharedPublicationListService) : PageModel
{
    public SharedPublicationList? SharedList { get; private set; }
    public List<Publication> SelectedPublications { get; private set; } = [];
    public List<MyPublicationAdminItemViewModel> AvailablePublications { get; private set; } = [];
    public int ActivePublicationCount { get; private set; }
    public string Mode { get; private set; } = "Lista";

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, string? mode = null)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/SharedLists/Select", new { id }) });
        }

        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsCompany))
        {
            return Forbid();
        }

        Mode = NormalizeMode(mode);
        SharedList = await sharedPublicationListService.GetOwnedListAsync(userId, id);
        if (SharedList is null)
        {
            return NotFound();
        }

        SelectedPublications = SharedList.Items
            .Select(x => x.Publication)
            .Where(x => x.IsActive)
            .ToList();

        var selectedIds = SelectedPublications.Select(x => x.Id).ToHashSet();
        var activePublications = (await publicationService.GetOwnedPublicationsAsync(userId))
            .Where(x => x.Publication.IsActive)
            .ToList();
        ActivePublicationCount = activePublications.Count;
        AvailablePublications = activePublications
            .Where(x => !selectedIds.Contains(x.Publication.Id))
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(int id, List<int>? publicationIds, string? mode)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/SharedLists/Select", new { id }) });
        }

        if (!await sharedPublicationListService.AddPublicationsAsync(userId, id, publicationIds))
        {
            ErrorMessage = "No se pudo actualizar la lista compartible.";
        }
        else if (publicationIds?.Count > 0)
        {
            SuccessMessage = publicationIds.Count == 1 ? "Anuncio agregado a la lista." : "Anuncios agregados a la lista.";
        }

        return RedirectToPage(new { id, mode = NormalizeMode(mode) });
    }

    public async Task<IActionResult> OnPostRemoveAsync(int id, int publicationId, string? mode)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/SharedLists/Select", new { id }) });
        }

        if (!await sharedPublicationListService.RemovePublicationAsync(userId, id, publicationId))
        {
            ErrorMessage = "No se pudo quitar el anuncio de la lista.";
        }

        return RedirectToPage(new { id, mode = NormalizeMode(mode) });
    }

    public async Task<IActionResult> OnPostClearAsync(int id, string? mode)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/SharedLists/Select", new { id }) });
        }

        if (!await sharedPublicationListService.ClearAsync(userId, id))
        {
            ErrorMessage = "No se pudo limpiar la lista.";
        }

        return RedirectToPage(new { id, mode = NormalizeMode(mode) });
    }

    private static string NormalizeMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "galeria" => "Galeria",
        "mapa" => "Mapa",
        _ => "Lista"
    };
}
