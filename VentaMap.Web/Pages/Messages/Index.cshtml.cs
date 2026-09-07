using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VentaMap.Pages.Messages;

[Authorize]
public class IndexModel(IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    public string ChatBaseUrl { get; private set; } = string.Empty;
    public string ChatApiBaseUrl { get; private set; } = string.Empty;
    public string ChatHubUrl { get; private set; } = string.Empty;

    public void OnGet()
    {
        ChatBaseUrl = NormalizeChatBaseUrl(configuration["Chat:BaseUrl"]);

        ChatApiBaseUrl = ChatBaseUrl;
        ChatHubUrl = string.IsNullOrWhiteSpace(ChatBaseUrl)
            ? string.Empty
            : $"{ChatBaseUrl}/hubs/chat";
    }

    private string NormalizeChatBaseUrl(string? configuredBaseUrl)
    {
        var normalized = (configuredBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (normalized.Contains(".example.com", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        if (!IsLocalhost(uri.Host))
        {
            return normalized;
        }

        var requestHost = Request.Host.Host;
        return IsLocalhost(requestHost) ? normalized : string.Empty;
    }

    private static bool IsLocalhost(string? host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
    }
}
