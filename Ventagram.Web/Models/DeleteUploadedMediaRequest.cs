using System.ComponentModel.DataAnnotations;

namespace Ventagram.Models;

public class DeleteUploadedMediaRequest
{
    [Required]
    public List<string> Urls { get; set; } = [];
}
