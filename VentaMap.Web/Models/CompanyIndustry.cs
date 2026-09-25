using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class CompanyIndustry
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}