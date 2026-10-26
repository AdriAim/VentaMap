using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public sealed class ExchangeRateService(IServiceScopeFactory scopeFactory, ILogger<ExchangeRateService> logger)
{
    private const string StoredRateKey = "ExchangeRate.LastOfficialUsdSale";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly SemaphoreSlim gate = new(1, 1);
    private decimal? rate;
    private DateTime obtainedAt;
    private DateTime nextAttemptAt;
    private bool loaded;
    private bool failureNotified;

    public async Task<decimal?> GetRateAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (!loaded) await LoadLastRateAsync();
            var now = DateTime.UtcNow;
            if (now < nextAttemptAt) return UsableRate(now);

            var primary = await ReadRateAsync<PrimaryQuote>("https://dolarapi.com/v1/dolares/oficial", q => q.Venta);
            var fallback = primary is > 0 ? null : await ReadRateAsync<FallbackQuote>("https://api.bluelytics.com.ar/v2/latest", q => q.Oficial?.Venta);
            var latest = primary is > 0 ? primary : fallback;
            if (latest is > 0)
            {
                rate = latest;
                obtainedAt = DateTime.UtcNow;
                nextAttemptAt = obtainedAt.AddHours(12);
                failureNotified = false;
                await SaveLastRateAsync(primary is > 0 ? "DolarApi" : "Bluelytics");
                return rate;
            }

            nextAttemptAt = DateTime.UtcNow.AddMinutes(5);
            if (!failureNotified) failureNotified = await NotifyFailureAsync();
            return UsableRate(DateTime.UtcNow);
        }
        finally { gate.Release(); }
    }

    // Argentina uses UTC-3; accept only a quote obtained today or yesterday.
    private decimal? UsableRate(DateTime now)
    {
        var ageInCalendarDays = (now.AddHours(-3).Date - obtainedAt.AddHours(-3).Date).TotalDays;
        return rate is > 0 && ageInCalendarDays is >= 0 and <= 1 ? rate : null;
    }

    private async Task LoadLastRateAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VentaMapDbContext>();
            var latest = await db.ExchangeRateQuotes.AsNoTracking().OrderByDescending(x => x.ObtainedAtUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
            if (latest != null)
            {
                rate = latest.Rate;
                obtainedAt = DateTime.SpecifyKind(latest.ObtainedAtUtc, DateTimeKind.Utc);
                nextAttemptAt = obtainedAt.AddHours(12);
            }
            else
            {
                var entry = await db.VentaMapParameters.AsNoTracking().SingleOrDefaultAsync(x => x.Key == StoredRateKey);
                if (entry != null && decimal.TryParse(entry.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0)
                {
                    rate = value;
                    obtainedAt = DateTime.SpecifyKind(entry.UpdatedAtUtc, DateTimeKind.Utc);
                    nextAttemptAt = obtainedAt.AddHours(12);
                }
            }
            loaded = true;
        }
        catch (Exception ex) { logger.LogError(ex, "No se pudo recuperar la ultima cotizacion guardada."); }
    }

    private async Task SaveLastRateAsync(string source)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VentaMapDbContext>();
            db.ExchangeRateQuotes.Add(new ExchangeRateQuote { Rate = rate!.Value, Source = source, ObtainedAtUtc = obtainedAt });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "No se pudo guardar la cotizacion para usarla tras un reinicio."); }
    }

    private async Task<bool> NotifyFailureAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var last = UsableRate(DateTime.UtcNow);
            var message = last is decimal value
                ? $"Fallaron DolarApi y Bluelytics. Se usa la ultima cotizacion guardada: ARS {value.ToString(CultureInfo.InvariantCulture)} por USD, obtenida el {obtainedAt:yyyy-MM-dd HH:mm} UTC."
                : "Fallaron DolarApi y Bluelytics y no hay una cotizacion reciente guardada. La conversion entre monedas no esta disponible.";
            return await sender.SendAsync("info@ventamap.com.ar", "VentaMap: fallo al obtener la cotizacion", $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>", message);
        }
        catch (Exception ex) { logger.LogError(ex, "No se pudo enviar el aviso de cotizacion."); return false; }
    }

    private static async Task<decimal?> ReadRateAsync<T>(string url, Func<T, decimal?> select)
    {
        try { var quote = await Client.GetFromJsonAsync<T>(url); return quote is null ? null : select(quote); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException) { return null; }
    }

    private sealed record PrimaryQuote([property: JsonPropertyName("venta")] decimal? Venta);
    private sealed record FallbackQuote([property: JsonPropertyName("oficial")] OfficialQuote? Oficial);
    private sealed record OfficialQuote([property: JsonPropertyName("value_sell")] decimal? Venta);
}

public sealed class ExchangeRateWorker(ExchangeRateService rates, ILogger<ExchangeRateWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await rates.GetRateAsync(); }
            catch (Exception ex) { logger.LogError(ex, "Fallo la actualizacion de cotizacion."); }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
