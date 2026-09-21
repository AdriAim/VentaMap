using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class SiteVisitBackgroundService(IServiceScopeFactory scopeFactory, SiteVisitQueue queue, ILogger<SiteVisitBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var visit in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<VentaMapDbContext>();
                db.SiteVisits.Add(new SiteVisit { VisitorHash = visit.VisitorHash, VisitedOn = visit.VisitedOn });
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (DbUpdateException)
            {
                // The unique index handles repeated requests from one browser.
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "No se pudo registrar una visita del sitio.");
            }
        }
    }
}
