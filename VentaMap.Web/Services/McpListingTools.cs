using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text;
using ModelContextProtocol.Server;
using VentaMap.Models;

namespace VentaMap.Services;

/// <summary>
/// The only public MCP tool. Its response is purposefully limited so third-party
/// assistants can direct people to VentaMap without receiving contact or private data.
/// </summary>
[McpServerToolType]
public class McpListingTools(
    PublicationService publicationService,
    ArgentineLocalityLookupService localityLookupService,
    IConfiguration configuration,
    McpDailyQueryLimitService dailyQueryLimitService)
{
    private const int DefaultSearchRadiusKm = 60;
    private const int ExpandedSearchRadiusKm = 600;

    [McpServerTool(Name = "buscar_anuncios")]
    [Description("Busca anuncios públicos activos de VentaMap. Devuelve exclusivamente descripción breve, precio y URL verificada del anuncio. No genera ni describe imágenes.")]
    public async Task<string> BuscarAnunciosAsync(
        [Required]
        [Description("Localidad obligatoria donde se desea buscar. Si la persona no la indicó, pedila antes de ejecutar la búsqueda.")] string localidad,
        [Description("Qué busca la persona. Por ejemplo: casa con patio o Toyota Corolla.")] string? consulta = null,
        [Description("Categoría opcional: Inmuebles, Rodados, Electronica, Embarcaciones, Agro, Generales o Moda.")] string? categoria = null,
        [Description("Precio mínimo opcional.")] decimal? precio_desde = null,
        [Description("Precio máximo opcional.")] decimal? precio_hasta = null,
        [Description("Cantidad máxima de resultados, entre 1 y 10. Por defecto: 5.")] int limite = 5)
    {
        if (string.IsNullOrWhiteSpace(localidad))
        {
            return "Para buscar anuncios en VentaMap necesitás indicar una localidad.";
        }

        var locality = await ResolveLocalityAsync(localidad);
        if (locality is null)
        {
            return "No pudimos identificar esa localidad. Indicá una localidad de Argentina más específica para buscar en VentaMap.";
        }

        if (!dailyQueryLimitService.TryConsumeQuery())
        {
            return "Alcanzaste el límite de 20 consultas diarias por MCP. Ingresá a VentaMap para buscar sin límite.";
        }

        var filters = new PublicationSearchFilters
        {
            PriceFrom = precio_desde,
            PriceTo = precio_hasta,
            RadiusKm = DefaultSearchRadiusKm
        };
        var group = Enum.TryParse<PublicationGroup>(categoria, ignoreCase: true, out var parsedGroup)
            ? parsedGroup
            : (PublicationGroup?)null;
        var publications = await SearchAsync(
            group,
            consulta,
            locality,
            filters,
            Math.Clamp(limite, 1, 10));

        // The web search widens its radius when the normal 60 km search is empty.
        if (publications.Count == 0)
        {
            filters.RadiusKm = ExpandedSearchRadiusKm;
            publications = await SearchAsync(group, consulta, locality, filters, Math.Clamp(limite, 1, 10));
        }

        // Natural language commonly changes a listing's singular noun to plural
        // ("terrenos" vs. "terreno") or wraps it in generic phrasing such as
        // "terrenos en venta". The database search is phrase-based, so retry
        // with each useful individual term after the exact search is empty.
        if (publications.Count == 0 && !string.IsNullOrWhiteSpace(consulta))
        {
            foreach (var fallbackQuery in GetQueryFallbacks(consulta))
            {
                publications = await SearchAsync(group, fallbackQuery, locality, filters, Math.Clamp(limite, 1, 10));
                if (publications.Count > 0)
                {
                    break;
                }
            }
        }
        var publicBaseUrl = (configuration["Site:PublicBaseUrl"] ?? "https://ventamap.com.ar").TrimEnd('/');
        var argentina = CultureInfo.GetCultureInfo("es-AR");

        var results = publications.Select(publication => new
        {
            descripcion = string.IsNullOrWhiteSpace(publication.ShortDescription)
                ? publication.Title
                : publication.ShortDescription,
            precio = $"{publication.Currency} {publication.Price.ToString("N0", argentina)}",
            url = $"{publicBaseUrl}/Publications/Details/{publication.Id}"
        });

        return JsonSerializer.Serialize(results);
    }

    private async Task<List<Publication>> SearchAsync(
        PublicationGroup? group,
        string? query,
        ArgentineLocalitySearchResult locality,
        PublicationSearchFilters filters,
        int limit)
        => await publicationService.SearchPublicMcpPublicationsAsync(
            group,
            query?.Trim(),
            locality.Latitude,
            locality.Longitude,
            filters,
            limit);

    private async Task<ArgentineLocalitySearchResult?> ResolveLocalityAsync(string locality)
    {
        var localMatches = await localityLookupService.SearchLocalAsync(locality);
        var externalMatches = await localityLookupService.SearchExternalAsync(locality);
        var normalizedQuery = NormalizeLocalityKey(locality);

        return localMatches
            .Concat(externalMatches)
            .GroupBy(result => $"{NormalizeLocalityKey(result.Locality)}|{NormalizeLocalityKey(result.Province)}")
            .Select(group => group.OrderByDescending(result => result.IsLocal).First())
            .OrderByDescending(result => GetLocalityMatchScore(result.Locality, normalizedQuery))
            .ThenByDescending(result => result.IsLocal)
            .ThenBy(result => result.Locality)
            .FirstOrDefault();
    }

    private static int GetLocalityMatchScore(string locality, string query)
    {
        var normalizedLocality = NormalizeLocalityKey(locality);
        if (string.Equals(normalizedLocality, query, StringComparison.Ordinal)) return 4;
        if (normalizedLocality.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(query, StringComparer.Ordinal)) return 3;
        if (normalizedLocality.StartsWith(query, StringComparison.Ordinal)) return 2;
        return normalizedLocality.Contains(query, StringComparison.Ordinal) ? 1 : 0;
    }

    private static string NormalizeLocalityKey(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string SingularizeWords(string value)
        => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.Length > 4 && word.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? word[..^1]
                : word));

    private static IEnumerable<string> GetQueryFallbacks(string query)
    {
        var singularQuery = SingularizeWords(query);
        if (!string.Equals(singularQuery, query, StringComparison.OrdinalIgnoreCase))
        {
            yield return singularQuery;
        }

        var genericTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "busco", "buscar", "anuncio", "anuncios", "de", "del", "en", "venta", "alquiler", "con", "para", "por", "una", "uno", "las", "los", "que"
        };

        foreach (var word in singularQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(word => word.Length > 2 && !genericTerms.Contains(word))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return word;
        }
    }
}
