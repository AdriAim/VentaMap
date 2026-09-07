using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class FavoriteList
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<FavoriteListItem> Items { get; set; } = [];
}
