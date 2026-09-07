using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Pages;

public class BrowseModel(IConfiguration configuration, VentaMapDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Group { get; set; } = "Inmuebles";

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "Galeria";

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Operation { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? PriceFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? PriceTo { get; set; }

    [BindProperty(SupportsGet = true, Name = "radioKm")]
    public int? RadiusKm { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;

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
