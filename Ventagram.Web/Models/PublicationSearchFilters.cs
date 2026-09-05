namespace Ventagram.Models;

public class PublicationSearchFilters
{
    public string? Operation { get; set; }
    public int? CategoryId { get; set; }
    public decimal? PriceFrom { get; set; }
    public decimal? PriceTo { get; set; }
    public int? RadiusKm { get; set; }
    public double? North { get; set; }
    public double? South { get; set; }
    public double? East { get; set; }
    public double? West { get; set; }
    public List<PublicationFieldSearchFilter> FieldFilters { get; set; } = [];
}

public class PublicationFieldSearchFilter
{
    public int FieldId { get; set; }
    public string? Value { get; set; }
}
