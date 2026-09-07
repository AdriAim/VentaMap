using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages.Account;

[Authorize]
public class ChangeLocalityModel(
    VentaMapDbContext db,
    CurrentUserAccessor currentUserAccessor,
    ArgentineLocalityLookupService localityLookupService,
    PublicationGroupPreferenceService publicationGroupPreferenceService,
    PublicationGroupTypeService publicationGroupTypeService) : PageModel
{
    private const string LocalityField = $"{nameof(Input)}.{nameof(InputModel.ArgentineLocalityId)}";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public string SelectedLocalityLabel { get; private set; } = string.Empty;

    public IReadOnlyList<PublicationGroupType> HeaderGroupOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadHeaderGroupOptionsAsync();

        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangeLocality") });
        }

        var user = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.ArgentineLocalityId,
                x.HeaderPublicationGroupsCsv,
                LocalityLabel = x.ArgentineLocality == null
                    ? string.Empty
                    : (x.ArgentineLocality.Locality + ", " + x.ArgentineLocality.Province)
            })
            .FirstOrDefaultAsync();
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangeLocality") });
        }

        Input.ArgentineLocalityId = user.ArgentineLocalityId;
        Input.ArgentineLocalityLabel = user.LocalityLabel;
        Input.HeaderPublicationGroups = PublicationGroupPreferenceService.NormalizeGroupNames(user.HeaderPublicationGroupsCsv);
        SyncSelectedLocalityLabel();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadHeaderGroupOptionsAsync();

        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangeLocality") });
        }

        if (!await TryResolveLocalityAsync())
        {
            SyncSelectedLocalityLabel();
            return Page();
        }

        SyncSelectedLocalityLabel();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangeLocality") });
        }

        user.ArgentineLocalityId = Input.ArgentineLocalityId;
        user.HeaderPublicationGroupsCsv = await publicationGroupPreferenceService.NormalizeGroupNamesCsvAsync(Input.HeaderPublicationGroups);
        await db.SaveChangesAsync();

        SuccessMessage = "Localidad y accesos rápidos actualizados.";
        return RedirectToPage();
    }

    private async Task<bool> TryResolveLocalityAsync()
    {
        try
        {
            var locality = await localityLookupService.EnsureLocalityAsync(
                Input.ArgentineLocalityId,
                Input.SelectedExternalLocalityId,
                HttpContext.RequestAborted);

            Input.ArgentineLocalityId = locality.Id;
            Input.ArgentineLocalityLabel = $"{locality.Locality}, {locality.Province}";
            Input.SelectedExternalLocalityId = string.Empty;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(LocalityField, ex.Message);
            return false;
        }
    }

    private void SyncSelectedLocalityLabel()
    {
        SelectedLocalityLabel = !string.IsNullOrWhiteSpace(Input.ArgentineLocalityLabel)
            ? Input.ArgentineLocalityLabel
            : string.Empty;
    }

    private async Task LoadHeaderGroupOptionsAsync()
    {
        HeaderGroupOptions = await publicationGroupTypeService.GetActiveAsync();
    }

    public class InputModel
    {
        public int? ArgentineLocalityId { get; set; }

        public string ArgentineLocalityLabel { get; set; } = string.Empty;

        public string SelectedExternalLocalityId { get; set; } = string.Empty;

        public List<string> HeaderPublicationGroups { get; set; } = [];
    }
}
