namespace VentaMap.ViewModels;

public class AddFavoriteRequest
{
    public int PublicationId { get; set; }
    public int? ListId { get; set; }
    public string? NewListName { get; set; }
    public string? SuggestedListName { get; set; }
}
