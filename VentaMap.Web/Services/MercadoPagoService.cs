using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VentaMap.Services;

public class MercadoPagoService(HttpClient httpClient, IConfiguration configuration)
{
    public sealed record CheckoutResult(string? Url, bool IsConfigured);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["MercadoPago:AccessToken"]);
    public bool IsWebhookConfigured => !string.IsNullOrWhiteSpace(configuration["MercadoPago:WebhookSecret"]);

    public async Task<CheckoutResult> CreateCheckoutUrlAsync(int chargeId, string description, decimal amount, string? payerEmail)
    {
        var token = configuration["MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(token)) return new(null, false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/v1/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            type = "online",
            processing_mode = "manual",
            total_amount = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            external_reference = chargeId.ToString(),
            description,
            payer = string.IsNullOrWhiteSpace(payerEmail) ? null : new { email = payerEmail },
            items = new[]
            {
                new
                {
                    title = description,
                    quantity = 1,
                    unit_measure = "unit",
                    unit_price = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    total_amount = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                }
            }
        }), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new(null, true);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new(document.RootElement.TryGetProperty("checkout_url", out var checkoutUrl) ? checkoutUrl.GetString() : null, true);
    }

    public bool IsValidWebhook(string? xSignature, string? xRequestId, string? dataId)
    {
        var secret = configuration["MercadoPago:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(xSignature) ||
            string.IsNullOrWhiteSpace(xRequestId) || string.IsNullOrWhiteSpace(dataId)) return false;

        Dictionary<string, string> parts;
        try
        {
            parts = xSignature.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split('=', 2, StringSplitOptions.TrimEntries))
                .Where(x => x.Length == 2)
                .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        if (!parts.TryGetValue("ts", out var timestamp) || !parts.TryGetValue("v1", out var signature)) return false;

        // Formato indicado por Mercado Pago para firmas de Webhooks de Orders.
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{xRequestId};ts:{timestamp};";
        var expectedBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest));
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                Convert.FromHexString(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task<(bool Processed, int ChargeId, string? PaymentId)> GetProcessedOrderAsync(string orderId)
    {
        var token = configuration["MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(token)) return (false, 0, null);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/v1/orders/{Uri.EscapeDataString(orderId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, 0, null);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var processed = root.TryGetProperty("status", out var status) && status.GetString() == "processed";
        var reference = root.TryGetProperty("external_reference", out var externalReference) ? externalReference.GetString() : null;
        string? paymentId = null;
        if (root.TryGetProperty("transactions", out var transactions) &&
            transactions.TryGetProperty("payments", out var payments) && payments.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in payments.EnumerateArray())
            {
                if (!candidate.TryGetProperty("status", out var paymentStatus) || paymentStatus.GetString() != "processed") continue;
                if (candidate.TryGetProperty("id", out var payment)) paymentId = payment.GetString();
                break;
            }
        }
        return (processed && int.TryParse(reference, out var chargeId), int.TryParse(reference, out var id) ? id : 0, paymentId);
    }
}
