using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Services;

namespace VentaMap.Pages.Monitor;

[Authorize]
public class PanelModel(VentaMapDbContext db, CurrentUserAccessor currentUserAccessor) : PageModel
{
    public List<DailyMetric> Metrics { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var currentUser = currentUserAccessor.UserId is int userId
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId)
            : null;
        if (!MonitorAccess.IsMonitor(currentUser)) return Forbid();

        var today = DateTime.UtcNow.AddHours(-3).Date;
        var from = today.AddDays(-29);
        var until = today.AddDays(1);
        var utcFrom = from.AddHours(3);
        var utcUntil = until.AddHours(3);

        var users = await db.Users.AsNoTracking()
            .Where(x => !x.IsDebugUser && x.Email != MonitorAccess.Email && x.CreatedAtUtc >= utcFrom && x.CreatedAtUtc < utcUntil)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync();
        var publications = await db.Publications.AsNoTracking()
            .Where(x => (x.User == null || !x.User.IsDebugUser) && x.CreatedAtUtc >= utcFrom && x.CreatedAtUtc < utcUntil)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync();
        var visitors = await db.SiteVisits.AsNoTracking()
            .Where(x => x.VisitedOn >= from && x.VisitedOn < until)
            .GroupBy(x => x.VisitedOn)
            .Select(x => new { Day = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count);

        var userCounts = users.GroupBy(ToArgentinaDay).ToDictionary(x => x.Key, x => x.Count());
        var publicationCounts = publications.GroupBy(ToArgentinaDay).ToDictionary(x => x.Key, x => x.Count());
        Metrics = Enumerable.Range(0, 30)
            .Select(offset => today.AddDays(-offset))
            .Select(day => new DailyMetric(day, userCounts.GetValueOrDefault(day), publicationCounts.GetValueOrDefault(day), visitors.GetValueOrDefault(day)))
            .ToList();
        return Page();
    }

    private static DateTime ToArgentinaDay(DateTime utc) => utc.AddHours(-3).Date;

    public sealed record DailyMetric(DateTime Day, int RegisteredUsers, int NewPublications, int Visitors);
}
