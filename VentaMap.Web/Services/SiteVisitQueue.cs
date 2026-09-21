using System.Threading.Channels;

namespace VentaMap.Services;

public sealed record SiteVisitEvent(string VisitorHash, DateTime VisitedOn);

public class SiteVisitQueue
{
    private readonly Channel<SiteVisitEvent> channel = Channel.CreateBounded<SiteVisitEvent>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropWrite });

    public bool TryEnqueue(SiteVisitEvent visit) => channel.Writer.TryWrite(visit);

    public IAsyncEnumerable<SiteVisitEvent> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);
}
