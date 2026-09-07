using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class SiteSuggestion
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(120)]
    public string? SenderName { get; set; }

    [StringLength(160)]
    public string? SenderEmail { get; set; }

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
