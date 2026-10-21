using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

/// <summary>
/// Transcodes one directly uploaded video at a time so a small VPS is not
/// saturated. The original stays available in R2 until its replacement wins.
/// </summary>
public sealed class PublicationVideoProcessingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PublicationVideoProcessingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StaleClaimAfter = TimeSpan.FromMinutes(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessOneAsync(stoppingToken))
                {
                    // Keep draining work, but only one video is ever transcoded at once.
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falló el worker de procesamiento de videos.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VentaMapDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<CloudflareR2ImageStorageService>();
        var staleBefore = DateTime.UtcNow - StaleClaimAfter;

        var media = await db.PublicationMedia
            .Where(x => x.MediaType == PublicationMediaType.Video
                        && (x.VideoProcessingState == "Pending"
                            || (x.VideoProcessingState == "Processing" && x.VideoProcessingStartedAtUtc < staleBefore)))
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (media is null)
        {
            return false;
        }

        media.VideoProcessingState = "Processing";
        media.VideoProcessingStartedAtUtc = DateTime.UtcNow;
        media.VideoProcessingFailedAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        var originalUrl = media.Url;
        try
        {
            var optimizedUrl = await storage.ProcessPendingPublicationVideoAsync(originalUrl, cancellationToken);
            media.Url = optimizedUrl;
            media.VideoProcessingState = "Ready";
            media.VideoProcessingStartedAtUtc = null;
            await db.SaveChangesAsync(cancellationToken);
            await storage.DeletePublicObjectsAsync([originalUrl], cancellationToken);
            logger.LogInformation("Video de publicación {PublicationId} optimizado en segundo plano.", media.PublicationId);
        }
        catch (Exception ex)
        {
            media.VideoProcessingState = "Failed";
            media.VideoProcessingFailedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "No se pudo optimizar el video pendiente de la publicación {PublicationId}; se conserva el original.", media.PublicationId);
        }

        return true;
    }
}
