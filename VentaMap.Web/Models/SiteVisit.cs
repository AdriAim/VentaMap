using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class SiteVisit
{
    public int Id { get; set; }

    [StringLength(64)]
    public string VisitorHash { get; set; } = string.Empty;

    // Calendar day in Argentina (UTC-3), stored without a time component.
    public DateTime VisitedOn { get; set; }

    // First acquisition channel recorded for this visitor on this day.
    [StringLength(32)]
    public string AcquisitionChannel { get; set; } = "other";

    // Campaign data is intentionally limited to marketing tags and the referrer host.
    // It never stores the full URL, IP address, or the visitor's browsing history.
    [StringLength(120)]
    public string? CampaignSource { get; set; }

    [StringLength(120)]
    public string? CampaignMedium { get; set; }

    [StringLength(160)]
    public string? CampaignName { get; set; }

    [StringLength(255)]
    public string? ReferrerHost { get; set; }
}
