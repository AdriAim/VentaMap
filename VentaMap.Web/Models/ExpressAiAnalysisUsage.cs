namespace VentaMap.Models;

/// <summary>Auditable, server-side record used solely to apply daily AI analysis limits.</summary>
public class ExpressAiAnalysisUsage
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
