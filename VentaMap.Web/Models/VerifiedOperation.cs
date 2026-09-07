using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class VerifiedOperation
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication? Publication { get; set; }
    public PublicationOperationType? OperationType { get; set; }
    public int AdvertiserUserId { get; set; }
    public ApplicationUser? AdvertiserUser { get; set; }
    public int? CounterpartyUserId { get; set; }
    public ApplicationUser? CounterpartyUser { get; set; }

    [StringLength(160)]
    public string CounterpartyEmail { get; set; } = string.Empty;

    [StringLength(20)]
    public string CounterpartyKind { get; set; } = CounterpartyKinds.External;

    [StringLength(40)]
    public string Status { get; set; } = VerifiedOperationStatuses.PendingCounterpartyConfirmation;

    [StringLength(64)]
    public string ResponseTokenHash { get; set; } = string.Empty;

    public DateTime ResponseTokenExpiresAtUtc { get; set; }
    public bool CounterpartyReportedProblem { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CounterpartyRespondedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public List<OperationReview> Reviews { get; set; } = [];
}

public static class CounterpartyKinds
{
    public const string Registered = "Registered";
    public const string External = "External";
}

public static class VerifiedOperationStatuses
{
    public const string PendingCounterpartyConfirmation = "PendingCounterpartyConfirmation";
    public const string Confirmed = "Confirmed";
    public const string Rejected = "Rejected";
}
