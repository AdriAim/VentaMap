using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class PublicationCreateRequest
{
    public PublicationGroup Group { get; set; } = PublicationGroup.Inmuebles;
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "U$D";
    public string Locality { get; set; } = string.Empty;
    [StringLength(60, ErrorMessage = "La descripción corta debe tener como máximo 60 caracteres, incluidos los espacios.")]
    public string ShortDescription { get; set; } = string.Empty;
    public string? LongDescription { get; set; }
    public string ImagesCsv { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public bool Featured { get; set; }
    public string? VideoUrl { get; set; }
    public string? InternalNotes { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool NoLocation { get; set; }
    public string? PublisherMode { get; set; } = "Anonymous";
    public string? PropertyType { get; set; }
    public string? Operation { get; set; }
    public string? Zone { get; set; }
    public decimal? TotalAreaM2 { get; set; }
    public decimal? CoveredAreaM2 { get; set; }
    public string? RoomsOrBedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public string? Address { get; set; }
    public int? GarageSpaces { get; set; }
    public int? AgeYears { get; set; }
    public decimal? Expenses { get; set; }
    public string? Condition { get; set; }
    public bool MortgageEligible { get; set; }
    public bool ProfessionalUseAllowed { get; set; }
    public string? Services { get; set; }
    public string? Amenities { get; set; }
    public string? VehicleType { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public int? Kilometers { get; set; }
    public string? Fuel { get; set; }
    public string? Transmission { get; set; }
    public string? Version { get; set; }
    public string? Color { get; set; }
    public string? LicensePlate { get; set; }
    public string? Engine { get; set; }
    public string? Traction { get; set; }
    public int? Doors { get; set; }
    public int? OwnersCount { get; set; }
    public bool AcceptsTrade { get; set; }
    public bool FinancingAvailable { get; set; }
    public string? Equipment { get; set; }
    public string? GeneralCondition { get; set; }
    public string? Subcategory { get; set; }
    public string? ItemCondition { get; set; }
    public string? Sku { get; set; }
    public int? Stock { get; set; }
    public string? Measure { get; set; }
    public string? Weight { get; set; }
    public string? Dimensions { get; set; }
    public string? Warranty { get; set; }
    public string? Shipping { get; set; }
    public List<PublicationDynamicFieldInput> DynamicFields { get; set; } = [];
}
