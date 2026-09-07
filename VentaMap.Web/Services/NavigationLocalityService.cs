using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class NavigationLocalityService(VentaMapDbContext db)
{
    public const string CookieName = "ventamap_nav_locality_id";

    public async Task<IReadOnlyList<ArgentineLocality>> GetAvailableLocalitiesAsync()
    {
        return await db.ArgentineLocalities
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Province)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Locality)
            .ToListAsync();
    }

    public async Task<NavigationLocalitySelection?> GetCookieLocalityAsync(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var rawValue)
            || !int.TryParse(rawValue, out var localityId)
            || localityId <= 0)
        {
            return null;
        }

        var locality = await db.ArgentineLocalities
            .AsNoTracking()
            .Where(x => x.IsActive && x.Id == localityId)
            .Select(x => new NavigationLocalitySelection(
                x.Id,
                x.Locality,
                x.Province,
                x.Latitude,
                x.Longitude,
                true))
            .FirstOrDefaultAsync();

        return locality;
    }

    public async Task<NavigationLocalitySelection?> GetEffectiveLocalityAsync(HttpContext httpContext)
    {
        var cookieLocality = await GetCookieLocalityAsync(httpContext);
        if (cookieLocality is not null)
        {
            return cookieLocality;
        }

        var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
        {
            return null;
        }

        return await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.ArgentineLocalityId != null)
            .Select(x => new NavigationLocalitySelection(
                x.ArgentineLocality!.Id,
                x.ArgentineLocality.Locality,
                x.ArgentineLocality.Province,
                x.ArgentineLocality.Latitude,
                x.ArgentineLocality.Longitude,
                false))
            .FirstOrDefaultAsync();
    }
}

public sealed record NavigationLocalitySelection(
    int ArgentineLocalityId,
    string Locality,
    string Province,
    double Latitude,
    double Longitude,
    bool IsOverride)
{
    public string DisplayLabel => $"{Locality}, {Province}";
}
