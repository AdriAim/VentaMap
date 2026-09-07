using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class ArgentineLocality
{
    public int Id { get; set; }

    [StringLength(120)]
    public string Locality { get; set; } = string.Empty;

    [StringLength(120)]
    public string Province { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
