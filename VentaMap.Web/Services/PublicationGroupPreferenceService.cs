using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class PublicationGroupPreferenceService(
    VentaMapDbContext db,
    IHttpContextAccessor httpContextAccessor,
    PublicationGroupTypeService publicationGroupTypeService)
{
    public const string CookieName = "ventamap_header_groups";
    public const int MaxHeaderGroups = 5;

    public async Task<List<PublicationGroupType>> GetHeaderGroupsAsync(HttpContext httpContext)
    {
        var activeGroups = await publicationGroupTypeService.GetActiveAsync();
        var activeByName = activeGroups.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var preferredNames = await GetPreferredGroupNamesAsync(httpContext);
        var completedPreferredNames = AddFallbackGroupsWhenUnderFour(preferredNames, activeByName);
        var selected = completedPreferredNames
            .Where(activeByName.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxHeaderGroups)
            .Select(x => activeByName[x])
            .ToList();

        return selected.Count > 0
            ? selected
            : activeGroups.Take(MaxHeaderGroups).ToList();
    }

    public async Task<List<string>> GetPreferredGroupNamesAsync(HttpContext httpContext)
    {
        if (TryGetCurrentUserId(httpContext) is int userId)
        {
            var csv = await db.Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => x.HeaderPublicationGroupsCsv)
                .FirstOrDefaultAsync();

            return NormalizeGroupNames(csv);
        }

        return httpContext.Request.Cookies.TryGetValue(CookieName, out var cookieValue)
            ? NormalizeGroupNames(cookieValue)
            : [];
    }

    public async Task<string> NormalizeGroupNamesCsvAsync(IEnumerable<string?> rawNames)
    {
        var activeGroups = await publicationGroupTypeService.GetActiveAsync();
        var activeByName = activeGroups.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var names = rawNames
            .SelectMany(SplitGroupNames)
            .Where(activeByName.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxHeaderGroups)
            .ToList();

        names = AddFallbackGroupsWhenUnderFour(names, activeByName);

        return string.Join(",", names.Select(x => activeByName[x].Name));
    }

    public void WriteGuestCookie(string csv)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(csv))
        {
            httpContext.Response.Cookies.Delete(CookieName);
            return;
        }

        httpContext.Response.Cookies.Append(CookieName, csv, new CookieOptions
        {
            Path = "/",
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });
    }

    public static List<string> NormalizeGroupNames(string? csv)
    {
        return SplitGroupNames(csv)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxHeaderGroups)
            .ToList();
    }

    public static string BuildDefaultCsv(IEnumerable<PublicationGroupType> groups)
    {
        return string.Join(",", groups
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Take(MaxHeaderGroups)
            .Select(x => x.Name));
    }

    private static IEnumerable<string> SplitGroupNames(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static List<string> AddFallbackGroupsWhenUnderFour(
        IReadOnlyCollection<string> names,
        IReadOnlyDictionary<string, PublicationGroupType> activeByName)
    {
        var result = names
            .Where(activeByName.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxHeaderGroups)
            .ToList();

        if (result.Count is <= 0 or >= 4)
        {
            return result.Take(MaxHeaderGroups).ToList();
        }

        foreach (var fallbackGroup in new[] { "Generales", "Inmuebles" })
        {
            if (result.Count >= MaxHeaderGroups)
            {
                break;
            }

            if (activeByName.ContainsKey(fallbackGroup)
                && !result.Contains(fallbackGroup, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(fallbackGroup);
            }
        }

        return result.Take(MaxHeaderGroups).ToList();
    }

    private static int? TryGetCurrentUserId(HttpContext httpContext)
    {
        var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) && userId > 0 ? userId : null;
    }
}
