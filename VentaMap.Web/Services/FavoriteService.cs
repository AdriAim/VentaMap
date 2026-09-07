using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.ViewModels;

namespace VentaMap.Services;

public class FavoriteService(VentaMapDbContext db)
{
    public async Task<List<FavoriteListSummaryViewModel>> GetListSummariesAsync(int userId)
    {
        return await db.FavoriteLists
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Name)
            .Select(x => new FavoriteListSummaryViewModel
            {
                Id = x.Id,
                Name = x.Name,
                ItemCount = x.Items.Count
            })
            .ToListAsync();
    }

    public async Task<HashSet<int>> GetFavoritePublicationIdsAsync(int userId, IEnumerable<int> publicationIds)
    {
        var ids = publicationIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var favorites = await db.FavoriteListItems
            .AsNoTracking()
            .Where(x => x.FavoriteList.UserId == userId && ids.Contains(x.PublicationId))
            .Select(x => x.PublicationId)
            .Distinct()
            .ToListAsync();

        return [.. favorites];
    }

    public async Task<(FavoriteList List, bool Added)> AddFavoriteAsync(int userId, int publicationId, int? listId, string? newListName, string? suggestedListName)
    {
        var publicationExists = await db.Publications.AnyAsync(x => x.Id == publicationId && x.IsActive);
        if (!publicationExists)
        {
            throw new InvalidOperationException("El anuncio ya no está disponible.");
        }

        FavoriteList? list = null;
        if (listId is int existingListId and > 0)
        {
            list = await db.FavoriteLists.FirstOrDefaultAsync(x => x.Id == existingListId && x.UserId == userId);
            if (list is null)
            {
                throw new InvalidOperationException("La lista seleccionada no existe.");
            }
        }
        else
        {
            var baseName = string.IsNullOrWhiteSpace(newListName) ? suggestedListName : newListName;
            var normalizedName = NormalizeListName(baseName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidOperationException("Elegí una lista o escribí un nombre para crear una nueva.");
            }

            list = await db.FavoriteLists.FirstOrDefaultAsync(x => x.UserId == userId && x.Name == normalizedName);
            if (list is null)
            {
                list = new FavoriteList
                {
                    UserId = userId,
                    Name = normalizedName,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.FavoriteLists.Add(list);
                await db.SaveChangesAsync();
            }
        }

        var existingItem = await db.FavoriteListItems.FirstOrDefaultAsync(x => x.FavoriteListId == list.Id && x.PublicationId == publicationId);
        if (existingItem is not null)
        {
            list.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return (list, false);
        }

        db.FavoriteListItems.Add(new FavoriteListItem
        {
            FavoriteListId = list.Id,
            PublicationId = publicationId,
            CreatedAtUtc = DateTime.UtcNow
        });
        list.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await EnsureUniqueFavoriteMarkerAsync(userId, publicationId);
        return (list, true);
    }

    public async Task<(FavoriteListSummaryViewModel Summary, List<Publication> Publications)?> GetListContentAsync(int userId, int listId, bool includeDebug = false)
    {
        var list = await db.FavoriteLists
            .AsNoTracking()
            .Where(x => x.Id == listId && x.UserId == userId)
            .Select(x => new FavoriteListSummaryViewModel
            {
                Id = x.Id,
                Name = x.Name,
                ItemCount = x.Items.Count
            })
            .FirstOrDefaultAsync();
        if (list is null)
        {
            return null;
        }

        var favoriteItems = await db.FavoriteListItems
            .AsNoTracking()
            .Where(x => x.FavoriteListId == listId
                && x.Publication.IsActive
                && (includeDebug || x.Publication.UserId == null || x.Publication.User == null || !x.Publication.User.IsDebugUser))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Include(x => x.Publication)
                .ThenInclude(x => x.Category)
            .Include(x => x.Publication)
                .ThenInclude(x => x.MediaItems)
            .ToListAsync();

        return (list, favoriteItems.Select(x => x.Publication).ToList());
    }

    public async Task RenameListAsync(int userId, int listId, string? name)
    {
        var normalizedName = NormalizeListName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Escribí un nombre para la lista.");
        }

        var list = await db.FavoriteLists.FirstOrDefaultAsync(x => x.Id == listId && x.UserId == userId);
        if (list is null)
        {
            throw new InvalidOperationException("La lista no existe.");
        }

        var duplicated = await db.FavoriteLists.AnyAsync(x =>
            x.UserId == userId &&
            x.Id != listId &&
            x.Name == normalizedName);
        if (duplicated)
        {
            throw new InvalidOperationException("Ya existe una lista con ese nombre.");
        }

        list.Name = normalizedName;
        list.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteListAsync(int userId, int listId)
    {
        var list = await db.FavoriteLists
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == listId && x.UserId == userId);
        if (list is null)
        {
            return false;
        }

        db.FavoriteLists.Remove(list);
        await db.SaveChangesAsync();
        return true;
    }

    private static string NormalizeListName(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : input.Trim()[..Math.Min(input.Trim().Length, 120)];
    }

    private async Task EnsureUniqueFavoriteMarkerAsync(int userId, int publicationId)
    {
        db.PublicationFavorites.Add(new PublicationFavorite
        {
            PublicationId = publicationId,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE `Publications` SET `UniqueFavoriteCount` = `UniqueFavoriteCount` + 1 WHERE `Id` = {publicationId}");
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries()
                .Where(x => x.Entity is PublicationFavorite && x.State == EntityState.Added))
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
