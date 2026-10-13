using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Pages.Publications;

public class DetailsModel(VentaMapDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public List<PublicationReportReason> ReportReasons { get; private set; } = [];
    public Publication? Publication { get; private set; }

    public async Task OnGetAsync()
    {
        Publication = await db.Publications
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == Id);

        if (Publication is not null)
        {
            var shortDescription = string.IsNullOrWhiteSpace(Publication.ShortDescription)
                ? Publication.Title
                : Publication.ShortDescription;

            if (Publication.User?.IsCompany == true)
            {
                var companyName = string.IsNullOrWhiteSpace(Publication.User.CompanyName)
                    ? Publication.User.Name
                    : Publication.User.CompanyName;
                // WhatsApp displays the Open Graph title in bold and the site domain below it.
                ViewData["OgTitle"] = companyName;
                ViewData["MetaDescription"] = shortDescription;

                if (!string.IsNullOrWhiteSpace(Publication.User.CompanyLogoUrl))
                {
                    ViewData["OgImageUrl"] = Publication.User.CompanyLogoUrl;
                }
            }
            else
            {
                ViewData["OgTitle"] = "Mira mi anuncio:";
                ViewData["MetaDescription"] = shortDescription;
            }
        }

        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }
}
