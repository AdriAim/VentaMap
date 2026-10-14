using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VentaMap.Pages;

public class IAModel : PageModel
{
    public string McpUrl { get; private set; } = string.Empty;

    public void OnGet()
    {
        McpUrl = $"{Request.Scheme}://{Request.Host}/mcp";
    }
}
