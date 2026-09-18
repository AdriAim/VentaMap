using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Services;

namespace VentaMap.Pages.Admin;

[Authorize]
public class BillingModel(CurrentUserAccessor currentUserAccessor, VentaMapDbContext db) : PageModel
{
    public List<UserRow> Users { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await IsAdminAsync()) return Forbid();
        Users = await db.Users.AsNoTracking().OrderBy(x => x.Email)
            .Select(x => new UserRow(x.Id, x.Name, x.Email, x.IsCompany, x.IsBillingExempt))
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleExemptAsync(int userId)
    {
        if (!await IsAdminAsync()) return Forbid();
        var user = await db.Users.FindAsync(userId);
        if (user is not null)
        {
            user.IsBillingExempt = user.IsBillingExempt == 1 ? 0 : 1;
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    private async Task<bool> IsAdminAsync()
    {
        return currentUserAccessor.UserId is int userId
            && await db.Users.AnyAsync(x => x.Id == userId && x.IsAdmin);
    }
    public sealed record UserRow(int Id, string Name, string Email, bool IsCompany, int IsBillingExempt);
}
