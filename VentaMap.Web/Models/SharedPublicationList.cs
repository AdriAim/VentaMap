using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class SharedPublicationList
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(160)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(20)]
    public string DefaultMode { get; set; } = "Galeria";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<SharedPublicationListItem> Items { get; set; } = [];
}
