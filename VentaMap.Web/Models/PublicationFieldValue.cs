using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class PublicationFieldValue
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;
    public int CategoryFieldId { get; set; }
    public PublicationCategoryField CategoryField { get; set; } = null!;

    [StringLength(500)]
    public string? ValueText { get; set; }

    public decimal? ValueNumber { get; set; }
    public bool? ValueBoolean { get; set; }
}
