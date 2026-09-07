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

    public async Task OnGetAsync()
    {
        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }
}
