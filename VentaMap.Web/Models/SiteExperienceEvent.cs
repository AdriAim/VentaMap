using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class SiteExperienceEvent
{
    public long Id { get; set; }

    [StringLength(64)]
    public string VisitorHash { get; set; } = string.Empty;

    [StringLength(64)]
    public string SessionHash { get; set; } = string.Empty;

    [StringLength(64)]
    public string EventName { get; set; } = string.Empty;

    [StringLength(64)]
    public string Button { get; set; } = string.Empty;

    [StringLength(32)]
    public string AcquisitionChannel { get; set; } = "other";

    // Sólo se almacena el path sin query string para evitar capturar búsquedas.
    [StringLength(512)]
    public string Route { get; set; } = "/";

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
