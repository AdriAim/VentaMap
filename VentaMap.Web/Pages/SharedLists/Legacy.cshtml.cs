using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages.SharedLists;

public class LegacyModel(SharedPublicationListService sharedPublicationListService) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var list = await sharedPublicationListService.GetPublicListBySlugAsync(slug);
        if (list is null || string.IsNullOrWhiteSpace(list.User?.CompanySlug))
        {
            return NotFound();
        }

        return RedirectToPagePermanent("/SharedLists/Details", new
        {
            companySlug = list.User.CompanySlug,
            slug = list.Slug
        });
    }
}
