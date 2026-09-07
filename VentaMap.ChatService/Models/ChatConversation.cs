namespace VentaMap.ChatService.Models;

public class ChatConversation
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public int BuyerUserId { get; set; }
    public int SellerUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAtUtc { get; set; }
    public string? LastMessagePreview { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
}
