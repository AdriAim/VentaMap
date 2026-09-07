namespace VentaMap.Models;

public class PublicationCategory
{
    public int Id { get; set; }
    public PublicationGroup Group { get; set; } = PublicationGroup.Inmuebles;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<PublicationCategoryField> Fields { get; set; } = [];
}
