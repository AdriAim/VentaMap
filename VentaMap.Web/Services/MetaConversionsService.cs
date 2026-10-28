using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using VentaMap.Models;

namespace VentaMap.Services;

public sealed class MetaConversionsService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<MetaConversionsService> logger)
{
    public const string RegistrationEventCookieName = "ventamap.meta-registration-event";

    public async Task TrackCompletedRegistrationAsync(
        ApplicationUser user,
        HttpContext context,
        string eventId,
        CancellationToken cancellationToken = default)
    {
        var pixelId = configuration["Meta:PixelId"]?.Trim();
        var accessToken = configuration["Meta:ConversionsApiAccessToken"]?.Trim();
        var enabled = configuration.GetValue<bool>("Meta:Enabled");

        if (!enabled || string.IsNullOrWhiteSpace(pixelId) || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var apiVersion = configuration["Meta:ConversionsApiVersion"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            apiVersion = "v23.0";
        }

        var userData = new Dictionary<string, object?>
        {
            ["em"] = new[] { Hash(user.Email) },
            ["client_ip_address"] = context.Connection.RemoteIpAddress?.ToString(),
            ["client_user_agent"] = context.Request.Headers.UserAgent.ToString(),
            ["fbp"] = context.Request.Cookies["_fbp"],
            ["fbc"] = context.Request.Cookies["_fbc"]
        };

        var payload = new
        {
            data = new[]
            {
                new
                {
                    event_name = "CompleteRegistration",
                    event_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    event_id = eventId,
                    action_source = "website",
                    event_source_url = context.Request.GetDisplayUrl(),
                    user_data = userData
                }
            },
            access_token = accessToken
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"https://graph.facebook.com/{apiVersion}/{Uri.EscapeDataString(pixelId)}/events",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Meta Conversions API rejected CompleteRegistration for user {UserId}. StatusCode={StatusCode}",
                    user.Id,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Attribution cannot prevent a successful account registration.
            logger.LogWarning(ex, "Could not send Meta CompleteRegistration for user {UserId}", user.Id);
        }
    }

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()))).ToLowerInvariant();
}
