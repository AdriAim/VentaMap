namespace VentaMap.ViewModels;

public class ResultsLayoutViewModel
{
    public required HomeContentViewModel Results { get; init; }
    public required string CurrentMode { get; init; }
    public required string CurrentGroup { get; init; }
    public required string TextViewUrl { get; init; }
    public required string GalleryViewUrl { get; init; }
    public required string MapViewUrl { get; init; }
    public required string DebugSuffix { get; init; }
    public string ViewLabel { get; init; } = string.Empty;
    public bool ShowOpenModeSelector { get; init; } = true;
    public bool ShowViewSwitch { get; init; } = true;
    public bool ShowLocalityPicker { get; init; } = true;
    public bool ShowMapSelectionDescription { get; init; }
    public string? MapSelectionDescription { get; init; }
    public string? PageSize50Url { get; init; }
    public string? PageSize100Url { get; init; }
    public string? PageSize200Url { get; init; }
    public string? PreviousPageUrl { get; init; }
    public string? NextPageUrl { get; init; }
    public bool CanGoPreviousPage { get; init; }
    public bool CanGoNextPage { get; init; }
}
