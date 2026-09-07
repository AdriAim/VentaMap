using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages;

public class CompanyProfileModel(VentaMapDbContext db, IConfiguration configuration, FavoriteService favoriteService) : PageModel
{
    public ApplicationUser? Company { get; private set; }
    public List<Publication> Publications { get; private set; } = [];
    public List<PublicationReportReason> ReportReasons { get; private set; } = [];
    public HashSet<int> FavoritePublicationIds { get; private set; } = [];
    public string MapStyleUrl { get; private set; } = string.Empty;
    public string MapTilesUrlTemplate { get; private set; } = string.Empty;
    public string MapAttributionHtml { get; private set; } = string.Empty;
    public bool DebugEnabled { get; private set; }

    public async Task<IActionResult> OnGetAsync(string companySlug)
    {
        var normalizedSlug = (companySlug ?? string.Empty).Trim().ToLowerInvariant();
        DebugEnabled = string.Equals(Request.Query["debug"], "1", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            return NotFound();
        }

        Company = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsCompany && x.CompanySlug == normalizedSlug);
        if (Company is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        Publications = await db.Publications
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Where(x => x.UserId == Company.Id
                && x.IsActive
                && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now)
                && (DebugEnabled || x.UserId == null || x.User == null || !x.User.IsDebugUser))
            .OrderByDescending(x => x.Featured)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        if (User.Identity?.IsAuthenticated == true &&
            int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var currentUserId))
        {
            FavoritePublicationIds = await favoriteService.GetFavoritePublicationIdsAsync(currentUserId, Publications.Select(x => x.Id));
        }

        MapStyleUrl = configuration["Map:StyleUrl"] ?? string.Empty;
        MapTilesUrlTemplate = configuration["Map:TilesUrlTemplate"] ?? string.Empty;
        MapAttributionHtml = configuration["Map:AttributionHtml"] ?? string.Empty;
        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Page();
    }
}
