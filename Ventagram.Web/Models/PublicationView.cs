using System.ComponentModel.DataAnnotations;

namespace Ventagram.Models;

public class PublicationView
{
    public int Id { get; set; }

    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;

    public int? ViewerUserId { get; set; }
    public ApplicationUser? ViewerUser { get; set; }

    [StringLength(64)]
    public string? AnonymousFingerprint { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
