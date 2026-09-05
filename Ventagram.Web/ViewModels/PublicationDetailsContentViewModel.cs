using Ventagram.Models;
using Ventagram.Services;

namespace Ventagram.ViewModels;

public class PublicationDetailsContentViewModel
{
    public Publication? Publication { get; set; }
    public string MapStyleUrl { get; set; } = string.Empty;
    public string MapTilesUrlTemplate { get; set; } = string.Empty;
    public string MapAttributionHtml { get; set; } = string.Empty;
    public string MapReverseGeocodingUrlTemplate { get; set; } = string.Empty;
    public string? MapLocationLabel { get; set; }
    public bool IsAuthenticated { get; set; }
    public int? CurrentUserId { get; set; }
    public UserReviewSummary? AdvertiserReviews { get; set; }
}
