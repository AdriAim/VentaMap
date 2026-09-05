namespace Ventagram.ViewModels;

public class ResultsShellViewModel
{
    public string? AnchorId { get; init; }
    public string Kicker { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? TitleDataAttribute { get; init; }
    public string? Subtitle { get; init; }
    public string CurrentMode { get; init; } = string.Empty;
    public string? TextViewUrl { get; init; }
    public string? GalleryViewUrl { get; init; }
    public string? MapViewUrl { get; init; }
    public bool ShowOpenModeSelector { get; init; }
    public bool ShowViewSwitch { get; init; } = true;
    public string? ActionUrl { get; init; }
    public string? ActionLabel { get; init; }
    public string ActionCssClass { get; init; } = "ghost-pill";
    public string MapStyleUrl { get; init; } = string.Empty;
    public string MapTilesUrlTemplate { get; init; } = string.Empty;
    public string MapAttributionHtml { get; init; } = string.Empty;
    public double? MapInitialLatitude { get; init; }
    public double? MapInitialLongitude { get; init; }
    public string MarkersJson { get; init; } = "[]";
    public string? MapMarkersApiEndpoint { get; init; }
    public string? MapSelectionAction { get; init; }
    public string MapPlaceholderTitle { get; init; } = "Mapa no disponible";
    public string MapPlaceholderMessage { get; init; } = "Configurá el proveedor de mapas para usar esta vista.";
    public string MapSelectionTitle { get; init; } = "Elegí un anuncio en el mapa";
    public string? MapSelectionDescription { get; init; }
}
