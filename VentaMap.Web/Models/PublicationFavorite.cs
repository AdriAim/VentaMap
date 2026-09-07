namespace VentaMap.Models;

public class PublicationFavorite
{
    public int Id { get; set; }

    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
