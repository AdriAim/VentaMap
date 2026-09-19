using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class Publication
{
    public int Id { get; set; }

    public PublicationGroup Group { get; set; } = PublicationGroup.Inmuebles;

    public int CategoryId { get; set; }
    public PublicationCategory? Category { get; set; }

    [StringLength(180)]
    public string Title { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public PublicationOperationType? OperationType { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "U$D";

    [StringLength(120)]
    public string Locality { get; set; } = string.Empty;

    [StringLength(60)]
    public string ShortDescription { get; set; } = string.Empty;

    public string? LongDescription { get; set; }
    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [StringLength(32)]
    public string ContactPhone { get; set; } = string.Empty;

    [StringLength(160)]
    public string? ContactEmail { get; set; }

    public PublicationStatus Status { get; set; } = PublicationStatus.Active;

    [StringLength(40)]
    public string ModerationStatus { get; set; } = "None";

    public bool Featured { get; set; }

    public string? InternalNotes { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsActive { get; set; } = true;
    public int UniqueViewCount { get; set; }
    public int UniqueFavoriteCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? ExpirationNoticeSentAtUtc { get; set; }
    public DateTime? ReportWarningSentAtUtc { get; set; }
    public DateTime? ReportTrashSentAtUtc { get; set; }
    public DateTime? TrashedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }

    [StringLength(80)]
    public string? DeactivationReason { get; set; }

    [StringLength(1000)]
    public string? DeactivationComment { get; set; }

    [StringLength(128)]
    public string? AnonymousDeletePasswordHash { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool HideFromMap { get; set; }

    public List<PublicationFieldValue> FieldValues { get; set; } = [];
    public List<PublicationMedia> MediaItems { get; set; } = [];
    public List<PublicationReport> Reports { get; set; } = [];
    public List<FavoriteListItem> FavoriteListItems { get; set; } = [];
    public List<SharedPublicationListItem> SharedPublicationListItems { get; set; } = [];
    public List<PublicationFavorite> Favorites { get; set; } = [];
    public List<PublicationView> Views { get; set; } = [];
    public List<VerifiedOperation> VerifiedOperations { get; set; } = [];

    public IReadOnlyList<string> ImageList
    {
        get => MediaItems
            .Where(x => x.MediaType == PublicationMediaType.Image && !string.IsNullOrWhiteSpace(x.Url))
            .OrderBy(x => x.SortOrder)
            .Select(x => x.Url)
            .ToList();
    }

    public string? PrimaryVideoUrl =>
        MediaItems
            .Where(x => x.MediaType == PublicationMediaType.Video && !string.IsNullOrWhiteSpace(x.Url))
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .Select(x => x.Url)
            .FirstOrDefault();
}
