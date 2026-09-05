namespace Ventagram.Models;

public class SharedPublicationListItem
{
    public int Id { get; set; }

    public int SharedPublicationListId { get; set; }

    public SharedPublicationList SharedPublicationList { get; set; } = null!;

    public int PublicationId { get; set; }

    public Publication Publication { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
