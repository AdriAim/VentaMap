using System.ComponentModel.DataAnnotations;

namespace VentaMap.Models;

public class DeleteUploadedMediaRequest
{
    [Required]
    public List<string> Urls { get; set; } = [];
}
