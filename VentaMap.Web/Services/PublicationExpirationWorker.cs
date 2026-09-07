using Microsoft.EntityFrameworkCore;
using VentaMap.Data;

namespace VentaMap.Services;

public class PublicationExpirationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PublicationExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredPublicationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo el worker de vencimiento de publicaciones.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredPublicationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VentaMapDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var publicationService = scope.ServiceProvider.GetRequiredService<PublicationService>();

        var processed = await publicationService.ExpirePublicationsAndSendNotificationsAsync(emailSender, logger, cancellationToken);
        if (processed > 0)
        {
            logger.LogInformation("Se vencieron y notificaron {Count} publicaciones.", processed);
        }
    }
}
