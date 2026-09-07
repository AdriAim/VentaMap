namespace VentaMap.ChatService.ViewModels;

public class ChatConversationViewModel
{
    public int ConversationId { get; set; }
    public int PublicationId { get; set; }
    public string PublicationTitle { get; set; } = string.Empty;
    public string PublicationPrice { get; set; } = string.Empty;
    public string PublicationLocality { get; set; } = string.Empty;
    public string? PublicationImageUrl { get; set; }
    public string? PublicationDetailsUrl { get; set; }
    public string OtherParticipantName { get; set; } = string.Empty;
    public string OtherParticipantEmail { get; set; } = string.Empty;
    public string OtherParticipantPhone { get; set; } = string.Empty;
    public bool CanEmail { get; set; }
    public bool CanCall { get; set; }
    public bool CanWhatsApp { get; set; }
    public List<ChatMessageViewModel> Messages { get; set; } = [];
}
