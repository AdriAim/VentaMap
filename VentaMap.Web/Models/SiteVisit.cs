using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class SiteVisit
{
    public int Id { get; set; }

    [StringLength(64)]
    public string VisitorHash { get; set; } = string.Empty;

    // Calendar day in Argentina (UTC-3), stored without a time component.
    public DateTime VisitedOn { get; set; }
}
