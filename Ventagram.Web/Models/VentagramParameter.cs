using System.ComponentModel.DataAnnotations;

namespace Ventagram.Models;

public class VentagramParameter
{
    public int Id { get; set; }

    [StringLength(120)]
    public string Key { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Value { get; set; } = string.Empty;

    [StringLength(30)]
    public string DataType { get; set; } = "String";

    [StringLength(300)]
    public string? Description { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public ApplicationUser? UpdatedByUser { get; set; }
}
