using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;
using VentaMap.ViewModels;

namespace VentaMap.Controllers;

[ApiController]
[Route("api/content")]
public partial class ContentController(
    PublicationService publicationService,
    PublicationAnalyticsService publicationAnalyticsService,
    PublicationGroupTypeService publicationGroupTypeService,
    PublicationCategoryService publicationCategoryService,
    PublicationCategoryFieldService publicationCategoryFieldService,
    ReportService reportService,
    FavoriteService favoriteService,
    SuggestionService suggestionService,
    CloudflareR2ImageStorageService imageStorageService,
    CurrentUserAccessor currentUserAccessor,
    NavigationLocalityService navigationLocalityService,
    ReviewService reviewService,
    VentaMapParameterService parameters,
    BillingService billingService,
    VentaMapDbContext db,
    ILogger<ContentController> logger,
    IConfiguration configuration) : Controller
{
    private const int MaxMapPublications = 250;
    private const int DefaultSearchRadiusKm = 60;
    private const int ExpandedSearchRadiusKm = 600;
    private const string AnonymousViewerCookieName = "ventamap.viewer";

    [HttpGet("home")]
    public async Task<IActionResult> Home([FromQuery] string? group = "Inmuebles", [FromQuery] string? mode = "Galeria", [FromQuery] string? query = null, [FromQuery] string? flash = null, [FromQuery] int? categoryId = null, [FromQuery] decimal? priceFrom = null, [FromQuery] decimal? priceTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var includeDebug = await CanViewDebugPublicationsAsync();
        var selectedGroup = ParseGroupFilter(group);
        var selectedGroupName = selectedGroup?.ToDisplayName() ?? "Todos";
        var selectedMode = NormalizeBrowseMode(mode);
        var safePageSize = NormalizeTextPageSize(pageSize);
        var safePage = Math.Max(1, page);
        var publications = new List<Publication>();
        var expandedRadiusPublications = new List<Publication>();
        var totalResults = 0;
        var expandedRadiusTotalResults = 0;
        var effectiveLocality = await navigationLocalityService.GetEffectiveLocalityAsync(HttpContext);
        var userLocalityLabel = effectiveLocality?.DisplayLabel;
        var userLocalityLatitude = effectiveLocality?.Latitude;
        var userLocalityLongitude = effectiveLocality?.Longitude;
        var favoritePublicationIds = new HashSet<int>();
        var favoriteLists = new List<FavoriteListSummaryViewModel>();
        var filters = BuildSearchFilters(priceFrom, priceTo, Request.Query);
        var priceSliderMax = NormalizePriceSliderMax(await publicationService.GetActiveMaxPriceAsync(selectedGroup, includeDebug), filters);
        var requireLocalitySelection = effectiveLocality is null;
        var requiredFields = await GetRequiredFieldsForSearchGroupAsync(selectedGroup);
        var categoryOptions = selectedGroup is null
            ? []
            : await publicationCategoryService.GetActiveByGroupAsync(selectedGroup.Value);

        if (!requireLocalitySelection && selectedMode == "Texto")
        {
            totalResults = await publicationService.CountActivePublicationsAsync(
                selectedGroup,
                query,
                userLocalityLatitude,
                userLocalityLongitude,
                filters,
                includeDebug);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalResults / (double)safePageSize));
            safePage = Math.Min(safePage, totalPages);
            publications = await publicationService.SearchActivePublicationsPageAsync(
                selectedGroup,
                query,
                (safePage - 1) * safePageSize,
                safePageSize,
                userLocalityLatitude,
                userLocalityLongitude,
                filters,
                includeDebug);

            if (ShouldLoadExpandedRadiusFallback(filters, totalResults))
            {
                var expandedFilters = CloneFiltersWithRadius(filters, ExpandedSearchRadiusKm);
                expandedRadiusTotalResults = await publicationService.CountActivePublicationsAsync(
                    selectedGroup,
                    query,
                    userLocalityLatitude,
                    userLocalityLongitude,
                    expandedFilters,
                    includeDebug);
                if (expandedRadiusTotalResults > 0)
                {
                    expandedRadiusPublications = await publicationService.SearchActivePublicationsPageAsync(
                        selectedGroup,
                        query,
                        0,
                        safePageSize,
                        userLocalityLatitude,
                        userLocalityLongitude,
                        expandedFilters,
                        includeDebug);
                }
            }
        }
        else if (!requireLocalitySelection && selectedMode == "Mapa")
        {
            totalResults = await publicationService.CountActivePublicationsAsync(
                selectedGroup,
                query,
                userLocalityLatitude,
                userLocalityLongitude,
                filters,
                includeDebug);
            if (totalResults > 0)
            {
                publications = await publicationService.SearchActivePublicationsPageAsync(
                    selectedGroup,
                    query,
                    0,
                    MaxMapPublications,
                    userLocalityLatitude,
                    userLocalityLongitude,
                    filters,
                    includeDebug);
            }
            else if (ShouldLoadExpandedRadiusFallback(filters, totalResults))
            {
                var expandedFilters = CloneFiltersWithRadius(filters, ExpandedSearchRadiusKm);
                expandedRadiusTotalResults = await publicationService.CountActivePublicationsAsync(
                    selectedGroup,
                    query,
                    userLocalityLatitude,
                    userLocalityLongitude,
                    expandedFilters,
                    includeDebug);
                if (expandedRadiusTotalResults > 0)
                {
                    expandedRadiusPublications = await publicationService.SearchActivePublicationsPageAsync(
                        selectedGroup,
                        query,
                        0,
                        safePageSize,
                        userLocalityLatitude,
                        userLocalityLongitude,
                        expandedFilters,
                        includeDebug);
                }
            }
        }

        if (currentUserAccessor.UserId is int currentUserId)
        {
            favoritePublicationIds = await favoriteService.GetFavoritePublicationIdsAsync(currentUserId, publications
                .Select(x => x.Id)
                .Concat(expandedRadiusPublications.Select(x => x.Id))
                .Distinct());
            favoriteLists = await favoriteService.GetListSummariesAsync(currentUserId);
        }

        var computedTotalPages = selectedMode == "Texto"
            ? Math.Max(1, (int)Math.Ceiling(totalResults / (double)safePageSize))
            : (publications.Count > 0 ? 1 : 0);
        var model = new HomeContentViewModel
        {
            Group = selectedGroupName,
            GroupOptions = await GetBrowseGroupOptionsAsync(),
            CategoryOptions = categoryOptions,
            SelectedCategoryId = filters.CategoryId,
            Mode = selectedMode,
            Query = query,
            Operation = filters.Operation,
            PriceFrom = filters.PriceFrom,
            PriceTo = filters.PriceTo,
            RadiusKm = filters.RadiusKm,
            PriceSliderMax = priceSliderMax,
            RequiredFilterFields = requiredFields,
            SelectedFieldFilters = filters.FieldFilters,
            Publications = publications,
            ExpandedRadiusPublications = expandedRadiusPublications,
            Page = safePage,
            PageSize = safePageSize,
            TotalResults = totalResults,
            TotalPages = computedTotalPages,
            ExpandedRadiusTotalResults = expandedRadiusTotalResults,
            ExpandedRadiusKm = expandedRadiusPublications.Count > 0 ? ExpandedSearchRadiusKm : null,
            UserLocalityLabel = userLocalityLabel,
            UserLocalityLatitude = userLocalityLatitude,
            UserLocalityLongitude = userLocalityLongitude,
            RequireLocalitySelection = requireLocalitySelection,
            CanManageFavorites = currentUserAccessor.IsAuthenticated,
            IsPaidSiteEnabled = await parameters.GetBoolAsync(VentaMapParameterService.PaidSiteEnabled, fallback: false),
            FavoritePublicationIds = favoritePublicationIds,
            FavoriteLists = favoriteLists,
            MapStyleUrl = configuration["Map:StyleUrl"] ?? string.Empty,
            MapTilesUrlTemplate = configuration["Map:TilesUrlTemplate"] ?? string.Empty,
            MapAttributionHtml = configuration["Map:AttributionHtml"] ?? string.Empty,
            MapGeocodingSearchUrlTemplate = configuration["Map:GeocodingSearchUrlTemplate"] ?? string.Empty,
            MapReverseGeocodingUrlTemplate = configuration["Map:ReverseGeocodingUrlTemplate"] ?? string.Empty,
            FlashMessage = flash,
            GalleryApiEndpoint = BuildGalleryApiEndpoint(selectedGroupName, query, filters, includeDebug),
            MapMarkersApiEndpoint = BuildMapMarkersApiEndpoint(selectedGroupName, query, filters, includeDebug),
            MarkersJson = JsonSerializer.Serialize(publications
                .Where(x => x.Latitude.HasValue && x.Longitude.HasValue && !x.HideFromMap)
                .Select(x => new
                {
                    id = x.Id,
                    code = x.ToAdCode(),
                    groupName = x.Group,
                    videoUrl = x.PrimaryVideoUrl,
                    title = x.Title,
                    shortDescription = x.ShortDescription,
                    locality = x.Locality,
                    isFavorite = favoritePublicationIds.Contains(x.Id),
                    image = x.ImageList.FirstOrDefault(),
                    images = x.ImageList.Take(11).ToList(),
                    operationLabel = x.OperationType.HasValue ? x.OperationType.Value.ToDisplayName() : null,
                    categoryLabel = x.Category != null ? x.Category.Name : null,
                    detailsUrl = BuildPublicationDetailsUrl(x.Id, includeDebug),
                    lat = x.Latitude,
                    lng = x.Longitude,
                    price = FormatPublicationPrice(x.Currency, x.Price, x.OperationType, "N0"),
                    priceTooltip = FormatPublicationPrice(x.Currency, x.Price, x.OperationType, "N0")
                }))
        };

        return PartialView("~/Views/Content/Home.cshtml", model);
    }

    [HttpGet("browse")]
    public async Task<IActionResult> Browse([FromQuery] string? group = "Inmuebles", [FromQuery] string? mode = "Galeria", [FromQuery] string? query = null, [FromQuery] string? flash = null, [FromQuery] int? categoryId = null, [FromQuery] decimal? priceFrom = null, [FromQuery] decimal? priceTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var includeDebug = await CanViewDebugPublicationsAsync();
        var selectedGroup = ParseGroupFilter(group);
        var selectedGroupName = selectedGroup?.ToDisplayName() ?? "Todos";
        var selectedMode = NormalizeBrowseMode(mode);
        var safePageSize = NormalizeTextPageSize(pageSize);
        var safePage = Math.Max(1, page);
        var publications = new List<Publication>();
        var expandedRadiusPublications = new List<Publication>();
        var totalResults = 0;
        var expandedRadiusTotalResults = 0;
        var effectiveLocality = await navigationLocalityService.GetEffectiveLocalityAsync(HttpContext);
        var userLocalityLabel = effectiveLocality?.DisplayLabel;
        var userLocalityLatitude = effectiveLocality?.Latitude;
        var userLocalityLongitude = effectiveLocality?.Longitude;
        var favoritePublicationIds = new HashSet<int>();
        var favoriteLists = new List<FavoriteListSummaryViewModel>();
        var filters = BuildSearchFilters(priceFrom, priceTo, Request.Query);
        var priceSliderMax = NormalizePriceSliderMax(await publicationService.GetActiveMaxPriceAsync(selectedGroup, includeDebug), filters);
        var requireLocalitySelection = effectiveLocality is null;
        var requiredFields = await GetRequiredFieldsForSearchGroupAsync(selectedGroup);
        var categoryOptions = selectedGroup is null
            ? []
            : await publicationCategoryService.GetActiveByGroupAsync(selectedGroup.Value);

        if (!requireLocalitySelection && selectedMode == "Texto")
        {
            totalResults = await publicationService.CountActivePublicationsAsync(
                selectedGroup,
                query,
                userLocalityLatitude,
                userLocalityLongitude,
                filters,
                includeDebug);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalResults / (double)safePageSize));
            safePage = Math.Min(safePage, totalPages);
            publications = await publicationService.SearchActivePublicationsPageAsync(
                selectedGroup,
                query,
                (safePage - 1) * safePageSize,
                safePageSize,
                userLocalityLatitude,
                userLocalityLongitude,
                filters,
                includeDebug);

            if (ShouldLoadExpandedRadiusFallback(filters, totalResults))
            {
                var expandedFilters = CloneFiltersWithRadius(filters, ExpandedSearchRadiusKm);
                expandedRadiusTotalResults = await publicationService.CountActivePublicationsAsync(
                    selectedGroup,
                    query,
                    userLocalityLatitude,
                    userLocalityLongitude,
                    expandedFilters,
                    includeDebug);
                if (expandedRadiusTotalResults > 0)
                {
                    expandedRadiusPublications = await publicationService.SearchActivePublicationsPageAsync(
                        selectedGroup,
                        query,
                        0,
                        safePageSize,
                        userLocalityLatitude,
                        userLocalityLongitude,
                        expandedFilters,
                        includeDebug);
                }
            }
        }
        else if (!requireLocalitySelection && selectedMode == "Mapa")
        {
            totalResults = await publicationService.CountActivePublicationsAsync(
                selectedGroup,
                query,
                userLocalityLatitude,
                userLocalityLongitude,
                filters,
                includeDebug);
            if (totalResults > 0)
            {
                publications = await publicationService.SearchActivePublicationsPageAsync(
                    selectedGroup,
                    query,
                    0,
                    MaxMapPublications,
                    userLocalityLatitude,
                    userLocalityLongitude,
                    filters,
                    includeDebug);
            }
            else if (ShouldLoadExpandedRadiusFallback(filters, totalResults))
            {
                var expandedFilters = CloneFiltersWithRadius(filters, ExpandedSearchRadiusKm);
                expandedRadiusTotalResults = await publicationService.CountActivePublicationsAsync(
                    selectedGroup,
                    query,
                    userLocalityLatitude,
                    userLocalityLongitude,
                    expandedFilters,
                    includeDebug);
                if (expandedRadiusTotalResults > 0)
                {
                    expandedRadiusPublications = await publicationService.SearchActivePublicationsPageAsync(
                        selectedGroup,
                        query,
                        0,
                        safePageSize,
                        userLocalityLatitude,
                        userLocalityLongitude,
                        expandedFilters,
                        includeDebug);
                }
            }
        }

        if (currentUserAccessor.UserId is int currentUserId)
        {
            favoritePublicationIds = await favoriteService.GetFavoritePublicationIdsAsync(currentUserId, publications
                .Select(x => x.Id)
                .Concat(expandedRadiusPublications.Select(x => x.Id))
                .Distinct());
            favoriteLists = await favoriteService.GetListSummariesAsync(currentUserId);
        }

        var computedTotalPages = selectedMode == "Texto"
            ? Math.Max(1, (int)Math.Ceiling(totalResults / (double)safePageSize))
            : (publications.Count > 0 ? 1 : 0);
        var model = new HomeContentViewModel
        {
            Group = selectedGroupName,
            GroupOptions = await GetBrowseGroupOptionsAsync(),
            CategoryOptions = categoryOptions,
            SelectedCategoryId = filters.CategoryId,
            Mode = selectedMode,
            Query = query,
            Operation = filters.Operation,
            PriceFrom = filters.PriceFrom,
            PriceTo = filters.PriceTo,
            RadiusKm = filters.RadiusKm,
            PriceSliderMax = priceSliderMax,
            RequiredFilterFields = requiredFields,
            SelectedFieldFilters = filters.FieldFilters,
            Publications = publications,
            ExpandedRadiusPublications = expandedRadiusPublications,
            Page = safePage,
            PageSize = safePageSize,
            TotalResults = totalResults,
            TotalPages = computedTotalPages,
            ExpandedRadiusTotalResults = expandedRadiusTotalResults,
            ExpandedRadiusKm = expandedRadiusPublications.Count > 0 ? ExpandedSearchRadiusKm : null,
            UserLocalityLabel = userLocalityLabel,
            UserLocalityLatitude = userLocalityLatitude,
            UserLocalityLongitude = userLocalityLongitude,
            RequireLocalitySelection = requireLocalitySelection,
            CanManageFavorites = currentUserAccessor.IsAuthenticated,
            IsPaidSiteEnabled = await parameters.GetBoolAsync(VentaMapParameterService.PaidSiteEnabled, fallback: false),
            FavoritePublicationIds = favoritePublicationIds,
            FavoriteLists = favoriteLists,
            MapStyleUrl = configuration["Map:StyleUrl"] ?? string.Empty,
            MapTilesUrlTemplate = configuration["Map:TilesUrlTemplate"] ?? string.Empty,
            MapAttributionHtml = configuration["Map:AttributionHtml"] ?? string.Empty,
            MapGeocodingSearchUrlTemplate = configuration["Map:GeocodingSearchUrlTemplate"] ?? string.Empty,
            MapReverseGeocodingUrlTemplate = configuration["Map:ReverseGeocodingUrlTemplate"] ?? string.Empty,
            FlashMessage = flash,
            GalleryApiEndpoint = BuildGalleryApiEndpoint(selectedGroupName, query, filters, includeDebug),
            MapMarkersApiEndpoint = BuildMapMarkersApiEndpoint(selectedGroupName, query, filters, includeDebug),
            MarkersJson = JsonSerializer.Serialize(publications
                .Where(x => x.Latitude.HasValue && x.Longitude.HasValue && !x.HideFromMap)
                .Select(x => new
                {
                    id = x.Id,
                    code = x.ToAdCode(),
                    groupName = x.Group,
                    videoUrl = x.PrimaryVideoUrl,
                    title = x.Title,
                    shortDescription = x.ShortDescription,
                    locality = x.Locality,
                    isFavorite = favoritePublicationIds.Contains(x.Id),
                    image = x.ImageList.FirstOrDefault(),
                    images = x.ImageList.Take(11).ToList(),
                    operationLabel = x.OperationType.HasValue ? x.OperationType.Value.ToDisplayName() : null,
                    categoryLabel = x.Category != null ? x.Category.Name : null,
                    detailsUrl = BuildPublicationDetailsUrl(x.Id, includeDebug),
                    lat = x.Latitude,
                    lng = x.Longitude,
                    price = FormatPublicationPrice(x.Currency, x.Price, x.OperationType, "N0"),
                    priceTooltip = FormatPublicationPrice(x.Currency, x.Price, x.OperationType, "N0")
                }))
        };

        return PartialView("~/Views/Content/Browse.cshtml", model);
    }

    [HttpGet("gallery-items")]
    public async Task<IActionResult> GalleryItems([FromQuery] string? group = "Inmuebles", [FromQuery] string? query = null, [FromQuery] int? categoryId = null, [FromQuery] decimal? priceFrom = null, [FromQuery] decimal? priceTo = null, [FromQuery] int offset = 0, [FromQuery] int limit = 20)
    {
        var includeDebug = await CanViewDebugPublicationsAsync();
        var effectiveLocality = await navigationLocalityService.GetEffectiveLocalityAsync(HttpContext);
        var selectedGroup = ParseGroupFilter(group);
        var safeOffset = Math.Max(0, offset);
        var safeLimit = Math.Clamp(limit, 1, 60);
        var filters = BuildSearchFilters(priceFrom, priceTo, Request.Query);
        var items = await publicationService.SearchActivePublicationsPageAsync(
            selectedGroup,
            query,
            safeOffset,
            safeLimit + 1,
            effectiveLocality?.Latitude,
            effectiveLocality?.Longitude,
            filters,
            includeDebug);
        var usedExpandedRadius = false;
        var expandedRadiusTotalResults = 0;
        var expandedRadiusKm = 0;
        if (safeOffset == 0 && items.Count == 0 && ShouldLoadExpandedRadiusFallback(filters, 0))
        {
            var expandedFilters = CloneFiltersWithRadius(filters, ExpandedSearchRadiusKm);
            expandedRadiusTotalResults = await publicationService.CountActivePublicationsAsync(
                selectedGroup,
                query,
                effectiveLocality?.Latitude,
                effectiveLocality?.Longitude,
                expandedFilters,
                includeDebug);
            if (expandedRadiusTotalResults > 0)
            {
                items = await publicationService.SearchActivePublicationsPageAsync(
                    selectedGroup,
                    query,
                    safeOffset,
                    safeLimit + 1,
                    effectiveLocality?.Latitude,
                    effectiveLocality?.Longitude,
                    expandedFilters,
                    includeDebug);
                usedExpandedRadius = true;
                expandedRadiusKm = ExpandedSearchRadiusKm;
            }
        }
        var hasMore = items.Count > safeLimit;
        var payloadItems = items.Take(safeLimit).ToList();
        var favoritePublicationIds = currentUserAccessor.UserId is int currentUserId
            ? await favoriteService.GetFavoritePublicationIdsAsync(currentUserId, payloadItems.Select(x => x.Id))
            : [];
        var payload = payloadItems
            .Select(item => MapGalleryItem(item, favoritePublicationIds.Contains(item.Id), includeDebug))
            .ToList();

        return Ok(new
        {
            items = payload,
            hasMore,
            nextOffset = safeOffset + payload.Count,
            usedExpandedRadius,
            expandedRadiusKm,
            expandedRadiusTotalResults
        });
    }

    [HttpGet("map-markers")]
    public async Task<IActionResult> MapMarkers([FromQuery] string? group = "Inmuebles", [FromQuery] string? query = null, [FromQuery] int? categoryId = null, [FromQuery] decimal? priceFrom = null, [FromQuery] decimal? priceTo = null, [FromQuery] double? north = null, [FromQuery] double? south = null, [FromQuery] double? east = null, [FromQuery] double? west = null)
    {
        var includeDebug = await CanViewDebugPublicationsAsync();
        var effectiveLocality = await navigationLocalityService.GetEffectiveLocalityAsync(HttpContext);
        var selectedGroup = ParseGroupFilter(group);
        var filters = BuildSearchFilters(priceFrom, priceTo, Request.Query);
        filters.North = north;
        filters.South = south;
        filters.East = east;
        filters.West = west;

        var limitedPublications = await publicationService.SearchActivePublicationsPageAsync(
            selectedGroup,
            query,
            0,
            MaxMapPublications,
            effectiveLocality?.Latitude,
            effectiveLocality?.Longitude,
            filters,
            includeDebug);
        var favoritePublicationIds = currentUserAccessor.UserId is int currentUserId
            ? await favoriteService.GetFavoritePublicationIdsAsync(currentUserId, limitedPublications.Select(x => x.Id))
            : [];

        return Ok(new
        {
            items = limitedPublications
                .Where(item => !item.HideFromMap)
                .Select(item => new
                {
                    id = item.Id,
                    code = item.ToAdCode(),
                    groupName = item.Group,
                    videoUrl = item.PrimaryVideoUrl,
                    title = item.Title,
                    shortDescription = item.ShortDescription,
                    locality = item.Locality,
                    isFavorite = favoritePublicationIds.Contains(item.Id),
                    image = item.ImageList.FirstOrDefault(),
                    images = item.ImageList.Take(11).ToList(),
                    operationLabel = item.OperationType.HasValue ? item.OperationType.Value.ToDisplayName() : null,
                    categoryLabel = item.Category != null ? item.Category.Name : null,
                    detailsUrl = BuildPublicationDetailsUrl(item.Id, includeDebug),
                    lat = item.Latitude,
                    lng = item.Longitude,
                    price = FormatPublicationPrice(item.Currency, item.Price, item.OperationType, "N0"),
                    priceTooltip = FormatPublicationPrice(item.Currency, item.Price, item.OperationType, "N0")
                })
        });
    }

    [HttpGet("favorite-lists")]
    public async Task<IActionResult> FavoriteLists()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para usar favoritos." });
        }

        var lists = await favoriteService.GetListSummariesAsync(userId);
        return Ok(new { lists });
    }

    [HttpGet("favorite-lists/{listId:int}")]
    public async Task<IActionResult> FavoriteListItems(int listId)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para usar favoritos." });
        }

        var includeDebug = await CanViewDebugPublicationsAsync();
        var result = await favoriteService.GetListContentAsync(userId, listId, includeDebug);
        if (result is null)
        {
            return NotFound(new { message = "La lista no existe." });
        }

        var (summary, publications) = result.Value;
        return Ok(new
        {
            list = summary,
            items = publications.Select(item => MapGalleryItem(item, true, includeDebug)).ToList()
        });
    }

    [HttpPost("favorites")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para guardar favoritos." });
        }

        if (request.PublicationId <= 0)
        {
            return BadRequest(new { message = "El anuncio indicado no es valido." });
        }

        try
        {
            var result = await favoriteService.AddFavoriteAsync(
                userId,
                request.PublicationId,
                request.ListId,
                request.NewListName,
                request.SuggestedListName);

            return Ok(new
            {
                message = result.Added
                    ? $"Guardado en {result.List.Name}."
                    : $"El anuncio ya estaba en {result.List.Name}.",
                listId = result.List.Id,
                listName = result.List.Name
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("suggestions")]
    public async Task<IActionResult> SubmitSuggestion([FromBody] SubmitSuggestionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Escribe una sugerencia válida." });
        }

        ApplicationUser? user = null;
        if (currentUserAccessor.UserId is int userId)
        {
            user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId);
        }

        var result = await suggestionService.SubmitAsync(
            user?.Id,
            user?.Name,
            user?.Email,
            request.Message);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    [HttpGet("trash")]
    public async Task<IActionResult> Trash()
    {
        var model = new TrashContentViewModel
        {
            Publications = await publicationService.GetModerationQueueAsync()
        };

        return PartialView("~/Views/Content/Trash.cshtml", model);
    }

    [HttpGet("details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var includeDebug = await CanViewDebugPublicationsAsync();
        var publication = await publicationService.GetByIdAsync(id, includeDebug);
        var isOwnerPreview = string.Equals(Request.Query["ownerView"], "1", StringComparison.Ordinal);

        if (publication is not null
            && publication.IsActive
            && (publication.ExpiresAtUtc is null || publication.ExpiresAtUtc > DateTime.UtcNow)
            && !isOwnerPreview)
        {
            await publicationAnalyticsService.TrackUniqueOpenAsync(
                publication.Id,
                publication.UserId,
                currentUserAccessor.UserId,
                GetOrCreateAnonymousViewerFingerprint());
        }

        var advertiserReviews = publication?.UserId is int advertiserUserId
            ? await reviewService.GetUserSummaryAsync(advertiserUserId, ReviewRoles.Advertiser)
            : null;
        var mapLocationLabel = publication is not null
            && publication.Latitude.HasValue
            && publication.Longitude.HasValue
            && !publication.HideFromMap
            ? await ResolveMapLocationLabelAsync(publication)
            : null;
        var model = new PublicationDetailsContentViewModel
        {
            Publication = publication,
            MapStyleUrl = configuration["Map:StyleUrl"] ?? string.Empty,
            MapTilesUrlTemplate = configuration["Map:TilesUrlTemplate"] ?? string.Empty,
            MapAttributionHtml = configuration["Map:AttributionHtml"] ?? string.Empty,
            MapLocationLabel = mapLocationLabel,
            IsAuthenticated = currentUserAccessor.IsAuthenticated,
            CurrentUserId = currentUserAccessor.UserId,
            AdvertiserReviews = advertiserReviews
        };

        return PartialView("~/Views/Content/Details.cshtml", model);
    }

    private async Task<string?> ResolveMapLocationLabelAsync(Publication publication)
    {
        var localities = await db.ArgentineLocalities
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync();

        var nearest = localities
            .OrderBy(x => Math.Pow(x.Latitude - publication.Latitude!.Value, 2)
                + Math.Pow(x.Longitude - publication.Longitude!.Value, 2))
            .FirstOrDefault();
        var locality = string.IsNullOrWhiteSpace(publication.Locality)
            ? nearest?.Locality
            : publication.Locality.Trim();
        var province = nearest?.Province;

        if (string.IsNullOrWhiteSpace(locality))
        {
            return null;
        }

        var parts = new[] { locality, province }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", parts);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var user = await LoadCurrentUserAsync();
        var suggestedLabel = user?.ArgentineLocality is null
            ? null
            : $"{user.ArgentineLocality.Locality}, {user.ArgentineLocality.Province}, Argentina";
        var defaultGroup = PublicationGroup.Inmuebles;
        var categories = await publicationCategoryService.GetActiveByGroupAsync(defaultGroup);
        var input = new PublicationCreateRequest
        {
            Group = defaultGroup,
            CategoryId = 0,
            Currency = "AR$",
            ContactName = user?.Name ?? string.Empty,
            ContactPhone = user?.Phone ?? string.Empty,
            ContactEmail = user?.Email,
            Locality = user?.ArgentineLocality?.Locality ?? string.Empty,
            Address = suggestedLabel,
            Latitude = null,
            Longitude = null,
            NoLocation = false
        };

        var model = new CreatePublicationContentViewModel
        {
            Input = input,
            GroupOptions = await publicationGroupTypeService.GetActiveAsync(),
            Categories = categories,
            IsAuthenticated = currentUserAccessor.IsAuthenticated,
            RequiresLogin = !currentUserAccessor.IsAuthenticated,
            CurrentUserName = user?.Name ?? User.Identity?.Name,
            CurrentUserEmail = user?.Email,
            CurrentUserPhone = user?.Phone,
            SuggestedLocalityLabel = suggestedLabel,
            SuggestedMapLatitude = user?.ArgentineLocality?.Latitude,
            SuggestedMapLongitude = user?.ArgentineLocality?.Longitude,
            PublishingBlocked = user is not null && !user.CanPublish,
            PublishingBlockedMessage = user is not null && !user.CanPublish
                ? "Tu cuenta no puede publicar nuevos anuncios hasta que un administrador revise el anuncio denunciado."
                : null,
            IsPaidSiteEnabled = await parameters.GetBoolAsync(VentaMapParameterService.PaidSiteEnabled, fallback: false),
            IsCompanyAccount = user?.IsCompany == true,
            IsBillingExempt = user?.IsBillingExempt == 1,
            IsBillingForced = user?.IsBillingExempt == 2,
            ShowPublicationChargeEstimator = user is not null,
            ActivePublicationCount = user is null ? 0 : await db.Publications.CountAsync(x => x.UserId == user.Id && x.IsActive),
            SharedListCount = user is null ? 0 : await db.SharedPublicationLists.CountAsync(x => x.UserId == user.Id),
            MapStyleUrl = configuration["Map:StyleUrl"] ?? string.Empty,
            MapTilesUrlTemplate = configuration["Map:TilesUrlTemplate"] ?? string.Empty,
            MapAttributionHtml = configuration["Map:AttributionHtml"] ?? string.Empty,
            MapGeocodingSearchUrlTemplate = configuration["Map:GeocodingSearchUrlTemplate"] ?? string.Empty,
            MapReverseGeocodingUrlTemplate = configuration["Map:ReverseGeocodingUrlTemplate"] ?? string.Empty,
            SubmitEndpoint = AppendDebugFlag("/api/content/create", IsDebugModeRequested() || user?.IsDebugUser == true),
            OperationOptions = []
        };

        return PartialView("~/Views/Content/Create.cshtml", model);
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        if (!currentUserAccessor.IsAuthenticated || currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized();
        }

        var user = await LoadCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var publication = await publicationService.GetOwnedByIdAsync(id, userId);
        if (publication is null)
        {
            return NotFound();
        }

        var categories = await publicationCategoryService.GetActiveByGroupAsync(publication.Group);
        var input = new PublicationCreateRequest
        {
            Group = publication.Group,
            CategoryId = publication.CategoryId,
            Title = publication.Title,
            Price = publication.Price,
            Currency = publication.Currency,
            Locality = publication.Locality,
            ShortDescription = publication.ShortDescription,
            LongDescription = publication.LongDescription,
            ImagesCsv = string.Join(",", publication.ImageList),
            VideoUrl = publication.PrimaryVideoUrl,
            ContactName = publication.ContactName,
            ContactPhone = publication.ContactPhone,
            ContactEmail = publication.ContactEmail,
            Operation = publication.OperationType?.ToDisplayName(),
            Featured = publication.Featured,
            InternalNotes = publication.InternalNotes,
            Latitude = publication.Latitude,
            Longitude = publication.Longitude,
            NoLocation = publication.HideFromMap,
            DynamicFields = publication.FieldValues
                .Where(x => !string.Equals(x.CategoryField?.InternalName, "operacion", StringComparison.OrdinalIgnoreCase))
                .Select(x => new PublicationDynamicFieldInput
                {
                    FieldId = x.CategoryFieldId,
                    ValueText = x.ValueText,
                    ValueNumber = x.ValueNumber,
                    ValueBoolean = x.ValueBoolean
                })
                .ToList()
        };

        var hasTechnicalValues = publication.FieldValues.Any(x =>
            x.CategoryField is not null
            && !string.Equals(x.CategoryField.InternalName, "operacion", StringComparison.OrdinalIgnoreCase)
            && !(x.CategoryField.ShowInBasicData || x.CategoryField.Required)
            && (x.ValueBoolean.HasValue || x.ValueNumber.HasValue || !string.IsNullOrWhiteSpace(x.ValueText)));

        var model = new CreatePublicationContentViewModel
        {
            Input = input,
            GroupOptions = await publicationGroupTypeService.GetActiveAsync(),
            Categories = categories,
            IsAuthenticated = true,
            RequiresLogin = false,
            CurrentUserName = user.Name,
            CurrentUserEmail = user.Email,
            CurrentUserPhone = user.Phone,
            SuggestedLocalityLabel = string.IsNullOrWhiteSpace(publication.Locality) ? null : publication.Locality,
            SuggestedMapLatitude = publication.HideFromMap
                ? user.ArgentineLocality?.Latitude
                : publication.Latitude ?? user.ArgentineLocality?.Latitude,
            SuggestedMapLongitude = publication.HideFromMap
                ? user.ArgentineLocality?.Longitude
                : publication.Longitude ?? user.ArgentineLocality?.Longitude,
            IsPaidSiteEnabled = await parameters.GetBoolAsync(VentaMapParameterService.PaidSiteEnabled, fallback: false),
            IsCompanyAccount = user.IsCompany,
            IsBillingExempt = user.IsBillingExempt == 1,
            IsBillingForced = user.IsBillingExempt == 2,
            ShowPublicationChargeEstimator = false,
            MapStyleUrl = configuration["Map:StyleUrl"] ?? string.Empty,
            MapTilesUrlTemplate = configuration["Map:TilesUrlTemplate"] ?? string.Empty,
            MapAttributionHtml = configuration["Map:AttributionHtml"] ?? string.Empty,
            MapGeocodingSearchUrlTemplate = configuration["Map:GeocodingSearchUrlTemplate"] ?? string.Empty,
            MapReverseGeocodingUrlTemplate = configuration["Map:ReverseGeocodingUrlTemplate"] ?? string.Empty,
            FormEyebrow = "Mis anuncios",
            FormTitle = "Editar anuncio",
            FormDescription = "Modifica los mismos datos que usas al crear un anuncio, incluyendo imagenes, video, ubicacion y ficha tecnica.",
            SubmitButtonText = "Guardar cambios",
            CancelUrl = "/MisAnuncios",
            SubmitEndpoint = AppendDebugFlag($"/api/content/edit/{publication.Id}", IsDebugModeRequested() || user.IsDebugUser),
            ShowLocationSection = !publication.HideFromMap,
            ShowTechnicalSection = hasTechnicalValues,
            OperationOptions = await GetOperationOptionsForCategoryAsync(publication.CategoryId),
            InitialDynamicFieldValues = publication.FieldValues
                .Where(x => !string.Equals(x.CategoryField?.InternalName, "operacion", StringComparison.OrdinalIgnoreCase))
                .Select(x => new CreatePublicationDynamicFieldValueSeed
                {
                    InternalName = x.CategoryField?.InternalName ?? string.Empty,
                    Value = x.ValueBoolean.HasValue
                        ? x.ValueBoolean.Value.ToString().ToLowerInvariant()
                        : x.ValueNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? x.ValueText ?? string.Empty
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.InternalName))
                .ToList()
        };

        return PartialView("~/Views/Content/Create.cshtml", model);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] string? group = null)
    {
        var selectedGroup = ParseGroupFilter(group) ?? PublicationGroup.Inmuebles;
        var categories = await publicationCategoryService.GetActiveByGroupAsync(selectedGroup);

        return Ok(categories.Select(x => new
        {
            id = x.Id,
            name = x.Name
        }));
    }

    [HttpGet("required-filter-fields")]
    public async Task<IActionResult> RequiredFilterFields([FromQuery] string? group = null)
    {
        var selectedGroup = ParseGroupFilter(group);
        var fields = await GetRequiredFieldsForSearchGroupAsync(selectedGroup);
        return Ok(fields.Select(x => new
        {
            id = x.Id,
            label = x.Label,
            internalName = x.InternalName,
            dataType = SplitCsvOptions(x.OptionsCsv).Length > 0
                ? PublicationCategoryFieldDataType.Lista.ToString().ToLowerInvariant()
                : x.DataType.ToString().ToLowerInvariant(),
            required = x.Required,
            options = SplitCsvOptions(x.OptionsCsv)
        }));
    }

    [HttpPost("report")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Report([FromBody] ReportPublicationRequest request)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Debes iniciar sesión para denunciar un anuncio." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Datos invalidos para la denuncia." });
        }

        var reasonExists = await db.PublicationReportReasons.AnyAsync(x => x.Id == request.ReasonId && x.IsActive);
        if (!reasonExists)
        {
            return BadRequest(new { message = "Selecciona un motivo de denuncia valido." });
        }

        var result = await reportService.CreateAsync(request.PublicationId, userId, request.ReasonId, request.Comment);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    [HttpPost("create")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreatePost([FromBody] CreatePublicationApiRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                message = "Revisa los datos del formulario.",
                errors = ModelStateToFieldErrors(ModelState)
            });
        }

        if (!currentUserAccessor.IsAuthenticated || currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para publicar." });
        }

        var user = await db.Users
            .Include(x => x.ArgentineLocality)
            .FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return Unauthorized(new { message = "No se encontro el usuario autenticado." });
        }

        if (!user.CanPublish)
        {
            return StatusCode(403, new { message = "No puedes publicar nuevos anuncios hasta que un administrador revise el anuncio denunciado." });
        }

        if (await billingService.IsPublishingBlockedAsync(user))
        {
            return StatusCode(403, new { message = "Tenés un pago pendiente. Regularizalo desde Mis anuncios para publicar o modificar anuncios.", billingUrl = "/MisAnuncios" });
        }

        if (await billingService.HasReachedCompanyPublicationLimitAsync(user))
        {
            return StatusCode(403, new { message = "Tu cuenta empresa ya tiene 50 anuncios activos simultáneos. Da de baja uno para publicar otro." });
        }

        request.Currency = NormalizeCurrency(request.Currency);
        if (request.NoLocation)
        {
            request.Locality = request.Locality.Trim();
            if (string.IsNullOrWhiteSpace(request.Locality))
            {
                request.Locality = user.ArgentineLocality?.Locality?.Trim() ?? string.Empty;
            }
            if (!request.Latitude.HasValue)
            {
                request.Latitude = user.ArgentineLocality?.Latitude;
            }
            if (!request.Longitude.HasValue)
            {
                request.Longitude = user.ArgentineLocality?.Longitude;
            }
            request.Address = null;
        }
        request.ContactName = user.Name;
        request.ContactPhone = user.Phone;
        request.ContactEmail = user.Email;
        request.PublisherMode = "Account";
        request.VideoUrl = string.IsNullOrWhiteSpace(request.VideoUrl) ? null : request.VideoUrl.Trim();

        var category = await publicationCategoryService.GetActiveByIdAsync(request.CategoryId);
        request.Title = BuildPublicationTitle(category?.Name, request.Locality, request.Latitude, request.Longitude, request.NoLocation);

        var errors = await ValidateCreateRequestAsync(request);
        if (errors.Count > 0)
        {
            return BadRequest(new { message = "Revisa los datos del formulario.", errors });
        }

        var needsPersonPack = await billingService.IsPersonPublicationPackRequiredAsync(user, request);
        if (needsPersonPack)
        {
            var pendingResult = await publicationService.CreateAsync(request, user.Id, PublicationStatus.PendingPayment);
            var charge = await billingService.RequirePersonPublicationPackAsync(user, request, pendingResult.Publication.Id);
            if (charge is not null)
            {
                logger.LogInformation(
                    "Publication {PublicationId} for user {UserId} is pending payment; billing charge {ChargeId} was created or reused.",
                    pendingResult.Publication.Id,
                    user.Id,
                    charge.Id);

                return StatusCode(402, new
                {
                    message = "Este anuncio requiere el anuncio completo de $3.000. Podés pagarlo desde Mis anuncios.",
                    billingUrl = "/MisAnuncios",
                    chargeId = charge.Id
                });
            }

            await publicationService.ActivatePendingPaymentAsync(pendingResult.Publication.Id, user.Id);
            await billingService.ConsumePaidPersonPublicationPackAsync(user.Id);
            return Ok(new
            {
                message = "Anuncio creado.",
                redirectUrl = BuildPublicationDetailsUrl(pendingResult.Publication.Id, IsDebugModeRequested() || user.IsDebugUser)
            });
        }

        var result = await publicationService.CreateAsync(request, user.Id);

        return Ok(new
        {
            message = "Anuncio creado.",
            redirectUrl = BuildPublicationDetailsUrl(result.Publication.Id, IsDebugModeRequested() || user.IsDebugUser)
        });
    }

    [HttpPost("edit/{id:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> EditPost(int id, [FromBody] CreatePublicationApiRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                message = "Revisa los datos del formulario.",
                errors = ModelStateToFieldErrors(ModelState)
            });
        }

        if (!currentUserAccessor.IsAuthenticated || currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para editar." });
        }

        var user = await db.Users
            .Include(x => x.ArgentineLocality)
            .FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return Unauthorized(new { message = "No se encontro el usuario autenticado." });
        }

        if (await billingService.IsPublishingBlockedAsync(user))
        {
            return StatusCode(403, new { message = "Tenés un pago pendiente. Regularizalo desde Mis anuncios para modificar anuncios.", billingUrl = "/MisAnuncios" });
        }

        var publication = await publicationService.GetOwnedByIdAsync(id, userId);
        if (publication is null)
        {
            return NotFound(new { message = "El anuncio no existe o no te pertenece." });
        }

        request.Currency = NormalizeCurrency(request.Currency);
        if (request.NoLocation)
        {
            request.Locality = request.Locality.Trim();
            if (string.IsNullOrWhiteSpace(request.Locality))
            {
                request.Locality = user.ArgentineLocality?.Locality?.Trim() ?? string.Empty;
            }
            if (!request.Latitude.HasValue)
            {
                request.Latitude = user.ArgentineLocality?.Latitude;
            }
            if (!request.Longitude.HasValue)
            {
                request.Longitude = user.ArgentineLocality?.Longitude;
            }
            request.Address = null;
        }

        request.ContactName = user.Name;
        request.ContactPhone = user.Phone;
        request.ContactEmail = user.Email;
        request.PublisherMode = "Account";
        request.VideoUrl = string.IsNullOrWhiteSpace(request.VideoUrl) ? null : request.VideoUrl.Trim();

        var category = await publicationCategoryService.GetActiveByIdAsync(request.CategoryId);
        request.Title = BuildPublicationTitle(category?.Name, request.Locality, request.Latitude, request.Longitude, request.NoLocation);

        var errors = await ValidateCreateRequestAsync(request);
        if (errors.Count > 0)
        {
            return BadRequest(new { message = "Revisa los datos del formulario.", errors });
        }

        var updated = await publicationService.UpdateOwnedAsync(id, userId, request);
        if (!updated)
        {
            return BadRequest(new { message = "No se pudo actualizar el anuncio." });
        }

        return Ok(new
        {
            message = "Anuncio actualizado.",
            redirectUrl = BuildPublicationDetailsUrl(id, IsDebugModeRequested() || user.IsDebugUser)
        });
    }

    [HttpPost("upload-images")]
    [IgnoreAntiforgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files)
    {
        if (!currentUserAccessor.IsAuthenticated)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para subir imagenes." });
        }

        if (files is null || files.Count == 0)
        {
            logger.LogWarning("UploadImages rejected: no files bound for user {UserId}.", currentUserAccessor.UserId);
            return BadRequest(new { message = "Subi al menos una imagen." });
        }

        if (files.Count > 10)
        {
            logger.LogWarning("UploadImages rejected: too many files ({Count}) for user {UserId}.", files.Count, currentUserAccessor.UserId);
            return BadRequest(new { message = "Podes subir hasta 10 imagenes." });
        }

        var fileInfo = files.Select(file => new { file.FileName, file.Length, file.ContentType }).ToList();
        logger.LogInformation("UploadImages received {Count} files for user {UserId}: {@Files}.", files.Count, currentUserAccessor.UserId, fileInfo);

        try
        {
            var urls = await imageStorageService.UploadPublicationImagesAsync(files);
            if (urls.Count == 0)
            {
                logger.LogWarning("UploadImages produced no urls for user {UserId}. File info: {@Files}.", currentUserAccessor.UserId, fileInfo);
                return BadRequest(new { message = "No se pudieron guardar imagenes validas." });
            }

            return Ok(new { urls });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "UploadImages configuration/validation failure for user {UserId}. File info: {@Files}.", currentUserAccessor.UserId, fileInfo);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UploadImages unexpected failure for user {UserId}. File info: {@Files}.", currentUserAccessor.UserId, fileInfo);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("upload-video")]
    [IgnoreAntiforgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadVideo([FromForm] IFormFile? file)
    {
        if (!currentUserAccessor.IsAuthenticated)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para subir videos." });
        }

        if (file is null || file.Length <= 0)
        {
            return BadRequest(new { message = "Subi un video valido." });
        }

        var fileInfo = new { file.FileName, file.Length, file.ContentType };
        logger.LogInformation("UploadVideo received file for user {UserId}: {@File}.", currentUserAccessor.UserId, fileInfo);

        try
        {
            var url = await imageStorageService.UploadPublicationVideoAsync(file);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "UploadVideo validation failure for user {UserId}. File info: {@File}.", currentUserAccessor.UserId, fileInfo);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UploadVideo unexpected failure for user {UserId}. File info: {@File}.", currentUserAccessor.UserId, fileInfo);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("delete-uploaded-media")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteUploadedMedia([FromBody] DeleteUploadedMediaRequest request)
    {
        if (!currentUserAccessor.IsAuthenticated)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para borrar archivos subidos." });
        }

        var urls = request.Urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        if (urls.Count == 0)
        {
            return BadRequest(new { message = "No se recibieron archivos para borrar." });
        }

        try
        {
            await imageStorageService.DeletePublicObjectsAsync(urls);
            return Ok(new { deleted = urls.Count });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "DeleteUploadedMedia validation failure for user {UserId}.", currentUserAccessor.UserId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteUploadedMedia unexpected failure for user {UserId}.", currentUserAccessor.UserId);
            return StatusCode(500, new { message = "No se pudieron borrar los archivos subidos." });
        }
    }

    private async Task<ApplicationUser?> LoadCurrentUserAsync()
    {
        if (!currentUserAccessor.IsAuthenticated || currentUserAccessor.UserId is not int userId)
        {
            return null;
        }

        return await db.Users
            .Include(x => x.ArgentineLocality)
            .FirstOrDefaultAsync(x => x.Id == userId);
    }

    private string? GetOrCreateAnonymousViewerFingerprint()
    {
        var existingValue = Request.Cookies[AnonymousViewerCookieName]?.Trim();
        if (!string.IsNullOrWhiteSpace(existingValue))
        {
            return existingValue[..Math.Min(existingValue.Length, 64)];
        }

        if (currentUserAccessor.IsAuthenticated)
        {
            return null;
        }

        var newValue = Guid.NewGuid().ToString("N");
        Response.Cookies.Append(AnonymousViewerCookieName, newValue, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddYears(2)
        });

        return newValue;
    }

    private async Task<List<object>> ValidateCreateRequestAsync(CreatePublicationApiRequest request)
    {
        var errors = new List<object>();

        void AddError(string field, string message)
        {
            errors.Add(new { field, message });
        }

        if (!await publicationGroupTypeService.ExistsAsync(request.Group)) AddError("group", "Selecciona el tipo de anuncio.");

        var categoryExists = false;
        if (request.CategoryId <= 0)
        {
            AddError("category", "Selecciona una categoria.");
        }
        else
        {
            categoryExists = await publicationCategoryService.ExistsAsync(request.Group, request.CategoryId);
            if (!categoryExists)
            {
                AddError("category", "La categoria no corresponde al tipo elegido.");
            }
        }

        if (categoryExists)
        {
            foreach (var dynamicError in await publicationService.ValidateDynamicFieldsAsync(request))
            {
                errors.Add(dynamicError);
            }
        }

        if (request.Price <= 0) AddError("price", "Ingresa un precio mayor a cero.");
        if (string.IsNullOrWhiteSpace(request.Currency)) AddError("currency", "Selecciona la moneda.");
        if (request.NoLocation && string.IsNullOrWhiteSpace(request.Locality)) AddError("locationSearch", "Completa tu localidad de usuario o marca un punto en el mapa.");
        if (!request.NoLocation && string.IsNullOrWhiteSpace(request.Locality)) AddError("locationSearch", "Indica la ubicacion del anuncio.");
        if (!request.NoLocation && (request.Latitude is null || request.Longitude is null))
        {
            AddError("locationSearch", "Marca un punto valido en el mapa.");
        }

        if (string.IsNullOrWhiteSpace(request.ShortDescription)) AddError("shortDescription", "Completa la descripcion corta.");
        if (request.ShortDescription?.Length > 60) AddError("shortDescription", "La descripción corta debe tener como máximo 60 caracteres, incluidos los espacios.");
        if (string.IsNullOrWhiteSpace(request.LongDescription)) AddError("longDescription", "Completa la descripcion completa.");
        if (ContainsHyperlink(request.ShortDescription)) AddError("shortDescription", "No se permiten hipervinculos en la descripcion.");
        if (ContainsHyperlink(request.LongDescription)) AddError("longDescription", "No se permiten hipervinculos en la descripcion.");
        if (string.IsNullOrWhiteSpace(request.ImagesCsv)) AddError("imagesCsv", "Subi al menos una imagen.");
        if (!string.IsNullOrWhiteSpace(request.VideoUrl)
            && !Uri.IsWellFormedUriString(request.VideoUrl, UriKind.Absolute)
            && !request.VideoUrl.StartsWith("/", StringComparison.Ordinal))
        {
            AddError("videoUrl", "El video principal no tiene una URL valida.");
        }

        return errors;
    }

    private static bool ContainsHyperlink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return HyperlinkPattern().IsMatch(value);
    }

    [GeneratedRegex(
        @"(?ix)(https?://|ftp://|mailto:|www\.|\b[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.(?:com\.ar|net\.ar|org\.ar|com|net|org|info|io|app|co|uy|py|br|cl|es|dev|site|online|store|shop|me|ly)\b)",
        RegexOptions.CultureInvariant)]
    private static partial Regex HyperlinkPattern();

    private static List<object> ModelStateToFieldErrors(ModelStateDictionary modelState)
    {
        var errors = new List<object>();

        foreach (var entry in modelState)
        {
            if (entry.Value?.Errors.Count is not > 0)
            {
                continue;
            }

            var field = NormalizeModelStateFieldName(entry.Key);
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            foreach (var error in entry.Value.Errors)
            {
                var message = string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Campo inválido." : error.ErrorMessage;
                errors.Add(new { field, message });
            }
        }

        return errors;
    }

    private static string NormalizeModelStateFieldName(string field)
    {
        var raw = field.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw.StartsWith("$."))
        {
            raw = raw[2..];
        }

        if (raw.Contains('.'))
        {
            raw = raw[(raw.LastIndexOf('.') + 1)..];
        }

        return raw switch
        {
            "Group" => "group",
            "CategoryId" => "category",
            "Category" => "category",
            "Price" => "price",
            "Operation" => "operation",
            "Currency" => "currency",
            "Locality" => "locationSearch",
            "Latitude" => "locationSearch",
            "Longitude" => "locationSearch",
            "ShortDescription" => "shortDescription",
            "LongDescription" => "longDescription",
            "ImagesCsv" => "imagesCsv",
            "VideoUrl" => "videoUrl",
            _ when raw.Length == 1 => raw.ToLowerInvariant(),
            _ => char.ToLowerInvariant(raw[0]) + raw[1..]
        };
    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency)
            ? string.Empty
            : currency.Trim().ToUpperInvariant();

        return normalized switch
        {
            "ARS" or "AR$" => "AR$",
            _ => "U$D"
        };
    }

    private static PublicationSearchFilters BuildSearchFilters(decimal? priceFrom, decimal? priceTo, IQueryCollection query)
    {
        var filters = new PublicationSearchFilters
        {
            Operation = string.IsNullOrWhiteSpace(query["operation"])
                ? null
                : query["operation"].ToString().Trim(),
            CategoryId = int.TryParse(query["categoryId"], out var categoryId) && categoryId > 0 ? categoryId : null,
            PriceFrom = priceFrom is >= 0 ? priceFrom : null,
            PriceTo = priceTo is >= 0 ? priceTo : null,
            RadiusKm = NormalizeRadiusKm(query),
            North = double.TryParse(query["north"], NumberStyles.Float, CultureInfo.InvariantCulture, out var north) ? north : null,
            South = double.TryParse(query["south"], NumberStyles.Float, CultureInfo.InvariantCulture, out var south) ? south : null,
            East = double.TryParse(query["east"], NumberStyles.Float, CultureInfo.InvariantCulture, out var east) ? east : null,
            West = double.TryParse(query["west"], NumberStyles.Float, CultureInfo.InvariantCulture, out var west) ? west : null
        };

        var fieldIds = query["filterFieldId"];
        var values = query["filterValue"];
        var count = Math.Max(fieldIds.Count, values.Count);
        for (var i = 0; i < count; i++)
        {
            var rawFieldId = i < fieldIds.Count ? fieldIds[i] : null;
            var rawValue = i < values.Count ? values[i] : null;
            if (!int.TryParse(rawFieldId, out var fieldId) || fieldId <= 0 || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            filters.FieldFilters.Add(new PublicationFieldSearchFilter
            {
                FieldId = fieldId,
                Value = rawValue.Trim()
            });
        }

        return filters;
    }

    private static int? NormalizeRadiusKm(IQueryCollection query)
    {
        if (!query.ContainsKey("radioKm"))
        {
            return DefaultSearchRadiusKm;
        }

        return int.TryParse(query["radioKm"], out var radiusKm) && radiusKm > 0
            ? Math.Clamp(radiusKm, 1, 200)
            : null;
    }

    private static bool ShouldLoadExpandedRadiusFallback(PublicationSearchFilters filters, int totalResults)
    {
        return totalResults == 0
            && filters.RadiusKm == DefaultSearchRadiusKm
            && filters.North is null
            && filters.South is null
            && filters.East is null
            && filters.West is null;
    }

    private static PublicationSearchFilters CloneFiltersWithRadius(PublicationSearchFilters filters, int radiusKm)
    {
        return new PublicationSearchFilters
        {
            Operation = filters.Operation,
            CategoryId = filters.CategoryId,
            PriceFrom = filters.PriceFrom,
            PriceTo = filters.PriceTo,
            RadiusKm = radiusKm,
            North = filters.North,
            South = filters.South,
            East = filters.East,
            West = filters.West,
            FieldFilters = filters.FieldFilters
                .Select(x => new PublicationFieldSearchFilter
                {
                    FieldId = x.FieldId,
                    Value = x.Value
                })
                .ToList()
        };
    }

    private static decimal NormalizePriceSliderMax(decimal activeMaxPrice, PublicationSearchFilters filters)
    {
        var effectiveMax = new[]
        {
            activeMaxPrice,
            filters.PriceFrom ?? 0m,
            filters.PriceTo ?? 0m,
            100000m
        }.Max();

        return Math.Ceiling(effectiveMax / 10000m) * 10000m;
    }

    private static string BuildGalleryApiEndpoint(string group, string? query, PublicationSearchFilters filters, bool includeDebug)
    {
        var parts = new List<string>
        {
            $"group={Uri.EscapeDataString(group)}",
            $"query={Uri.EscapeDataString(query ?? string.Empty)}"
        };

        AddFilterQueryParts(parts, filters);
        if (includeDebug)
        {
            parts.Add("debug=1");
        }
        return $"/api/content/gallery-items?{string.Join("&", parts)}";
    }

    private static string BuildMapMarkersApiEndpoint(string group, string? query, PublicationSearchFilters filters, bool includeDebug)
    {
        var parts = new List<string>
        {
            $"group={Uri.EscapeDataString(group)}",
            $"query={Uri.EscapeDataString(query ?? string.Empty)}"
        };

        AddFilterQueryParts(parts, filters);
        if (includeDebug)
        {
            parts.Add("debug=1");
        }

        return $"/api/content/map-markers?{string.Join("&", parts)}";
    }

    private bool IsDebugModeRequested()
    {
        return string.Equals(Request.Query["debug"], "1", StringComparison.Ordinal);
    }

    private async Task<bool> CanViewDebugPublicationsAsync()
    {
        if (IsDebugModeRequested())
        {
            return true;
        }

        return currentUserAccessor.UserId is int userId
            && await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsDebugUser);
    }

    private static string AppendDebugFlag(string url, bool includeDebug)
    {
        if (!includeDebug)
        {
            return url;
        }

        return url.Contains('?', StringComparison.Ordinal) ? $"{url}&debug=1" : $"{url}?debug=1";
    }

    private static string BuildPublicationDetailsUrl(int publicationId, bool includeDebug)
    {
        return AppendDebugFlag($"/Publications/Details/{publicationId}", includeDebug);
    }

    private async Task<List<PublicationGroupType>> GetBrowseGroupOptionsAsync()
    {
        var groups = await publicationGroupTypeService.GetActiveAsync();
        if (groups.Any(x => string.Equals(x.Name, "Todos", StringComparison.OrdinalIgnoreCase)))
        {
            return groups;
        }

        return
        [
            new PublicationGroupType
            {
                Id = 0,
                Name = "Todos",
                SortOrder = int.MinValue,
                IsActive = true
            },
            .. groups
        ];
    }

    private async Task<List<PublicationCategoryField>> GetRequiredFieldsForSearchGroupAsync(PublicationGroup? group)
    {
        if (group is not null)
        {
            return await publicationCategoryFieldService.GetRequiredActiveByGroupAsync(group.Value);
        }

        var operationField = await publicationCategoryFieldService.GetOperationFilterForAllGroupsAsync();
        return operationField is null ? [] : [operationField];
    }

    private static void AddFilterQueryParts(List<string> parts, PublicationSearchFilters filters)
    {
        if (filters.CategoryId is int categoryId && categoryId > 0)
        {
            parts.Add($"categoryId={categoryId.ToString(CultureInfo.InvariantCulture)}");
        }

        if (filters.PriceFrom is decimal priceFrom)
        {
            parts.Add($"priceFrom={Uri.EscapeDataString(priceFrom.ToString(CultureInfo.InvariantCulture))}");
        }

        if (filters.PriceTo is decimal priceTo)
        {
            parts.Add($"priceTo={Uri.EscapeDataString(priceTo.ToString(CultureInfo.InvariantCulture))}");
        }

        if (filters.RadiusKm is int radiusKm && radiusKm > 0)
        {
            parts.Add($"radioKm={radiusKm.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(filters.Operation))
        {
            parts.Add($"operation={Uri.EscapeDataString(filters.Operation)}");
        }

        foreach (var filter in filters.FieldFilters)
        {
            if (filter.FieldId <= 0 || string.IsNullOrWhiteSpace(filter.Value))
            {
                continue;
            }

            parts.Add($"filterFieldId={filter.FieldId.ToString(CultureInfo.InvariantCulture)}");
            parts.Add($"filterValue={Uri.EscapeDataString(filter.Value)}");
        }
    }

    private static string[] SplitCsvOptions(string? optionsCsv)
    {
        return string.IsNullOrWhiteSpace(optionsCsv)
            ? []
            : optionsCsv.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private async Task<List<string>> GetOperationOptionsForCategoryAsync(int categoryId)
    {
        if (categoryId <= 0)
        {
            return [];
        }

        var fields = await publicationCategoryFieldService.GetActiveByCategoryIdAsync(categoryId);
        return fields
            .Where(x => string.Equals(x.InternalName, "operacion", StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => SplitCsvOptions(x.OptionsCsv))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildPublicationTitle(string? categoryName, string? locality, double? latitude, double? longitude, bool noLocation)
    {
        var category = categoryName?.Trim();
        var city = locality?.Trim();
        var hasSelectedMapLocation = !noLocation && latitude.HasValue && longitude.HasValue;

        if (!string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(city) && hasSelectedMapLocation)
        {
            return $"{category} en {city}";
        }

        return category ?? "Nuevo anuncio";
    }

    private static int NormalizeTextPageSize(int pageSize)
    {
        return pageSize switch
        {
            100 => 100,
            200 => 200,
            _ => 50
        };
    }

    private static string NormalizeBrowseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Galeria";
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var buffer = new char[normalized.Length];
        var length = 0;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer[length++] = char.ToLowerInvariant(character);
        }

        var compactValue = new string(buffer, 0, length);
        return compactValue switch
        {
            "mapa" => "Mapa",
            "texto" => "Texto",
            "galeria" => "Galeria",
            _ => "Galeria"
        };
    }

    private static PublicationGroup? ParseGroupFilter(string? value)
    {
        return string.Equals(value?.Trim(), "Todos", StringComparison.OrdinalIgnoreCase)
            ? null
            : PublicationGroupExtensions.ParseOrDefault(value);
    }

    /// <summary>
    /// Limita la cantidad de publicaciones que salen en la vista de mapa.
    ///
    /// Qué resuelve:
    /// - Evita entregar cientos o miles de markers de una sola vez.
    /// - Mantiene más liviana la serialización del JSON del mapa.
    /// - Reduce trabajo de render del navegador y evita que el mapa se vuelva tosco al moverlo.
    ///
    /// Criterio aplicado:
    /// - Solo conserva publicaciones con coordenadas válidas.
    /// - Prioriza destacadas primero.
    /// - Dentro de ese grupo, prioriza las más recientes.
    /// - Finalmente corta en un máximo fijo.
    ///
    /// La idea es que el mapa no intente mostrar "todo", sino una muestra útil y estable.
    /// Si en el futuro se quiere una política mejor, este es el único punto a reemplazar
    /// por lógica de bounding box, zoom, clustering o relevancia geográfica.
    /// </summary>
    private static List<Publication> LimitMapPublications(IEnumerable<Publication> publications, int maxItems)
    {
        return publications
            .Where(x => x.Latitude.HasValue && x.Longitude.HasValue && !x.HideFromMap)
            .OrderByDescending(x => x.Featured)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, maxItems))
            .ToList();
    }

    private static object MapGalleryItem(Publication item, bool isFavorite, bool includeDebug)
    {
        var images = item.ImageList
            .Take(11)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return new
        {
            id = item.Id,
            title = item.Title,
            shortDescription = item.ShortDescription,
            publicationCode = item.ToAdCode(),
            price = FormatPublicationPrice(item.Currency, item.Price, item.OperationType, "N0"),
            priceTooltip = FormatPublicationPrice(item.Currency, item.Price, item.OperationType, "N0"),
            operationLabel = item.OperationType?.ToDisplayName(),
            categoryLabel = item.Category?.Name,
            detailsUrl = BuildPublicationDetailsUrl(item.Id, includeDebug),
            videoUrl = item.PrimaryVideoUrl,
            images,
            groupName = item.Group.ToDisplayName(),
            isFavorite
        };
    }

    private static string FormatPublicationPrice(string? currency, decimal price, PublicationOperationType? operationType, string numericFormat)
    {
        var displayCurrency = NormalizeCurrency(currency);
        var period = operationType switch
        {
            PublicationOperationType.Alquiler => " / mes",
            PublicationOperationType.Temporario => " / día",
            _ => string.Empty
        };

        return $"{displayCurrency} {price.ToString(numericFormat, CultureInfo.GetCultureInfo("es-AR"))}{period}";
    }
}
