using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;
using VentaMap.ViewModels;

namespace VentaMap.Pages;

[Authorize]
public class FavoritesModel(
    FavoriteService favoriteService,
    CurrentUserAccessor currentUserAccessor,
    VentaMapDbContext db) : PageModel
{
    public List<FavoriteListSummaryViewModel> FavoriteLists { get; private set; } = [];

    public List<PublicationReportReason> ReportReasons { get; private set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Favorites") });
        }

        FavoriteLists = await favoriteService.GetListSummariesAsync(userId);
        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostRenameAsync(int listId, string? name)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Favorites") });
        }

        try
        {
            await favoriteService.RenameListAsync(userId, listId, name);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int listId)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Favorites") });
        }

        if (await favoriteService.DeleteListAsync(userId, listId))
        {
            SuccessMessage = "Lista de favoritos eliminada.";
        }
        else
        {
            ErrorMessage = "No se pudo eliminar la lista.";
        }

        return RedirectToPage();
    }
}
