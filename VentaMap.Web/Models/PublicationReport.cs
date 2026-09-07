namespace VentaMap.Models;

public class PublicationReport
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;
    public int ReporterUserId { get; set; }
    public ApplicationUser ReporterUser { get; set; } = null!;
    public int ReasonId { get; set; }
    public PublicationReportReason Reason { get; set; } = null!;
    public string? Comment { get; set; }
    public bool CountsTowardThreshold { get; set; } = true;

    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string ReviewStatus { get; set; } = "Pending";

    public DateTime? ReviewedAtUtc { get; set; }
    public int? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
