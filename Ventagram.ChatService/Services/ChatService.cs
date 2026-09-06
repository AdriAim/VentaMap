using Microsoft.EntityFrameworkCore;
using Ventagram.ChatService.Data;
using Ventagram.ChatService.Models;
using Ventagram.ChatService.ViewModels;

namespace Ventagram.ChatService.Services;

public class ChatAppService(ChatDbContext db, VentagramLookupDbContext lookupDb, IConfiguration configuration)
{
    public const int MaxMessageLength = 2000;

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await db.ChatMessages
            .CountAsync(x =>
                x.ReadAtUtc == null
                && x.SenderUserId != userId
                && (x.Conversation!.BuyerUserId == userId || x.Conversation.SellerUserId == userId));
    }

    public async Task<ChatConversation> GetOrCreateConversationAsync(int publicationId, int buyerUserId)
    {
        var publication = await lookupDb.Publications
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == publicationId);

        if (publication is null || !publication.IsActive)
        {
            throw new InvalidOperationException("La publicacion no esta disponible.");
        }

        if (publication.UserId is not int sellerUserId)
        {
            throw new InvalidOperationException("Esta publicacion no permite chat interno.");
        }

        if (publication.User?.AllowsSiteChat != true)
        {
            throw new InvalidOperationException("Esta publicacion no permite chat interno.");
        }

        if (sellerUserId == buyerUserId)
        {
            throw new InvalidOperationException("No puedes iniciar un chat con tu propio anuncio.");
        }

        var existing = await db.ChatConversations
            .FirstOrDefaultAsync(x =>
                x.PublicationId == publicationId
                && x.BuyerUserId == buyerUserId
                && x.SellerUserId == sellerUserId);

        if (existing is not null)
        {
            return existing;
        }

        var conversation = new ChatConversation
        {
            PublicationId = publicationId,
            BuyerUserId = buyerUserId,
            SellerUserId = sellerUserId
        };

        db.ChatConversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation;
    }

    public async Task<ChatPageContentViewModel> GetPageAsync(int userId, int? conversationId)
    {
        var inbox = await LoadInboxAsync(userId);
        var currentUserMessageColor = await lookupDb.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.MessageBubbleColor)
            .FirstOrDefaultAsync();
        ChatConversationViewModel? selectedConversation = null;

        if (conversationId is int currentConversationId)
        {
            selectedConversation = await LoadConversationAsync(currentConversationId, userId);
        }

        return new ChatPageContentViewModel
        {
            CurrentUserId = userId,
            CurrentUserMessageColor = string.Equals(currentUserMessageColor, "blue", StringComparison.OrdinalIgnoreCase) ? "blue" : "rose",
            Inbox = inbox,
            SelectedConversation = selectedConversation
        };
    }

    public async Task<bool> CanAccessConversationAsync(int conversationId, int userId)
    {
        return await db.ChatConversations.AnyAsync(x =>
            x.Id == conversationId
            && (x.BuyerUserId == userId || x.SellerUserId == userId));
    }

    public async Task<int> GetOtherParticipantUserIdAsync(int conversationId, int userId)
    {
        var conversation = await db.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId);

        if (conversation is null)
        {
            throw new InvalidOperationException("La conversacion no existe.");
        }

        if (conversation.BuyerUserId == userId)
        {
            return conversation.SellerUserId;
        }

        if (conversation.SellerUserId == userId)
        {
            return conversation.BuyerUserId;
        }

        throw new InvalidOperationException("No tienes acceso a esta conversacion.");
    }

    public async Task<ChatMessageViewModel> SendMessageAsync(int conversationId, int senderUserId, string body)
    {
        var normalizedBody = NormalizeBody(body);
        var conversation = await db.ChatConversations.FirstOrDefaultAsync(x => x.Id == conversationId);

        if (conversation is null)
        {
            throw new InvalidOperationException("La conversacion no existe.");
        }

        if (conversation.BuyerUserId != senderUserId && conversation.SellerUserId != senderUserId)
        {
            throw new InvalidOperationException("No tienes acceso a esta conversacion.");
        }

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            Body = normalizedBody
        };

        db.ChatMessages.Add(message);
        conversation.LastMessageAtUtc = message.CreatedAtUtc;
        conversation.LastMessagePreview = BuildPreview(normalizedBody);
        await db.SaveChangesAsync();

        var senderName = await lookupDb.Users
            .Where(x => x.Id == senderUserId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync();

        return new ChatMessageViewModel
        {
            Id = message.Id,
            IsMine = true,
            SenderName = senderName ?? "Usuario",
            Body = message.Body,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadAtUtc = message.ReadAtUtc
        };
    }

    public async Task<ChatReadResult> MarkConversationAsReadAsync(int conversationId, int userId)
    {
        var conversation = await db.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId);

        if (conversation is null)
        {
            throw new InvalidOperationException("La conversacion no existe.");
        }

        if (conversation.BuyerUserId != userId && conversation.SellerUserId != userId)
        {
            throw new InvalidOperationException("No tienes acceso a esta conversacion.");
        }

        var now = DateTime.UtcNow;
        var unreadMessages = await db.ChatMessages
            .Where(x =>
                x.ConversationId == conversationId
                && x.SenderUserId != userId
                && x.ReadAtUtc == null)
            .ToListAsync();

        if (unreadMessages.Count == 0)
        {
            return new ChatReadResult
            {
                ConversationId = conversationId,
                ReaderUserId = userId,
                OtherUserId = conversation.BuyerUserId == userId ? conversation.SellerUserId : conversation.BuyerUserId,
                ReadAtUtc = now
            };
        }

        foreach (var message in unreadMessages)
        {
            message.ReadAtUtc = now;
        }

        await db.SaveChangesAsync();
        return new ChatReadResult
        {
            ConversationId = conversationId,
            ReaderUserId = userId,
            OtherUserId = conversation.BuyerUserId == userId ? conversation.SellerUserId : conversation.BuyerUserId,
            ReadAtUtc = now,
            MessageIds = unreadMessages.Select(x => x.Id).ToList()
        };
    }

    public async Task<ChatInboxItemViewModel?> GetInboxItemAsync(int conversationId, int viewerUserId)
    {
        var inbox = await LoadInboxAsync(viewerUserId, conversationId);
        return inbox.FirstOrDefault();
    }

    private async Task<List<ChatInboxItemViewModel>> LoadInboxAsync(int userId, int? onlyConversationId = null)
    {
        var conversations = db.ChatConversations
            .AsNoTracking()
            .Include(x => x.Messages)
            .Where(x => x.BuyerUserId == userId || x.SellerUserId == userId);

        if (onlyConversationId.HasValue)
        {
            conversations = conversations.Where(x => x.Id == onlyConversationId.Value);
        }

        var conversationRows = await conversations
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.PublicationId,
                x.BuyerUserId,
                x.SellerUserId,
                x.LastMessagePreview,
                x.LastMessageAtUtc,
                x.CreatedAtUtc,
                UnreadCount = x.Messages.Count(m => m.SenderUserId != userId && m.ReadAtUtc == null),
                LastMessageSentByCurrentUser = x.Messages
                    .OrderByDescending(m => m.CreatedAtUtc)
                    .Select(m => m.SenderUserId == userId)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var publicationIds = conversationRows.Select(x => x.PublicationId).Distinct().ToList();
        var userIds = conversationRows
            .SelectMany(x => new[] { x.BuyerUserId, x.SellerUserId })
            .Distinct()
            .ToList();

        var publications = await lookupDb.Publications
            .AsNoTracking()
            .Include(x => x.MediaItems)
            .Where(x => publicationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var users = await lookupDb.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        return conversationRows
            .Select(x =>
            {
                publications.TryGetValue(x.PublicationId, out var publication);
                var otherUserId = x.BuyerUserId == userId ? x.SellerUserId : x.BuyerUserId;
                users.TryGetValue(otherUserId, out var otherUser);

                return new ChatInboxItemViewModel
                {
                    ConversationId = x.Id,
                    PublicationId = x.PublicationId,
                    PublicationTitle = GetPublicationDisplayText(publication),
                    PublicationPrice = publication is null ? string.Empty : $"{publication.Currency} {publication.Price:0}",
                    PublicationLocality = publication?.Locality ?? string.Empty,
                    PublicationImageUrl = publication?.MediaItems
                        .OrderBy(m => m.SortOrder)
                        .Select(m => m.Url)
                        .FirstOrDefault(),
                    OtherParticipantName = otherUser?.Name ?? "Usuario",
                    LastMessagePreview = x.LastMessagePreview ?? "Conversacion iniciada",
                    LastMessageAtUtc = x.LastMessageAtUtc ?? x.CreatedAtUtc,
                    UnreadCount = x.UnreadCount,
                    LastMessageSentByCurrentUser = x.LastMessageSentByCurrentUser
                };
            })
            .ToList();
    }

    private async Task<ChatConversationViewModel?> LoadConversationAsync(int conversationId, int userId)
    {
        var conversation = await db.ChatConversations
            .AsNoTracking()
            .Include(x => x.Messages.OrderBy(m => m.CreatedAtUtc))
            .FirstOrDefaultAsync(x =>
                x.Id == conversationId
                && (x.BuyerUserId == userId || x.SellerUserId == userId));

        if (conversation is null)
        {
            return null;
        }

        var isBuyer = conversation.BuyerUserId == userId;
        var otherUserId = isBuyer ? conversation.SellerUserId : conversation.BuyerUserId;
        var publication = await lookupDb.Publications
            .AsNoTracking()
            .Include(x => x.MediaItems)
            .FirstOrDefaultAsync(x => x.Id == conversation.PublicationId);
        var otherUser = await lookupDb.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == otherUserId);
        var senderIds = conversation.Messages.Select(x => x.SenderUserId).Distinct().ToList();
        var senderNames = await lookupDb.Users
            .AsNoTracking()
            .Where(x => senderIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var contactPhone = otherUser?.Phone ?? string.Empty;
        var publicationBaseUrl = (configuration["Chat:PublicationBaseUrl"] ?? string.Empty).TrimEnd('/');

        return new ChatConversationViewModel
        {
            ConversationId = conversation.Id,
            PublicationId = publication?.Id ?? conversation.PublicationId,
            PublicationTitle = GetPublicationDisplayText(publication),
            PublicationPrice = publication is null ? string.Empty : $"{publication.Currency} {publication.Price:0}",
            PublicationLocality = publication?.Locality ?? string.Empty,
            PublicationImageUrl = publication?.MediaItems
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Url)
                .FirstOrDefault(),
            PublicationDetailsUrl = string.IsNullOrWhiteSpace(publicationBaseUrl)
                ? $"/Publications/Details/{conversation.PublicationId}"
                : $"{publicationBaseUrl}/Publications/Details/{conversation.PublicationId}",
            OtherParticipantName = otherUser?.Name ?? "Usuario",
            OtherParticipantEmail = otherUser?.Email ?? string.Empty,
            OtherParticipantPhone = contactPhone,
            CanEmail = otherUser?.RespondsEmails == true && !string.IsNullOrWhiteSpace(otherUser.Email),
            CanCall = otherUser?.AcceptsCalls == true && !string.IsNullOrWhiteSpace(contactPhone),
            CanWhatsApp = otherUser?.RespondsWhatsApp == true && !string.IsNullOrWhiteSpace(contactPhone),
            Messages = conversation.Messages
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => new ChatMessageViewModel
                {
                    Id = x.Id,
                    IsMine = x.SenderUserId == userId,
                    SenderName = senderNames.TryGetValue(x.SenderUserId, out var senderName) ? senderName : "Usuario",
                    Body = x.Body,
                    CreatedAtUtc = x.CreatedAtUtc,
                    ReadAtUtc = x.ReadAtUtc
                })
                .ToList()
        };
    }

    private static string GetPublicationDisplayText(Publication? publication)
    {
        return string.IsNullOrWhiteSpace(publication?.ShortDescription)
            ? publication?.Title ?? "Publicacion"
            : publication.ShortDescription.Trim();
    }

    private static string NormalizeBody(string? body)
    {
        var normalized = (body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Escribe un mensaje.");
        }

        if (normalized.Length > MaxMessageLength)
        {
            throw new InvalidOperationException($"El mensaje no puede superar los {MaxMessageLength} caracteres.");
        }

        return normalized;
    }

    private static string BuildPreview(string body)
    {
        const int maxPreviewLength = 220;
        if (body.Length <= maxPreviewLength)
        {
            return body;
        }

        return $"{body[..(maxPreviewLength - 3)]}...";
    }
}

public class ChatReadResult
{
    public int ConversationId { get; set; }
    public int ReaderUserId { get; set; }
    public int OtherUserId { get; set; }
    public DateTime ReadAtUtc { get; set; }
    public List<int> MessageIds { get; set; } = [];
}
