using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class PublicationGroupTypeService(VentaMapDbContext db)
{
    public Task<List<PublicationGroupType>> GetActiveAsync()
    {
        return db.PublicationGroupTypes
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public Task<bool> ExistsAsync(PublicationGroup group)
    {
        var id = (byte)group;
        return db.PublicationGroupTypes.AnyAsync(x => x.IsActive && x.Id == id);
    }
}
