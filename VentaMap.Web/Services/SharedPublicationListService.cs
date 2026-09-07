using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.ViewModels;

namespace VentaMap.Services;

public class SharedPublicationListService(VentaMapDbContext db)
{
    public async Task<List<SharedPublicationListSummaryViewModel>> GetOwnedSummariesAsync(int userId)
    {
        return await db.SharedPublicationLists
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Name)
            .Select(x => new SharedPublicationListSummaryViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                CompanySlug = x.User != null ? x.User.CompanySlug ?? string.Empty : string.Empty,
                ItemCount = x.Items.Count(item => item.Publication.IsActive),
                DefaultMode = x.DefaultMode,
                ShareUrl = "/Listas/" + (x.User != null ? x.User.CompanySlug ?? string.Empty : string.Empty) + "/" + x.Slug
            })
            .ToListAsync();
    }

    public async Task<SharedPublicationList?> GetOwnedListAsync(int userId, int listId)
    {
        return await db.SharedPublicationLists
            .Include(x => x.User)
            .Include(x => x.Items
                .Where(item => item.Publication.IsActive)
                .OrderByDescending(item => item.CreatedAtUtc))
                .ThenInclude(x => x.Publication)
                    .ThenInclude(x => x.Category)
            .Include(x => x.Items
                .Where(item => item.Publication.IsActive)
                .OrderByDescending(item => item.CreatedAtUtc))
                .ThenInclude(x => x.Publication)
                    .ThenInclude(x => x.MediaItems)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == listId);
    }

    public async Task<SharedPublicationList?> GetPublicListBySlugAsync(string slug)
    {
        var normalizedSlug = (slug ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            return null;
        }

        return await db.SharedPublicationLists
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Items
                .Where(item => item.Publication.IsActive)
                .OrderByDescending(item => item.CreatedAtUtc))
                .ThenInclude(x => x.Publication)
                    .ThenInclude(x => x.Category)
            .Include(x => x.Items
                .Where(item => item.Publication.IsActive)
                .OrderByDescending(item => item.CreatedAtUtc))
                .ThenInclude(x => x.Publication)
                    .ThenInclude(x => x.MediaItems)
            .FirstOrDefaultAsync(x => x.Slug == normalizedSlug);
    }

    public async Task<SharedPublicationList> CreateAsync(int userId, string name)
    {
        var normalizedName = NormalizeListName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Escribe un nombre para la lista.");
        }

        var ownedListNames = await db.SharedPublicationLists
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Name)
            .ToListAsync();
        if (ownedListNames.Any(existingName => string.Equals(NormalizeListName(existingName), normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Ya existe una lista con ese nombre.");
        }

        var list = new SharedPublicationList
        {
            UserId = userId,
            Name = normalizedName,
            Slug = await GenerateUniqueSlugAsync(normalizedName),
            DefaultMode = "Galeria",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.SharedPublicationLists.Add(list);
        await db.SaveChangesAsync();
        return list;
    }

    public async Task<bool> TogglePublicationAsync(int userId, int listId, int publicationId)
    {
        var list = await db.SharedPublicationLists
            .FirstOrDefaultAsync(x => x.Id == listId && x.UserId == userId);
        if (list is null)
        {
            return false;
        }

        var publication = await db.Publications
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == publicationId && x.UserId == userId && x.IsActive);
        if (publication is null)
        {
            return false;
        }

        var existingItem = await db.SharedPublicationListItems
            .FirstOrDefaultAsync(x => x.SharedPublicationListId == listId && x.PublicationId == publicationId);

        if (existingItem is null)
        {
            db.SharedPublicationListItems.Add(new SharedPublicationListItem
            {
                SharedPublicationListId = listId,
                PublicationId = publicationId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            db.SharedPublicationListItems.Remove(existingItem);
        }

        list.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddPublicationsAsync(int userId, int listId, IEnumerable<int>? publicationIds)
    {
        var list = await db.SharedPublicationLists
            .FirstOrDefaultAsync(x => x.Id == listId && x.UserId == userId);
        if (list is null)
        {
            return false;
        }

        var requestedIds = publicationIds?.Distinct().ToList() ?? [];
        if (requestedIds.Count == 0)
        {
            return true;
        }

        var eligibleIds = await db.Publications
            .Where(x => requestedIds.Contains(x.Id) && x.UserId == userId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();
        var existingIds = await db.SharedPublicationListItems
            .Where(x => x.SharedPublicationListId == listId && eligibleIds.Contains(x.PublicationId))
            .Select(x => x.PublicationId)
            .ToListAsync();

        var itemsToAdd = eligibleIds.Except(existingIds).ToList();
        if (itemsToAdd.Count == 0)
        {
            return true;
        }

        db.SharedPublicationListItems.AddRange(itemsToAdd.Select(publicationId => new SharedPublicationListItem
        {
            SharedPublicationListId = listId,
            PublicationId = publicationId,
            CreatedAtUtc = DateTime.UtcNow
        }));
        list.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemovePublicationAsync(int userId, int listId, int publicationId)
    {
        var item = await db.SharedPublicationListItems
            .Include(x => x.SharedPublicationList)
            .FirstOrDefaultAsync(x =>
                x.SharedPublicationListId == listId &&
                x.PublicationId == publicationId &&
                x.SharedPublicationList.UserId == userId);
        if (item is null)
        {
            return false;
        }

        item.SharedPublicationList.UpdatedAtUtc = DateTime.UtcNow;
        db.SharedPublicationListItems.Remove(item);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ClearAsync(int userId, int listId)
    {
        var list = await db.SharedPublicationLists
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == listId && x.UserId == userId);
        if (list is null)
        {
            return false;
        }

        if (list.Items.Count == 0)
        {
            return true;
        }

        list.UpdatedAtUtc = DateTime.UtcNow;
        db.SharedPublicationListItems.RemoveRange(list.Items);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateDefaultModeAsync(int userId, int listId, string? mode)
    {
        var list = await db.SharedPublicationLists
            .FirstOrDefaultAsync(x => x.Id == listId && x.UserId == userId);
        if (list is null)
        {
            return false;
        }

        list.DefaultMode = NormalizeMode(mode);
        list.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int userId, int listId)
    {
        var list = await db.SharedPublicationLists
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == listId && x.UserId == userId);
        if (list is null)
        {
            return false;
        }

        db.SharedPublicationLists.Remove(list);
        await db.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var baseSlug = Slugify(name);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "lista";
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var candidate = $"{baseSlug}-{suffix}";
            if (!await db.SharedPublicationLists.AnyAsync(x => x.Slug == candidate))
            {
                return candidate;
            }
        }

        return $"{baseSlug}-{Guid.NewGuid():N}";
    }

    private static string NormalizeListName(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : input.Trim()[..Math.Min(input.Trim().Length, 120)];
    }

    public static string NormalizeMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "lista" => "Lista",
        "mapa" => "Mapa",
        _ => "Galeria"
    };

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousDash = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousDash = false;
                continue;
            }

            if (previousDash || builder.Length == 0)
            {
                continue;
            }

            builder.Append('-');
            previousDash = true;
        }

        return builder.ToString().Trim('-')[..Math.Min(builder.ToString().Trim('-').Length, 120)];
    }
}
