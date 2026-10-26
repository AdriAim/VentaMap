using System.ComponentModel.DataAnnotations;
namespace VentaMap.Models;
public class ExchangeRateQuote
{
    public long Id { get; set; }
    public decimal Rate { get; set; }
    [StringLength(30)] public string Source { get; set; } = string.Empty;
    public DateTime ObtainedAtUtc { get; set; }
}
