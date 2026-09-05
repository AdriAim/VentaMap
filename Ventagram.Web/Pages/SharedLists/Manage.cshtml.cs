using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Services;
using Ventagram.ViewModels;

namespace Ventagram.Pages.SharedLists;

[Authorize]
public class ManageModel(
    CurrentUserAccessor currentUserAccessor,
    VentagramDbContext db,
    SharedPublicationListService sharedPublicationListService) : PageModel
{
    public List<SharedPublicationListSummaryViewModel> SharedLists { get; private set; } = [];
    public string CreateListPlaceholder { get; private set; } = "Ej. Selección destacada de agosto";

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/SharedLists/Manage") });
        }

        var user = await GetCompanyUserAsync(userId);
        if (user is null)
        {
            return Forbid();
        }

        CreateListPlaceholder = BuildCreateListPlaceholder(user.CompanyIndustry);
        SharedLists = await sharedPublicationListService.GetOwnedSummariesAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(string listName)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/SharedLists/Manage") });
        }

        if (await GetCompanyUserAsync(userId) is null)
        {
            return Forbid();
        }

        try
        {
            var list = await sharedPublicationListService.CreateAsync(userId, listName);
            SuccessMessage = "La lista fue creada. Ahora podés elegir sus anuncios.";
            return RedirectToPage("/SharedLists/Select", new { id = list.Id });
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int listId)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/SharedLists/Manage") });
        }

        if (await sharedPublicationListService.DeleteAsync(userId, listId))
        {
            SuccessMessage = "La lista compartible fue eliminada.";
        }
        else
        {
            ErrorMessage = "No se pudo eliminar la lista compartible.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDefaultModeAsync(int listId, string? defaultMode)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            if (IsAjaxRequest())
            {
                return new JsonResult(new { success = false, message = "Debes iniciar sesión nuevamente." }) { StatusCode = StatusCodes.Status401Unauthorized };
            }

            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/SharedLists/Manage") });
        }

        if (!await sharedPublicationListService.UpdateDefaultModeAsync(userId, listId, defaultMode))
        {
            ErrorMessage = "No se pudo actualizar el modo por defecto.";
            if (IsAjaxRequest())
            {
                return new JsonResult(new { success = false, message = ErrorMessage }) { StatusCode = StatusCodes.Status400BadRequest };
            }
        }
        else
        {
            SuccessMessage = "Modo por defecto actualizado.";
            if (IsAjaxRequest())
            {
                return new JsonResult(new
                {
                    success = true,
                    message = SuccessMessage,
                    defaultMode = SharedPublicationListService.NormalizeMode(defaultMode)
                });
            }
        }

        return RedirectToPage();
    }

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
        Request.Headers.Accept.Any(x => x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    private Task<CompanyUserInfo?> GetCompanyUserAsync(int userId) =>
        db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.IsCompany)
            .Select(x => new CompanyUserInfo(x.CompanyIndustry))
            .FirstOrDefaultAsync();

    private static string BuildCreateListPlaceholder(string? companyIndustry)
    {
        var industry = (companyIndustry ?? string.Empty).Trim().ToLowerInvariant();

        if (industry.Contains("inmobili") || industry.Contains("propiedad") || industry.Contains("inmueble"))
        {
            return "Ej. Departamentos premium de agosto";
        }

        if (industry.Contains("auto") || industry.Contains("vehiculo") || industry.Contains("vehículo") || industry.Contains("moto"))
        {
            return "Ej. Usados seleccionados de la semana";
        }

        if (industry.Contains("moda") || industry.Contains("ropa") || industry.Contains("indumentaria") || industry.Contains("calzado"))
        {
            return "Ej. Looks de temporada";
        }

        if (industry.Contains("tecnologia") || industry.Contains("tecnología") || industry.Contains("celular") || industry.Contains("electronica") || industry.Contains("electrónica"))
        {
            return "Ej. Equipos destacados para renovar";
        }

        if (industry.Contains("servicio"))
        {
            return "Ej. Servicios destacados del mes";
        }

        return "Ej. Selección destacada de agosto";
    }

    private sealed record CompanyUserInfo(string? CompanyIndustry);
}
