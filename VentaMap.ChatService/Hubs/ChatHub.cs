using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VentaMap.ChatService.Services;

namespace VentaMap.ChatService.Hubs;

[Authorize]
public class ChatHub(CurrentUserAccessor currentUserAccessor, ChatAppService chatService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (currentUserAccessor.UserId is int userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public async Task OpenConversation(int conversationId)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            throw new HubException("Sesion invalida.");
        }

        if (!await chatService.CanAccessConversationAsync(conversationId, userId))
        {
            throw new HubException("No tienes acceso a esta conversacion.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
    }

    public async Task SendMessage(int conversationId, string body)
    {
        if (currentUserAccessor.UserId is not int senderUserId)
        {
            throw new HubException("Sesion invalida.");
        }

        var message = await chatService.SendMessageAsync(conversationId, senderUserId, body);
        var senderInboxItem = await chatService.GetInboxItemAsync(conversationId, senderUserId);
        if (senderInboxItem is null)
        {
            throw new HubException("No se pudo actualizar la conversacion.");
        }

        var recipientUserId = await chatService.GetOtherParticipantUserIdAsync(conversationId, senderUserId);
        var recipientInboxItem = await chatService.GetInboxItemAsync(conversationId, recipientUserId);

        await Clients.Group(ConversationGroup(conversationId)).SendAsync("MessageReceived", new
        {
            conversationId,
            message = new
            {
                id = message.Id,
                body = message.Body,
                senderName = message.SenderName,
                senderUserId,
                createdAtUtc = message.CreatedAtUtc,
                readAtUtc = message.ReadAtUtc
            }
        });

        await Clients.Group(UserGroup(senderUserId)).SendAsync("ConversationUpdated", new
        {
            conversation = senderInboxItem
        });

        await Clients.Group(UserGroup(recipientUserId)).SendAsync("ConversationUpdated", new
        {
            conversation = recipientInboxItem
        });
    }

    public async Task MarkConversationRead(int conversationId)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            throw new HubException("Sesion invalida.");
        }

        var result = await chatService.MarkConversationAsReadAsync(conversationId, userId);
        var currentInboxItem = await chatService.GetInboxItemAsync(conversationId, userId);
        var otherInboxItem = await chatService.GetInboxItemAsync(conversationId, result.OtherUserId);

        await Clients.Group(UserGroup(userId)).SendAsync("ConversationUpdated", new
        {
            conversation = currentInboxItem
        });

        if (result.MessageIds.Count > 0)
        {
            await Clients.Group(UserGroup(result.OtherUserId)).SendAsync("MessagesRead", new
            {
                conversationId = result.ConversationId,
                messageIds = result.MessageIds,
                readAtUtc = result.ReadAtUtc
            });

            await Clients.Group(UserGroup(result.OtherUserId)).SendAsync("ConversationUpdated", new
            {
                conversation = otherInboxItem
            });
        }
    }

    private static string UserGroup(int userId) => $"chat-user:{userId}";

    private static string ConversationGroup(int conversationId) => $"chat-conversation:{conversationId}";
}
