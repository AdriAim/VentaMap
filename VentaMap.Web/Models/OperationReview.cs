using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class OperationReview
{
    public int Id { get; set; }
    public int VerifiedOperationId { get; set; }
    public VerifiedOperation? VerifiedOperation { get; set; }
    public int ReviewerUserId { get; set; }
    public ApplicationUser? ReviewerUser { get; set; }
    public int ReviewedUserId { get; set; }
    public ApplicationUser? ReviewedUser { get; set; }

    [StringLength(20)]
    public string ReviewedRole { get; set; } = ReviewRoles.Advertiser;

    public byte? Stars { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public bool DeclinedToRate { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishAtUtc { get; set; }

    [StringLength(30)]
    public string ModerationStatus { get; set; } = "Published";
}

public static class ReviewRoles
{
    public const string Advertiser = "Advertiser";
    public const string Counterparty = "Counterparty";
}
