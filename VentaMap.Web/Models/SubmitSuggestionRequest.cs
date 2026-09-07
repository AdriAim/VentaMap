using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class SubmitSuggestionRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 5)]
    public string Message { get; set; } = string.Empty;
}
