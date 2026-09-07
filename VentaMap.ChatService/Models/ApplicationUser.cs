namespace VentaMap.ChatService.Models;

public class ApplicationUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool RespondsEmails { get; set; }
    public bool AcceptsCalls { get; set; }
    public bool RespondsWhatsApp { get; set; }
    public bool AllowsSiteChat { get; set; }
    public string MessageBubbleColor { get; set; } = "rose";
    public bool IsDebugUser { get; set; }
}
