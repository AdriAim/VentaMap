using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class PublicationMedia
{
    public int Id { get; set; }

    public int PublicationId { get; set; }
    public Publication? Publication { get; set; }

    public int SortOrder { get; set; }

    public PublicationMediaType MediaType { get; set; } = PublicationMediaType.Image;

    [StringLength(1000)]
    public string Url { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
