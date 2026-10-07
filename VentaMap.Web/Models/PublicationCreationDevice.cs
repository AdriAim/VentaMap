using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class PublicationCreationDevice
{
    public int Id { get; set; }

    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;

    [StringLength(24)]
    public string DeviceType { get; set; } = "Unknown";

    [StringLength(40)]
    public string? OperatingSystem { get; set; }

    [StringLength(40)]
    public string? Browser { get; set; }

    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}
