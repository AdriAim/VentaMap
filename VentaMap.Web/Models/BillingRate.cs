using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class BillingRate
{
    public int Id { get; set; }

    [StringLength(40)]
    public string ChargeType { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
