using System.ComponentModel.DataAnnotations;

namespace VentaMap.ViewModels;

public class ReportPublicationRequest
{
    [Required]
    public int PublicationId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int ReasonId { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }
}
