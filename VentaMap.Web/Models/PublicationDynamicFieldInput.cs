namespace VentaMap.Models;

public class PublicationDynamicFieldInput
{
    public int FieldId { get; set; }
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public bool? ValueBoolean { get; set; }
}
