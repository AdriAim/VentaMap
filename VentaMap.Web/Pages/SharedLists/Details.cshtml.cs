using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages.SharedLists;

public class DetailsModel(SharedPublicationListService sharedPublicationListService) : PageModel
{
    public SharedPublicationList? SharedList { get; private set; }
    public List<Publication> Publications { get; private set; } = [];
    public string OwnerName { get; private set; } = string.Empty;
    public string Mode { get; private set; } = "Galeria";

    public async Task<IActionResult> OnGetAsync(string companySlug, string slug, string? mode = null)
    {
        SharedList = await sharedPublicationListService.GetPublicListBySlugAsync(slug);
        if (SharedList is null || !string.Equals(SharedList.User?.CompanySlug, companySlug, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        Mode = string.IsNullOrWhiteSpace(mode)
            ? SharedPublicationListService.NormalizeMode(SharedList.DefaultMode)
            : SharedPublicationListService.NormalizeMode(mode);

        Publications = SharedList.Items
            .Select(x => x.Publication)
            .Where(x => x.IsActive)
            .ToList();
        OwnerName = SharedList.User?.CompanyName ?? SharedList.User?.Name ?? "VentaMap";

        return Page();
    }
}
