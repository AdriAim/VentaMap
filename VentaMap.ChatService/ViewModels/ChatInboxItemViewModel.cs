namespace VentaMap.ChatService.ViewModels;

public class ChatInboxItemViewModel
{
    public int ConversationId { get; set; }
    public int PublicationId { get; set; }
    public string PublicationTitle { get; set; } = string.Empty;
    public string PublicationPrice { get; set; } = string.Empty;
    public string PublicationLocality { get; set; } = string.Empty;
    public string? PublicationImageUrl { get; set; }
    public string OtherParticipantName { get; set; } = string.Empty;
    public string LastMessagePreview { get; set; } = string.Empty;
    public DateTime? LastMessageAtUtc { get; set; }
    public int UnreadCount { get; set; }
    public bool LastMessageSentByCurrentUser { get; set; }
}
