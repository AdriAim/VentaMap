namespace VentaMap.Models;

public class FavoriteListItem
{
    public int Id { get; set; }

    public int FavoriteListId { get; set; }

    public FavoriteList FavoriteList { get; set; } = null!;

    public int PublicationId { get; set; }

    public Publication Publication { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
