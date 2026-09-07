using VentaMap.Models;

namespace VentaMap.ViewModels;

public class HomeContentViewModel
{
    public string MapStyleUrl { get; set; } = string.Empty;
    public string Group { get; set; } = "Inmuebles";
    public List<PublicationGroupType> GroupOptions { get; set; } = [];
    public List<PublicationCategory> CategoryOptions { get; set; } = [];
    public int? SelectedCategoryId { get; set; }
    public string Mode { get; set; } = "Galeria";
    public string? Query { get; set; }
    public string? Operation { get; set; }
    public decimal? PriceFrom { get; set; }
    public decimal? PriceTo { get; set; }
    public int? RadiusKm { get; set; }
    public decimal PriceSliderMax { get; set; } = 1000000m;
    public List<PublicationCategoryField> RequiredFilterFields { get; set; } = [];
    public List<PublicationFieldSearchFilter> SelectedFieldFilters { get; set; } = [];
    public string MapTilesUrlTemplate { get; set; } = string.Empty;
    public string MapAttributionHtml { get; set; } = string.Empty;
    public string MapGeocodingSearchUrlTemplate { get; set; } = string.Empty;
    public string MapReverseGeocodingUrlTemplate { get; set; } = string.Empty;
    public string MarkersJson { get; set; } = "[]";
    public List<Publication> Publications { get; set; } = [];
    public List<Publication> ExpandedRadiusPublications { get; set; } = [];
    public string? FlashMessage { get; set; }
    public string GalleryApiEndpoint { get; set; } = string.Empty;
    public string MapMarkersApiEndpoint { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalResults { get; set; }
    public int TotalPages { get; set; }
    public int ExpandedRadiusTotalResults { get; set; }
    public int? ExpandedRadiusKm { get; set; }
    public string? UserLocalityLabel { get; set; }
    public double? UserLocalityLatitude { get; set; }
    public double? UserLocalityLongitude { get; set; }
    public bool RequireLocalitySelection { get; set; }
    public bool CanManageFavorites { get; set; }
    public bool IsPaidSiteEnabled { get; set; }
    public bool HasExpandedRadiusFallback => ExpandedRadiusKm.HasValue && ExpandedRadiusPublications.Count > 0;
    public bool HasMapResults => Publications.Any(x => x.Latitude.HasValue && x.Longitude.HasValue && !x.HideFromMap);
    public HashSet<int> FavoritePublicationIds { get; set; } = [];
    public List<FavoriteListSummaryViewModel> FavoriteLists { get; set; } = [];
}
