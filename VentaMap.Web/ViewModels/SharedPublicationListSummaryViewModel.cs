namespace VentaMap.ViewModels;

public class SharedPublicationListSummaryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CompanySlug { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string DefaultMode { get; set; } = "Galeria";
    public string ShareUrl { get; set; } = string.Empty;
}
