namespace Ventagram.ChatService.Models;

public class Publication
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "ARS";
    public string Locality { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public bool IsActive { get; set; }
    public List<PublicationMedia> MediaItems { get; set; } = [];
}
