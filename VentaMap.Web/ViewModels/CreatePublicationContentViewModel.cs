using VentaMap.Models;

namespace VentaMap.ViewModels;

public class CreatePublicationContentViewModel
{
    public string MapStyleUrl { get; set; } = string.Empty;
    public PublicationCreateRequest Input { get; set; } = new();
    public List<PublicationGroupType> GroupOptions { get; set; } = [];
    public List<PublicationCategory> Categories { get; set; } = [];
    public bool IsAuthenticated { get; set; }
    public bool RequiresLogin { get; set; }
    public string? CurrentUserName { get; set; }
    public string? CurrentUserEmail { get; set; }
    public string? CurrentUserPhone { get; set; }
    public string? SuggestedLocalityLabel { get; set; }
    public double? SuggestedMapLatitude { get; set; }
    public double? SuggestedMapLongitude { get; set; }
    public string MapTilesUrlTemplate { get; set; } = string.Empty;
    public string MapAttributionHtml { get; set; } = string.Empty;
    public string MapGeocodingSearchUrlTemplate { get; set; } = string.Empty;
    public string MapReverseGeocodingUrlTemplate { get; set; } = string.Empty;
    public string FormEyebrow { get; set; } = "Anuncio nuevo";
    public string FormTitle { get; set; } = "Crear anuncio";
    public string FormDescription { get; set; } = "Completando los datos minimos, y subiendo al menos una foto ya puedes publicar tu anuncio.";
    public string SubmitButtonText { get; set; } = "Publicar";
    public string CancelUrl { get; set; } = "/";
    public string SubmitEndpoint { get; set; } = "/api/content/create";
    public bool ShowLocationSection { get; set; }
    public bool ShowTechnicalSection { get; set; }
    public bool PublishingBlocked { get; set; }
    public string? PublishingBlockedMessage { get; set; }
    public bool IsPaidSiteEnabled { get; set; }
    public List<CreatePublicationDynamicFieldValueSeed> InitialDynamicFieldValues { get; set; } = [];
    public List<string> OperationOptions { get; set; } = [];
}

public class CreatePublicationDynamicFieldValueSeed
{
    public string InternalName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
