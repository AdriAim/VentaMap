namespace VentaMap.ChatService.ViewModels;

public class ChatMessageViewModel
{
    public int Id { get; set; }
    public bool IsMine { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
