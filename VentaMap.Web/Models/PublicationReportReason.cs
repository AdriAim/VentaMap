using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class PublicationReportReason
{
    public int Id { get; set; }

    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public List<PublicationReport> Reports { get; set; } = [];
}
