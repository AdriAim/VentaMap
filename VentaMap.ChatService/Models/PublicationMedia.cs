namespace VentaMap.ChatService.Models;

public class PublicationMedia
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication? Publication { get; set; }
    public int SortOrder { get; set; }
    public string Url { get; set; } = string.Empty;
}
