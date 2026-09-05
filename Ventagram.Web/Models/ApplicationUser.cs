using System.ComponentModel.DataAnnotations;

namespace Ventagram.Models;

public class ApplicationUser
{
    public int Id { get; set; }

    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public bool IsCompany { get; set; }

    [StringLength(160)]
    public string? CompanyName { get; set; }

    [StringLength(180)]
    public string? CompanySlug { get; set; }

    [StringLength(260)]
    public string? CompanyLogoUrl { get; set; }

    [StringLength(260)]
    public string? CompanyHeroBackgroundUrl { get; set; }

    [StringLength(180)]
    public string? CompanyTagline { get; set; }

    [StringLength(120)]
    public string? CompanyIndustry { get; set; }

    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [StringLength(32)]
    public string Phone { get; set; } = string.Empty;

    public bool RespondsEmails { get; set; }

    public bool AcceptsCalls { get; set; }

    public bool RespondsWhatsApp { get; set; }

    public bool AllowsSiteChat { get; set; } = true;

    [StringLength(40)]
    public string ContactPreference { get; set; } = "CallsWhatsApp";

    [StringLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(40)]
    public string AuthProvider { get; set; } = "Local";

    public int? ArgentineLocalityId { get; set; }

    public ArgentineLocality? ArgentineLocality { get; set; }

    [StringLength(120)]
    public string? HeaderPublicationGroupsCsv { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsAdmin { get; set; }

    public bool IsDebugUser { get; set; }

    public bool CanPublish { get; set; } = true;

    public bool CanReport { get; set; } = true;

    public List<FavoriteList> FavoriteLists { get; set; } = [];
    public List<PublicationFavorite> PublicationFavorites { get; set; } = [];
    public List<Publication> Publications { get; set; } = [];
    public List<PublicationView> PublicationViews { get; set; } = [];
    public List<PublicationReport> Reports { get; set; } = [];
    public List<SharedPublicationList> SharedPublicationLists { get; set; } = [];
    public List<SiteSuggestion> SiteSuggestions { get; set; } = [];
    public List<VerifiedOperation> OperationsAsAdvertiser { get; set; } = [];
    public List<VerifiedOperation> OperationsAsCounterparty { get; set; } = [];
    public List<OperationReview> ReviewsWritten { get; set; } = [];
    public List<OperationReview> ReviewsReceived { get; set; } = [];
}
