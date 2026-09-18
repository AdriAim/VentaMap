using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class BillingCharge
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(40)]
    public string Type { get; set; } = string.Empty; // PersonPublication | CompanyMonthlyPlan
    public decimal Amount { get; set; }
    public DateTime BillingMonthUtc { get; set; }
    [StringLength(40)]
    public string Status { get; set; } = "Pending"; // Pending | Paid | Cancelled
    [StringLength(180)]
    public string Description { get; set; } = string.Empty;
    [StringLength(100)]
    public string? Reference { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
    [StringLength(180)]
    public string? MercadoPagoPaymentId { get; set; }
}
