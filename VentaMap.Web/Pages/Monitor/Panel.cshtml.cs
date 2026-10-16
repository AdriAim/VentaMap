using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages.Monitor;

[Authorize]
public class PanelModel(VentaMapDbContext db, CurrentUserAccessor currentUserAccessor) : PageModel
{
    public List<DailyMetric> Metrics { get; private set; } = [];
    public List<SiteSuggestion> RecentSuggestions { get; private set; } = [];
    public int LatestSuggestionId { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var accessResult = await EnsureMonitorAccessAsync();
        if (accessResult is not null) return accessResult;

        var today = DateTime.UtcNow.AddHours(-3).Date;
        var from = today.AddDays(-29);
        var until = today.AddDays(1);
        var utcFrom = from.AddHours(3);
        var utcUntil = until.AddHours(3);

        var newUsers = await db.Users.AsNoTracking()
            .Where(x => !x.IsDebugUser && x.Email != MonitorAccess.Email && x.CreatedAtUtc >= utcFrom && x.CreatedAtUtc < utcUntil)
            .Select(x => new MonitorUser(x.Id, x.Name, x.Email, x.CreatedAtUtc))
            .ToListAsync();
        var newPublications = await db.Publications.AsNoTracking()
            .Where(x => (x.User == null || !x.User.IsDebugUser)
                && (x.OriginalCreatedAtUtc ?? x.CreatedAtUtc) >= utcFrom
                && (x.OriginalCreatedAtUtc ?? x.CreatedAtUtc) < utcUntil)
            .Select(x => new MonitorPublication(
                x.Id,
                x.User == null ? "Anónimo" : x.User.Name,
                x.ShortDescription,
                x.Locality,
                x.Price,
                x.Currency,
                x.OriginalCreatedAtUtc ?? x.CreatedAtUtc))
            .ToListAsync();
        var republications = await db.PublicationRepublications.AsNoTracking()
            .Where(x => (x.Publication.User == null || !x.Publication.User.IsDebugUser)
                && x.RepublishedAtUtc >= utcFrom && x.RepublishedAtUtc < utcUntil)
            .Select(x => x.RepublishedAtUtc)
            .ToListAsync();
        var visitors = await db.SiteVisits.AsNoTracking()
            .Where(x => x.VisitedOn >= from && x.VisitedOn < until)
            .GroupBy(x => x.VisitedOn)
            .Select(x => new { Day = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count);

        var usersByDay = newUsers.GroupBy(x => ToArgentinaDay(x.CreatedAtUtc)).ToDictionary(x => x.Key, x => x.ToList());
        var publicationsByDay = newPublications.GroupBy(x => ToArgentinaDay(x.CreatedAtUtc)).ToDictionary(x => x.Key, x => x.ToList());
        var republicationCounts = republications.GroupBy(ToArgentinaDay).ToDictionary(x => x.Key, x => x.Count());
        RecentSuggestions = await db.SiteSuggestions.AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.Id)
            .Take(10)
            .ToListAsync();
        LatestSuggestionId = RecentSuggestions.Count == 0 ? 0 : RecentSuggestions.Max(x => x.Id);
        Metrics = Enumerable.Range(0, 30)
            .Select(offset => today.AddDays(-offset))
            .Select(day => new DailyMetric(
                day,
                usersByDay.GetValueOrDefault(day, []),
                publicationsByDay.GetValueOrDefault(day, []),
                republicationCounts.GetValueOrDefault(day),
                visitors.GetValueOrDefault(day)))
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnGetSuggestionsAsync(int afterId = 0)
    {
        var accessResult = await EnsureMonitorAccessAsync();
        if (accessResult is not null) return accessResult;

        var suggestions = await db.SiteSuggestions.AsNoTracking()
            .Where(x => x.Id > Math.Max(0, afterId))
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Message,
                SenderName = x.SenderName ?? x.User!.Name,
                SenderEmail = x.SenderEmail ?? x.User!.Email,
                x.CreatedAtUtc
            })
            .Take(20)
            .ToListAsync();

        return new JsonResult(suggestions);
    }

    private async Task<IActionResult?> EnsureMonitorAccessAsync()
    {
        var currentUser = currentUserAccessor.UserId is int userId
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId)
            : null;
        return MonitorAccess.IsMonitor(currentUser) ? null : Forbid();
    }

    private static DateTime ToArgentinaDay(DateTime utc) => utc.AddHours(-3).Date;

    public sealed record MonitorUser(int Id, string Name, string Email, DateTime CreatedAtUtc);
    public sealed record MonitorPublication(int Id, string UserName, string ShortDescription, string Locality, decimal Price, string Currency, DateTime CreatedAtUtc);
    public sealed record DailyMetric(
        DateTime Day,
        IReadOnlyList<MonitorUser> NewUsers,
        IReadOnlyList<MonitorPublication> NewPublications,
        int RepublishedPublications,
        int Visitors)
    {
        public int RegisteredUsers => NewUsers.Count;
    }
}
