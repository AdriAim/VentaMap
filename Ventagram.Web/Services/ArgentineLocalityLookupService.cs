using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public sealed class ArgentineLocalityLookupService(HttpClient httpClient, VentagramDbContext db)
{
    private const string GeorefBaseUrl = "https://apis.datos.gob.ar/georef/api/v2.0/";

    public async Task<IReadOnlyList<ArgentineLocalitySearchResult>> SearchLocalAsync(string query, CancellationToken cancellationToken = default)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        if (trimmedQuery.Length < 2)
        {
            return [];
        }

        var normalizedQuery = NormalizeKey(trimmedQuery);

        var localMatches = await db.ArgentineLocalities
            .AsNoTracking()
            .Where(x => x.IsActive
                && (EF.Functions.Like(x.Locality, $"%{trimmedQuery}%")
                    || EF.Functions.Like(x.Province, $"%{trimmedQuery}%")))
            .ToListAsync(cancellationToken);

        return localMatches
            .Select(ArgentineLocalitySearchResult.FromLocal)
            .OrderByDescending(x => IsExactMatch(x, normalizedQuery))
            .ThenByDescending(x => StartsWithMatch(x, normalizedQuery))
            .ThenByDescending(x => ContainsMatch(x, normalizedQuery))
            .ThenBy(x => x.Province)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Locality)
            .Take(8)
            .ToList();
    }

    public async Task<IReadOnlyList<ArgentineLocalitySearchResult>> SearchExternalAsync(string query, CancellationToken cancellationToken = default)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        if (trimmedQuery.Length < 2)
        {
            return [];
        }

        var normalizedQuery = NormalizeKey(trimmedQuery);
        var results = new List<ArgentineLocalitySearchResult>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var response = await httpClient.GetFromJsonAsync<GeorefLocalityListResponse>(
                $"{GeorefBaseUrl}localidades?nombre={Uri.EscapeDataString(trimmedQuery)}&max=8&aplanar=true",
                cancellationToken);

            foreach (var item in response?.Localidades ?? [])
            {
                if (string.IsNullOrWhiteSpace(item.Id)
                    || string.IsNullOrWhiteSpace(item.Nombre)
                    || string.IsNullOrWhiteSpace(item.ProvinciaNombre)
                    || item.CentroideLat is null
                    || item.CentroideLon is null)
                {
                    continue;
                }

                var existing = await FindExistingAsync(item.ProvinciaNombre, item.Nombre, cancellationToken);
                if (existing is not null)
                {
                    AddSearchResult(results, seenKeys, ArgentineLocalitySearchResult.FromLocal(existing));
                    continue;
                }

                AddSearchResult(results, seenKeys, ArgentineLocalitySearchResult.FromExternal(
                    item.Id,
                    item.Nombre,
                    item.ProvinciaNombre,
                    item.CentroideLat.Value,
                    item.CentroideLon.Value,
                    item.Categoria));
            }
        }
        catch
        {
            return [];
        }

        return results
            .OrderByDescending(x => IsExactMatch(x, normalizedQuery))
            .ThenByDescending(x => StartsWithMatch(x, normalizedQuery))
            .ThenByDescending(x => ContainsMatch(x, normalizedQuery))
            .ThenByDescending(x => x.IsLocal)
            .ThenBy(x => x.Province)
            .ThenBy(x => x.Locality)
            .Take(10)
            .ToList();
    }

    public async Task<ArgentineLocality> EnsureLocalityAsync(int? localId, string? externalGeorefId, CancellationToken cancellationToken = default)
    {
        if (localId is int selectedLocalId && selectedLocalId > 0)
        {
            var existing = await db.ArgentineLocalities.FirstOrDefaultAsync(
                x => x.Id == selectedLocalId && x.IsActive,
                cancellationToken);

            if (existing is not null)
            {
                return existing;
            }
        }

        if (string.IsNullOrWhiteSpace(externalGeorefId))
        {
            throw new InvalidOperationException("Selecciona una localidad valida.");
        }

        var response = await httpClient.GetFromJsonAsync<GeorefLocalityListResponse>(
            $"{GeorefBaseUrl}localidades?id={Uri.EscapeDataString(externalGeorefId.Trim())}&max=1&aplanar=true",
            cancellationToken);

        var resolved = response?.Localidades?.FirstOrDefault();
        if (resolved is null
            || string.IsNullOrWhiteSpace(resolved.Nombre)
            || string.IsNullOrWhiteSpace(resolved.ProvinciaNombre)
            || resolved.CentroideLat is null
            || resolved.CentroideLon is null)
        {
            throw new InvalidOperationException("No pudimos validar la localidad elegida. Vuelve a buscarla.");
        }

        var existingByName = await FindExistingAsync(resolved.ProvinciaNombre, resolved.Nombre, cancellationToken);
        if (existingByName is not null)
        {
            if (!existingByName.IsActive)
            {
                existingByName.IsActive = true;
                await db.SaveChangesAsync(cancellationToken);
            }

            return existingByName;
        }

        var nextSortOrder = await db.ArgentineLocalities
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var created = new ArgentineLocality
        {
            Locality = resolved.Nombre.Trim(),
            Province = resolved.ProvinciaNombre.Trim(),
            Latitude = resolved.CentroideLat.Value,
            Longitude = resolved.CentroideLon.Value,
            SortOrder = nextSortOrder + 1,
            IsActive = true
        };

        db.ArgentineLocalities.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            var concurrentExisting = await FindExistingAsync(resolved.ProvinciaNombre, resolved.Nombre, cancellationToken);
            if (concurrentExisting is not null)
            {
                return concurrentExisting;
            }

            throw;
        }
    }

    private static void AddSearchResult(
        ICollection<ArgentineLocalitySearchResult> results,
        ISet<string> seenKeys,
        ArgentineLocalitySearchResult candidate)
    {
        if (!seenKeys.Add(BuildKey(candidate.Province, candidate.Locality)))
        {
            return;
        }

        results.Add(candidate);
    }

    private Task<ArgentineLocality?> FindExistingAsync(string province, string locality, CancellationToken cancellationToken)
    {
        return db.ArgentineLocalities.FirstOrDefaultAsync(
            x => x.Province == province && x.Locality == locality,
            cancellationToken);
    }

    private static string BuildKey(string province, string locality)
    {
        return $"{NormalizeKey(province)}|{NormalizeKey(locality)}";
    }

    private static bool IsExactMatch(ArgentineLocalitySearchResult result, string normalizedQuery)
    {
        return NormalizeKey(result.Locality) == normalizedQuery
            || NormalizeKey(result.Label) == normalizedQuery;
    }

    private static bool StartsWithMatch(ArgentineLocalitySearchResult result, string normalizedQuery)
    {
        return NormalizeKey(result.Locality).StartsWith(normalizedQuery, StringComparison.Ordinal)
            || NormalizeKey(result.Label).StartsWith(normalizedQuery, StringComparison.Ordinal)
            || NormalizeKey(result.Province).StartsWith(normalizedQuery, StringComparison.Ordinal);
    }

    private static bool ContainsMatch(ArgentineLocalitySearchResult result, string normalizedQuery)
    {
        return NormalizeKey(result.Locality).Contains(normalizedQuery, StringComparison.Ordinal)
            || NormalizeKey(result.Label).Contains(normalizedQuery, StringComparison.Ordinal)
            || NormalizeKey(result.Province).Contains(normalizedQuery, StringComparison.Ordinal);
    }

    private static string NormalizeKey(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        Span<char> buffer = stackalloc char[normalized.Length];
        var index = 0;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer[index++] = char.IsLetterOrDigit(ch) ? ch : ' ';
        }

        return new string(buffer[..index]).Replace("  ", " ").Trim();
    }

    private sealed class GeorefLocalityListResponse
    {
        [JsonPropertyName("localidades")]
        public List<GeorefLocalityItem> Localidades { get; set; } = [];
    }

    private sealed class GeorefLocalityItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("provincia_nombre")]
        public string ProvinciaNombre { get; set; } = string.Empty;

        [JsonPropertyName("centroide_lat")]
        public double? CentroideLat { get; set; }

        [JsonPropertyName("centroide_lon")]
        public double? CentroideLon { get; set; }

        [JsonPropertyName("categoria")]
        public string? Categoria { get; set; }
    }
}

public sealed record ArgentineLocalitySearchResult(
    int? LocalId,
    string? ExternalId,
    string Locality,
    string Province,
    double Latitude,
    double Longitude,
    bool IsLocal,
    string? Category,
    int SortOrder = int.MaxValue)
{
    public string Label => $"{Locality}, {Province}";

    public static ArgentineLocalitySearchResult FromLocal(ArgentineLocality locality)
        => new(locality.Id, null, locality.Locality, locality.Province, locality.Latitude, locality.Longitude, true, null, locality.SortOrder);

    public static ArgentineLocalitySearchResult FromExternal(
        string externalId,
        string locality,
        string province,
        double latitude,
        double longitude,
        string? category)
        => new(null, externalId, locality, province, latitude, longitude, false, category);
}
