using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class PublicationCategoryService(VentaMapDbContext db)
{
    public Task<List<PublicationCategory>> GetActiveByGroupAsync(PublicationGroup group)
    {
        return db.PublicationCategories
            .Where(x => x.IsActive && x.Group == group)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public Task<PublicationCategory?> GetActiveByIdAsync(int categoryId)
    {
        return db.PublicationCategories
            .FirstOrDefaultAsync(x => x.IsActive && x.Id == categoryId);
    }

    public Task<bool> ExistsAsync(PublicationGroup group, int categoryId)
    {
        if (categoryId <= 0)
        {
            return Task.FromResult(false);
        }

        return db.PublicationCategories
            .AnyAsync(x => x.IsActive
                && x.Group == group
                && x.Id == categoryId);
    }
}
