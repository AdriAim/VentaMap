using VentaMap.Models;

namespace VentaMap.ViewModels;

public class TrashContentViewModel
{
    public List<ModerationQueueItemViewModel> Publications { get; set; } = [];
    public List<VoluntaryDeactivationQueueItemViewModel> VoluntaryDeactivations { get; set; } = [];
}

public class ModerationQueueItemViewModel
{
    public Publication Publication { get; set; } = null!;
    public int DistinctReportersCount { get; set; }
    public int PendingReportsCount { get; set; }
    public string LatestReasonsLabel { get; set; } = string.Empty;
}

public class VoluntaryDeactivationQueueItemViewModel
{
    public Publication Publication { get; set; } = null!;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
}
