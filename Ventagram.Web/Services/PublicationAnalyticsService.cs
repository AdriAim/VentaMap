using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class PublicationAnalyticsService(VentagramDbContext db)
{
    public async Task TrackUniqueOpenAsync(int publicationId, int? publicationOwnerUserId, int? viewerUserId, string? anonymousFingerprint)
    {
        if (viewerUserId.HasValue && publicationOwnerUserId == viewerUserId.Value)
        {
            return;
        }

        if (viewerUserId is int userId)
        {
            if (await db.PublicationViews.AnyAsync(x => x.PublicationId == publicationId && x.ViewerUserId == userId))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(anonymousFingerprint))
            {
                var anonymousView = await db.PublicationViews
                    .FirstOrDefaultAsync(x => x.PublicationId == publicationId && x.AnonymousFingerprint == anonymousFingerprint);

                if (anonymousView is not null)
                {
                    anonymousView.ViewerUserId = userId;
                    anonymousView.AnonymousFingerprint = null;
                    await SaveIgnoringUniqueCollisionsAsync();
                    return;
                }
            }

            db.PublicationViews.Add(new PublicationView
            {
                PublicationId = publicationId,
                ViewerUserId = userId,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (await SaveIgnoringUniqueCollisionsAsync())
            {
                await IncrementUniqueViewCountAsync(publicationId);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(anonymousFingerprint))
        {
            return;
        }

        var alreadyTracked = await db.PublicationViews
            .AnyAsync(x => x.PublicationId == publicationId && x.AnonymousFingerprint == anonymousFingerprint);

        if (alreadyTracked)
        {
            return;
        }

        db.PublicationViews.Add(new PublicationView
        {
            PublicationId = publicationId,
            AnonymousFingerprint = anonymousFingerprint,
            CreatedAtUtc = DateTime.UtcNow
        });

        if (await SaveIgnoringUniqueCollisionsAsync())
        {
            await IncrementUniqueViewCountAsync(publicationId);
        }
    }

    private async Task<bool> SaveIgnoringUniqueCollisionsAsync()
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries()
                .Where(x => x.Entity is PublicationView && (x.State == EntityState.Added || x.State == EntityState.Modified)))
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    private async Task IncrementUniqueViewCountAsync(int publicationId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE `Publications` SET `UniqueViewCount` = `UniqueViewCount` + 1 WHERE `Id` = {publicationId}");
    }
}
