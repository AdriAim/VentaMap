using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages.Admin;

[Authorize]
public class CompanyIndustriesModel(CurrentUserAccessor currentUserAccessor, VentaMapDbContext db) : PageModel
{
    [BindProperty]
    public string NewIndustryName { get; set; } = string.Empty;

    public List<CompanyIndustry> Industries { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await IsAdminAsync()) return Forbid();
        await LoadIndustriesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!await IsAdminAsync()) return Forbid();

        var name = NewIndustryName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(NewIndustryName), "Ingresa el nombre del rubro.");
        }
        else if (name.Length > 120)
        {
            ModelState.AddModelError(nameof(NewIndustryName), "El rubro no puede superar los 120 caracteres.");
        }
        else if (await db.CompanyIndustries.AnyAsync(x => x.Name == name))
        {
            ModelState.AddModelError(nameof(NewIndustryName), "Ese rubro ya existe.");
        }

        if (!ModelState.IsValid)
        {
            await LoadIndustriesAsync();
            return Page();
        }

        var sortOrder = await db.CompanyIndustries.Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
        db.CompanyIndustries.Add(new CompanyIndustry { Name = name, SortOrder = sortOrder + 1 });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        if (!await IsAdminAsync()) return Forbid();
        var industry = await db.CompanyIndustries.FindAsync(id);
        if (industry is not null)
        {
            industry.IsActive = !industry.IsActive;
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    private async Task LoadIndustriesAsync()
    {
        Industries = await db.CompanyIndustries.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
    }

    private async Task<bool> IsAdminAsync() =>
        currentUserAccessor.UserId is int userId
        && await db.Users.AnyAsync(x => x.Id == userId && x.IsAdmin);
}