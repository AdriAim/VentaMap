using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;

namespace VentaMap.Pages;

public class CompanyCatalogLegacyModel(VentaMapDbContext db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string companySlug)
    {
        var slug = (companySlug ?? string.Empty).Trim().ToLowerInvariant();
        var exists = !string.IsNullOrWhiteSpace(slug)
            && await db.Users.AsNoTracking().AnyAsync(x => x.IsCompany && x.CompanySlug == slug);
        if (!exists) return NotFound();

        return RedirectPermanent($"/catalogo/{Uri.EscapeDataString(slug)}{Request.QueryString}");
    }
}
