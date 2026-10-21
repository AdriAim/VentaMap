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
            .Include(x => x.MediaItems)
            .FirstOrDefaultAsync(x => x.Id == Id);

        if (Publication is not null)
        {
            var shortDescription = string.IsNullOrWhiteSpace(Publication.ShortDescription)
                ? Publication.Title
                : Publication.ShortDescription;
            var companyName = Publication.User?.IsCompany == true
                ? (string.IsNullOrWhiteSpace(Publication.User.CompanyName)
                    ? Publication.User.Name
                    : Publication.User.CompanyName)
                : string.Empty;

            if (Publication.User?.IsCompany == true)
            {
                // WhatsApp displays the Open Graph title in bold and the site domain below it.
                ViewData["OgTitle"] = shortDescription;
                ViewData["MetaDescription"] = $"{companyName} · {Publication.Locality}";

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

            var publicationImage = Publication.ImageList.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(publicationImage))
            {
                // WhatsApp preserves this vertical 4:5 rendition much better in
                // Status than a landscape source image.
                ViewData["OgImageUrl"] = $"/anuncio/{Publication.Id}/vista-previa.jpg";
                ViewData["OgImageWidth"] = "1080";
                ViewData["OgImageHeight"] = "1350";
                ViewData["OgImageType"] = "image/jpeg";
            }
        }

        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }
}
