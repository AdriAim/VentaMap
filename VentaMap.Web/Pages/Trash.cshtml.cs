using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Models;
using VentaMap.Services;
using VentaMap.ViewModels;

namespace VentaMap.Pages;

[Authorize]
public class TrashModel(
    PublicationService publicationService,
    ReportService reportService,
    SuggestionService suggestionService,
    CurrentUserAccessor currentUserAccessor) : PageModel
{
    public List<ModerationQueueItemViewModel> Publications { get; private set; } = [];
    public List<VoluntaryDeactivationQueueItemViewModel> VoluntaryDeactivations { get; private set; } = [];
    public List<SiteSuggestion> Suggestions { get; private set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!currentUserAccessor.IsAdmin)
        {
            return Forbid();
        }

        Publications = await publicationService.GetModerationQueueAsync();
        VoluntaryDeactivations = await publicationService.GetVoluntaryDeactivationQueueAsync();
        Suggestions = await suggestionService.GetRecentAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreAsync(int id)
    {
        if (!currentUserAccessor.IsAdmin || currentUserAccessor.UserId is not int userId)
        {
            return Forbid();
        }

        var result = await reportService.RestorePublicationAsync(id, userId);
        if (result.Success)
        {
            SuccessMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        if (!currentUserAccessor.IsAdmin || currentUserAccessor.UserId is not int userId)
        {
            return Forbid();
        }

        var result = await reportService.ConfirmTrashAsync(id, userId);
        if (result.Success)
        {
            SuccessMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }
}
