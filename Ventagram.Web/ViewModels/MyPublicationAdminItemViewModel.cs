using Ventagram.Models;

namespace Ventagram.ViewModels;

public class MyPublicationAdminItemViewModel
{
    public Publication Publication { get; set; } = null!;
    public int UniqueViewCount { get; set; }
    public int UniqueFavoriteCount { get; set; }
}
