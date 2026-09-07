using Microsoft.EntityFrameworkCore;
using VentaMap.ChatService.Data;
using VentaMap.ChatService.Models;

namespace VentaMap.ChatService.Services;

public class ChatEmailReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ChatEmailReminderWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReminderDelay = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo el worker de recordatorios de chat.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var lookupDb = scope.ServiceProvider.GetRequiredService<VentaMapLookupDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var threshold = DateTime.UtcNow - ReminderDelay;
        var candidates = await db.ChatMessages
            .Include(x => x.Conversation!)
            .Where(x => x.EmailReminderSentAtUtc == null && x.CreatedAtUtc <= threshold)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var publicationIds = candidates
            .Select(x => x.Conversation?.PublicationId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var userIds = candidates
            .SelectMany(x => x.Conversation is null
                ? []
                : new[] { x.Conversation.BuyerUserId, x.Conversation.SellerUserId, x.SenderUserId })
            .Distinct()
            .ToList();

        var publications = await lookupDb.Publications
            .AsNoTracking()
            .Where(x => publicationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var users = await lookupDb.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var message in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recipient = ResolveRecipient(message, users);
            if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
            {
                message.EmailReminderSentAtUtc = DateTime.UtcNow;
                continue;
            }

            var recipientReplied = await db.ChatMessages.AnyAsync(
                x => x.ConversationId == message.ConversationId
                    && x.SenderUserId == recipient.Id
                    && x.CreatedAtUtc > message.CreatedAtUtc,
                cancellationToken);

            if (recipientReplied)
            {
                message.EmailReminderSentAtUtc = DateTime.UtcNow;
                continue;
            }

            publications.TryGetValue(message.Conversation?.PublicationId ?? 0, out var publication);
            var senderName = users.TryGetValue(message.SenderUserId, out var sender) ? sender.Name : "Usuario";
            var publicationLabel = string.IsNullOrWhiteSpace(publication?.ShortDescription)
                ? publication?.Title ?? "un anuncio"
                : publication.ShortDescription.Trim();
            var publicBaseUrl = (configuration["Chat:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
            var detailsUrl = string.IsNullOrWhiteSpace(publicBaseUrl)
                ? $"/Mensajes/{message.ConversationId}"
                : $"{publicBaseUrl}/Mensajes/{message.ConversationId}";

            var subject = $"Nuevo mensaje pendiente en VentaMap sobre {publicationLabel}";
            var htmlBody = $"""
                <p>Hola {recipient.Name},</p>
                <p>Hace una hora recibiste un mensaje en VentaMap y todavia no lo respondiste.</p>
                <p><strong>Anuncio:</strong> {System.Net.WebUtility.HtmlEncode(publicationLabel)}</p>
                <p><strong>De:</strong> {System.Net.WebUtility.HtmlEncode(senderName)}</p>
                <blockquote style="margin:16px 0;padding:12px 16px;border-left:4px solid #e3374e;background:#f9f8f7;">
                    {System.Net.WebUtility.HtmlEncode(message.Body).Replace("\n", "<br />")}
                </blockquote>
                <p><a href="{detailsUrl}">Abrir conversacion</a></p>
                """;
            var textBody = $"""
                Hola {recipient.Name},

                Hace una hora recibiste un mensaje en VentaMap y todavia no lo respondiste.

                Anuncio: {publicationLabel}
                De: {senderName}

                Mensaje:
                {message.Body}

                Abrir conversacion: {detailsUrl}
                """;

            var sent = await emailSender.SendAsync(recipient.Email, subject, htmlBody, textBody);
            if (sent)
            {
                message.EmailReminderSentAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static ApplicationUser? ResolveRecipient(ChatMessage message, IReadOnlyDictionary<int, ApplicationUser> users)
    {
        var conversation = message.Conversation;
        if (conversation is null)
        {
            return null;
        }

        var recipientUserId = message.SenderUserId == conversation.BuyerUserId
            ? conversation.SellerUserId
            : conversation.BuyerUserId;

        return users.TryGetValue(recipientUserId, out var recipient) ? recipient : null;
    }
}
