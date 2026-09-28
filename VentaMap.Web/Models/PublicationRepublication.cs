namespace VentaMap.Models;

public class PublicationRepublication
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;
    public DateTime RepublishedAtUtc { get; set; }
}
