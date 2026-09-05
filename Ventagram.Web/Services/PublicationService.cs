using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;
using Ventagram.ViewModels;

namespace Ventagram.Services;

public class PublicationService(
    VentagramDbContext db,
    CloudflareR2ImageStorageService imageStorageService,
    ILogger<PublicationService> logger)
{
    public const string OwnerDeletedStatus = "Eliminado por usuario";

    public Task<List<Publication>> SearchActivePublicationsAsync(PublicationGroup? group, string? query, bool includeDebug = false)
        => SearchActivePublicationsAsync(group, query, null, includeDebug);

    public Task<List<Publication>> SearchActivePublicationsAsync(PublicationGroup? group, string? query, PublicationSearchFilters? filters, bool includeDebug = false)
        => BuildActiveSearchQuery(group, query, filters, includeDebug).ToListAsync();

    public Task<List<Publication>> SearchActivePublicationsAsync(PublicationGroup? group, string? query, double? referenceLatitude, double? referenceLongitude, bool includeDebug = false)
        => SearchActivePublicationsAsync(group, query, referenceLatitude, referenceLongitude, null, includeDebug);

    public Task<List<Publication>> SearchActivePublicationsAsync(PublicationGroup? group, string? query, double? referenceLatitude, double? referenceLongitude, PublicationSearchFilters? filters, bool includeDebug = false)
        => BuildOrderedActiveSearchQuery(group, query, referenceLatitude, referenceLongitude, filters, includeDebug).ToListAsync();

    public Task<int> CountActivePublicationsAsync(PublicationGroup? group, string? query, bool includeDebug = false)
        => CountActivePublicationsAsync(group, query, null, includeDebug);

    public Task<int> CountActivePublicationsAsync(PublicationGroup? group, string? query, PublicationSearchFilters? filters, bool includeDebug = false)
        => BuildActiveSearchQuery(group, query, filters, includeDebug).CountAsync();

    public Task<int> CountActivePublicationsAsync(PublicationGroup? group, string? query, double? referenceLatitude, double? referenceLongitude, PublicationSearchFilters? filters, bool includeDebug = false)
    {
        var items = BuildActiveSearchQuery(group, query, filters, includeDebug);
        if (referenceLatitude is double latitude && referenceLongitude is double longitude)
        {
            items = ApplyRadiusFilter(items, latitude, longitude, filters);
        }

        return items.CountAsync();
    }

    public async Task<decimal> GetActiveMaxPriceAsync(PublicationGroup? group, bool includeDebug = false)
    {
        var now = DateTime.UtcNow;
        var items = db.Publications
            .AsNoTracking()
            .Where(x => x.IsActive && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now));

        items = ApplyDebugVisibility(items, includeDebug);

        if (group is not null)
        {
            items = items.Where(x => x.Group == group.Value);
        }

        return await items.AnyAsync()
            ? await items.MaxAsync(x => x.Price)
            : 1000000m;
    }

    public Task<List<Publication>> SearchActivePublicationsPageAsync(PublicationGroup? group, string? query, int skip, int take, bool includeDebug = false)
        => SearchActivePublicationsPageAsync(group, query, skip, take, null, includeDebug);

    public Task<List<Publication>> SearchActivePublicationsPageAsync(PublicationGroup? group, string? query, int skip, int take, PublicationSearchFilters? filters, bool includeDebug = false)
        => BuildActiveSearchQuery(group, query, filters, includeDebug)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ToListAsync();

    public Task<List<Publication>> SearchActivePublicationsPageAsync(PublicationGroup? group, string? query, int skip, int take, double? referenceLatitude, double? referenceLongitude, bool includeDebug = false)
        => SearchActivePublicationsPageAsync(group, query, skip, take, referenceLatitude, referenceLongitude, null, includeDebug);

    public Task<List<Publication>> SearchActivePublicationsPageAsync(PublicationGroup? group, string? query, int skip, int take, double? referenceLatitude, double? referenceLongitude, PublicationSearchFilters? filters, bool includeDebug = false)
        => BuildOrderedActiveSearchQuery(group, query, referenceLatitude, referenceLongitude, filters, includeDebug)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ToListAsync();

    public async Task<List<Publication>> GetActivePublicationsAsync(bool includeDebug = false)
    {
        var now = DateTime.UtcNow;
        return await ApplyDebugVisibility(db.Publications
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Include(x => x.FieldValues)
                .ThenInclude(x => x.CategoryField)
            .Where(x => x.IsActive && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now)), includeDebug)
            .OrderByDescending(x => x.Featured)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<Publication?> GetByIdAsync(int id, bool includeDebug = false)
    {
        return await ApplyDebugVisibility(db.Publications
            .Include(x => x.User)
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Include(x => x.FieldValues)
                .ThenInclude(x => x.CategoryField)
            .Include(x => x.Reports)
                .ThenInclude(x => x.Reason)
            .Where(x => x.Id == id), includeDebug)
            .FirstOrDefaultAsync();
    }

    public async Task<List<MyPublicationAdminItemViewModel>> GetOwnedPublicationsAsync(int userId)
    {
        var publications = await db.Publications
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Where(x => x.UserId == userId && x.Status != OwnerDeletedStatus)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        if (publications.Count == 0)
        {
            return [];
        }

        return publications
            .Select(publication => new MyPublicationAdminItemViewModel
            {
                Publication = publication,
                UniqueViewCount = publication.UniqueViewCount,
                UniqueFavoriteCount = publication.UniqueFavoriteCount
            })
            .ToList();
    }

    public async Task<Publication?> GetOwnedByIdAsync(int publicationId, int userId)
    {
        return await db.Publications
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Include(x => x.FieldValues)
                .ThenInclude(x => x.CategoryField)
            .FirstOrDefaultAsync(x => x.Id == publicationId && x.UserId == userId);
    }

    public async Task<List<Publication>> GetReportedPublicationsAsync()
    {
        return await db.Publications
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Include(x => x.FieldValues)
                .ThenInclude(x => x.CategoryField)
            .Include(x => x.Reports)
                .ThenInclude(x => x.Reason)
            .Where(x => x.Reports.Any())
            .OrderByDescending(x => x.Reports.Count)
            .ThenByDescending(x => x.Reports.Max(r => r.CreatedAtUtc))
            .ToListAsync();
    }

    public Task<int> CountCreatedByUserAsync(int userId)
    {
        return db.Publications.CountAsync(x => x.UserId == userId);
    }

    public async Task<List<ModerationQueueItemViewModel>> GetModerationQueueAsync()
    {
        var items = await db.Publications
            .Include(x => x.User)
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Include(x => x.Reports.Where(r => r.ReviewStatus == "Pending"))
                .ThenInclude(x => x.Reason)
            .Where(x => x.ModerationStatus == "PendingReview" || x.ModerationStatus == "InTrash")
            .OrderByDescending(x => x.TrashedAtUtc ?? x.ReportTrashSentAtUtc ?? x.CreatedAtUtc)
            .ToListAsync();

        return items.Select(item =>
        {
            var pendingReports = item.Reports
                .Where(r => r.ReviewStatus == "Pending")
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();

            var reasonLabel = string.Join(", ", pendingReports
                .Select(r => r.Reason?.Name ?? $"Razón #{r.ReasonId}")
                .Distinct()
                .Take(4));

            return new ModerationQueueItemViewModel
            {
                Publication = item,
                PendingReportsCount = pendingReports.Count,
                DistinctReportersCount = pendingReports.Select(r => r.ReporterUserId).Distinct().Count(),
                LatestReasonsLabel = reasonLabel
            };
        }).ToList();
    }

    public async Task<List<VoluntaryDeactivationQueueItemViewModel>> GetVoluntaryDeactivationQueueAsync()
    {
        var items = await db.Publications
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Where(x => !x.IsActive
                && x.DeactivatedAtUtc != null
                && !string.IsNullOrWhiteSpace(x.DeactivationReason))
            .OrderByDescending(x => x.DeactivatedAtUtc)
            .Take(100)
            .ToListAsync();

        return items.Select(item => new VoluntaryDeactivationQueueItemViewModel
        {
            Publication = item,
            OwnerName = item.User?.Name ?? item.ContactName,
            OwnerEmail = item.User?.Email ?? item.ContactEmail ?? string.Empty
        }).ToList();
    }

    public async Task<(Publication Publication, string? AnonymousPassword)> CreateAsync(PublicationCreateRequest input, int? userId)
    {
        var category = await db.PublicationCategories
            .AsNoTracking()
            .FirstAsync(x => x.Id == input.CategoryId);

        var categoryFields = await db.PublicationCategoryFields
            .Where(x => x.IsActive
                && (x.GroupId == (byte)category.Group || x.GroupId == null)
                && (x.CategoryId == input.CategoryId || x.CategoryId == null))
            .OrderBy(x => x.CategoryId == null ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var publication = new Publication
        {
            Group = input.Group,
            CategoryId = input.CategoryId,
            Title = input.Title,
            Price = input.Price,
            OperationType = PublicationOperationTypeExtensions.ParseOrNull(input.Operation),
            Currency = input.Currency,
            Locality = input.Locality,
            ShortDescription = input.ShortDescription,
            LongDescription = input.LongDescription,
            ContactName = input.ContactName ?? string.Empty,
            ContactPhone = input.ContactPhone ?? string.Empty,
            ContactEmail = input.ContactEmail,
            Status = "Activa",
            Featured = input.Featured,
            InternalNotes = input.InternalNotes,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            HideFromMap = input.NoLocation,
            UserId = userId,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };

        publication.MediaItems.AddRange(PublicationMediaBuilder.Build(
            input.ImagesCsv,
            input.VideoUrl,
            publication.CreatedAtUtc));

        string? rawAnonymousPassword = null;
        if (input.PublisherMode == "Anonymous")
        {
            rawAnonymousPassword = AuthService.GenerateAnonymousPassword();
            publication.IsAnonymous = true;
            publication.AnonymousDeletePasswordHash = AuthService.HashPassword(rawAnonymousPassword);
        }

        foreach (var fieldValue in BuildDynamicFieldValues(input, categoryFields))
        {
            publication.FieldValues.Add(fieldValue);
        }

        db.Publications.Add(publication);
        await db.SaveChangesAsync();
        return (publication, rawAnonymousPassword);
    }

    public async Task<List<object>> ValidateDynamicFieldsAsync(PublicationCreateRequest input)
    {
        var errors = new List<object>();
        var category = await db.PublicationCategories
            .AsNoTracking()
            .FirstAsync(x => x.Id == input.CategoryId);

        var definitions = await db.PublicationCategoryFields
            .Where(x => x.IsActive
                && (x.GroupId == (byte)category.Group || x.GroupId == null)
                && (x.CategoryId == input.CategoryId || x.CategoryId == null))
            .OrderBy(x => x.CategoryId == null ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var normalizedInputs = new Dictionary<int, PublicationDynamicFieldInput>();
        foreach (var item in MergeDynamicFieldInputs(input, definitions))
        {
            normalizedInputs[item.FieldId] = item;
        }

        foreach (var field in definitions)
        {
            if (string.Equals(field.InternalName, "operacion", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            normalizedInputs.TryGetValue(field.Id, out var value);
            if (field.Required && IsMissingDynamicValue(field.DataType, value))
            {
                errors.Add(new { field = field.InternalName, message = $"Completa {field.Label}." });
                continue;
            }

            if (value is null || IsMissingDynamicValue(field.DataType, value))
            {
                continue;
            }

            if (!IsValidDynamicValue(field.DataType, value))
            {
                errors.Add(new { field = field.InternalName, message = $"El valor de {field.Label} no coincide con el tipo esperado." });
            }
        }

        foreach (var unknownField in input.DynamicFields.Where(x => definitions.All(d => d.Id != x.FieldId)))
        {
            errors.Add(new { field = $"dynamicField:{unknownField.FieldId}", message = "El campo dinamico no pertenece a la categoria seleccionada." });
        }

        var operationField = definitions.FirstOrDefault(x => string.Equals(x.InternalName, "operacion", StringComparison.OrdinalIgnoreCase));
        if (operationField is not null)
        {
            var selectedOperation = input.Operation?.Trim();
            var allowedOperations = SplitOperationOptions(operationField.OptionsCsv);

            if (operationField.Required && string.IsNullOrWhiteSpace(selectedOperation))
            {
                errors.Add(new { field = "operation", message = $"Completa {operationField.Label}." });
            }
            else if (!string.IsNullOrWhiteSpace(selectedOperation)
                && PublicationOperationTypeExtensions.ParseOrNull(selectedOperation) is null)
            {
                errors.Add(new { field = "operation", message = "El tipo de operación no es válido." });
            }
            else if (!string.IsNullOrWhiteSpace(selectedOperation)
                && allowedOperations.Count > 0
                && !allowedOperations.Contains(selectedOperation, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(new { field = "operation", message = "El tipo de operación no corresponde a la categoría seleccionada." });
            }
        }

        return errors;
    }

    public async Task<bool> DeleteAnonymousPermanentlyAsync(int publicationId, string password)
    {
        var publication = await db.Publications
            .Include(x => x.MediaItems)
            .FirstOrDefaultAsync(x => x.Id == publicationId && x.IsAnonymous);
        if (publication is null || string.IsNullOrWhiteSpace(publication.AnonymousDeletePasswordHash))
        {
            return false;
        }

        if (publication.AnonymousDeletePasswordHash != AuthService.HashPassword(password))
        {
            return false;
        }

        var mediaUrls = publication.MediaItems
            .Select(x => x.Url)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        db.Publications.Remove(publication);
        await db.SaveChangesAsync();
        await SafeDeleteMediaFromStorageAsync(mediaUrls);
        return true;
    }

    public async Task<bool> DeactivateOwnedAsync(int publicationId, int userId, string reason, string? comment)
    {
        var publication = await db.Publications.FirstOrDefaultAsync(x => x.Id == publicationId && x.UserId == userId && x.IsActive);
        if (publication is null)
        {
            return false;
        }

        publication.IsActive = false;
        publication.Status = "Baja solicitada";
        publication.DeactivationReason = string.IsNullOrWhiteSpace(reason) ? "Sin motivo" : reason.Trim();
        publication.DeactivationComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        publication.DeactivatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteOwnedPermanentlyAsync(int publicationId, int userId)
    {
        var publication = await db.Publications
            .Include(x => x.MediaItems)
            .FirstOrDefaultAsync(x => x.Id == publicationId && x.UserId == userId && !x.IsActive);
        if (publication is null)
        {
            return false;
        }

        var mediaUrls = publication.MediaItems
            .Select(x => x.Url)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Owner-side delete keeps the record for moderation/history, but removes
        // all published media and hides the ad from "Mis anuncios".
        db.RemoveRange(publication.MediaItems);
        publication.MediaItems.Clear();
        publication.IsActive = false;
        publication.Status = OwnerDeletedStatus;
        publication.ExpiresAtUtc = null;
        await db.SaveChangesAsync();
        await SafeDeleteMediaFromStorageAsync(mediaUrls);
        return true;
    }

    public async Task<bool> RepublishOwnedAsync(int publicationId, int userId)
    {
        if (await db.VerifiedOperations.AnyAsync(x =>
                x.PublicationId == publicationId
                && x.Status != VerifiedOperationStatuses.Rejected))
        {
            return false;
        }

        var publication = await db.Publications.FirstOrDefaultAsync(x => x.Id == publicationId && x.UserId == userId && !x.IsActive && x.Status != OwnerDeletedStatus);
        if (publication is null)
        {
            return false;
        }

        var previousMediaUrls = publication.MediaItems
            .Select(x => x.Url)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (publication.ModerationStatus is "PendingReview" or "Confirmed")
        {
            return false;
        }

        publication.IsActive = true;
        publication.Status = "Activa";
        publication.CreatedAtUtc = DateTime.UtcNow;
        publication.ExpiresAtUtc = DateTime.UtcNow.AddDays(30);
        publication.ExpirationNoticeSentAtUtc = null;
        publication.DeactivationReason = null;
        publication.DeactivationComment = null;
        publication.DeactivatedAtUtc = null;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<int> ExpirePublicationsAndSendNotificationsAsync(IEmailSender emailSender, ILogger logger, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expired = await db.Publications
            .Include(x => x.User)
            .Where(x => x.IsActive
                && x.ExpiresAtUtc != null
                && x.ExpiresAtUtc <= now
                && x.ExpirationNoticeSentAtUtc == null)
            .OrderBy(x => x.ExpiresAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var publication in expired)
        {
            cancellationToken.ThrowIfCancellationRequested();

            publication.IsActive = false;
            publication.Status = "Vencida";
            publication.DeactivatedAtUtc = now;

            var email = publication.User?.Email ?? publication.ContactEmail;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var subject = "Tu anuncio venció en Ventagram";
                var html = $"""
                    <p>Tu anuncio <strong>{publication.Title}</strong> finalizó luego de 30 días de estar activo.</p>
                    <p>Si quieres republicarla, debes ingresar a <strong>Mis anuncios</strong> dentro de tu usuario y usar la opción de republicar.</p>
                    """;
                var text = $"""
                    Tu anuncio "{publication.Title}" finalizó luego de 30 días de estar activo.

                    Si quieres republicarla, debes ingresar a Mis anuncios dentro de tu usuario y usar la opción de republicar.
                    """;

                try
                {
                    await emailSender.SendAsync(email, subject, html, text);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No se pudo enviar correo de vencimiento para el anuncio {PublicationId}.", publication.Id);
                }
            }

            publication.ExpirationNoticeSentAtUtc = DateTime.UtcNow;
        }

        if (expired.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return expired.Count;
    }

    public async Task<bool> UpdateOwnedAsync(
        int publicationId,
        int userId,
        PublicationCreateRequest input)
    {
        var publication = await db.Publications
            .Include(x => x.MediaItems)
            .Include(x => x.FieldValues)
            .FirstOrDefaultAsync(x => x.Id == publicationId && x.UserId == userId && x.IsActive);
        if (publication is null)
        {
            return false;
        }

        var previousMediaUrls = publication.MediaItems
            .Select(x => x.Url)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedLocality = input.Locality.Trim();
        var localityChanged = !string.Equals((publication.Locality ?? string.Empty).Trim(), normalizedLocality, StringComparison.Ordinal);
        var category = await db.PublicationCategories
            .AsNoTracking()
            .FirstAsync(x => x.Id == input.CategoryId);
        var categoryFields = await db.PublicationCategoryFields
            .Where(x => x.IsActive
                && (x.GroupId == (byte)category.Group || x.GroupId == null)
                && (x.CategoryId == input.CategoryId || x.CategoryId == null))
            .OrderBy(x => x.CategoryId == null ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        publication.Group = input.Group;
        publication.CategoryId = input.CategoryId;
        publication.Title = input.Title.Trim();
        publication.Price = input.Price;
        publication.OperationType = PublicationOperationTypeExtensions.ParseOrNull(input.Operation);
        publication.Currency = input.Currency;
        publication.Locality = normalizedLocality;
        publication.ShortDescription = input.ShortDescription.Trim();
        publication.LongDescription = input.LongDescription?.Trim();
        publication.Featured = input.Featured;
        publication.InternalNotes = input.InternalNotes;

        if (!input.NoLocation && input.Latitude.HasValue && input.Longitude.HasValue)
        {
            publication.Latitude = input.Latitude;
            publication.Longitude = input.Longitude;
        }
        else if (!input.NoLocation && localityChanged)
        {
            publication.Latitude = null;
            publication.Longitude = null;
        }
        else
        {
            publication.Latitude = input.Latitude;
            publication.Longitude = input.Longitude;
        }
        publication.HideFromMap = input.NoLocation;

        var nextMediaItems = PublicationMediaBuilder.Build(
            input.ImagesCsv,
            input.VideoUrl,
            publication.CreatedAtUtc);
        var nextMediaUrls = nextMediaItems
            .Select(x => x.Url)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var removedMediaUrls = previousMediaUrls
            .Except(nextMediaUrls, StringComparer.OrdinalIgnoreCase)
            .ToList();

        db.RemoveRange(publication.MediaItems);
        publication.MediaItems.Clear();
        publication.MediaItems.AddRange(nextMediaItems);

        db.RemoveRange(publication.FieldValues);
        publication.FieldValues.Clear();
        foreach (var fieldValue in BuildDynamicFieldValues(input, categoryFields))
        {
            publication.FieldValues.Add(fieldValue);
        }

        await db.SaveChangesAsync();
        await SafeDeleteMediaFromStorageAsync(removedMediaUrls);
        return true;
    }

    private async Task SafeDeleteMediaFromStorageAsync(IEnumerable<string> urls)
    {
        var mediaUrls = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (mediaUrls.Count == 0)
        {
            return;
        }

        try
        {
            await imageStorageService.DeletePublicObjectsAsync(mediaUrls);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudieron borrar algunos medios de R2.");
        }
    }

    private IQueryable<Publication> BuildActiveSearchQuery(PublicationGroup? group, string? query, PublicationSearchFilters? filters = null, bool includeDebug = false)
    {
        var now = DateTime.UtcNow;
        var items = ApplyDebugVisibility(db.Publications
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.MediaItems)
            .Where(x => x.IsActive && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now)), includeDebug);

        if (group is not null)
        {
            items = items.Where(x => x.Group == group.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            items = items.Where(x =>
                EF.Functions.Like(x.Title, term) ||
                EF.Functions.Like(x.ShortDescription, term) ||
                EF.Functions.Like(x.Locality, term) ||
                (x.Category != null && EF.Functions.Like(x.Category.Name, term)) ||
                x.FieldValues.Any(v => v.ValueText != null && EF.Functions.Like(v.ValueText, term)));
        }

        items = ApplySearchFilters(items, filters);

        return items
            .OrderByDescending(x => x.Featured)
            .ThenByDescending(x => x.CreatedAtUtc);
    }

    private IQueryable<Publication> BuildOrderedActiveSearchQuery(
        PublicationGroup? group,
        string? query,
        double? referenceLatitude,
        double? referenceLongitude,
        PublicationSearchFilters? filters = null,
        bool includeDebug = false)
    {
        var items = BuildActiveSearchQuery(group, query, filters, includeDebug);
        if (referenceLatitude is null || referenceLongitude is null)
        {
            return items;
        }

        var latitude = referenceLatitude.Value;
        var longitude = referenceLongitude.Value;
        items = ApplyRadiusFilter(items, latitude, longitude, filters);

        return items
            .OrderBy(x => x.Latitude.HasValue && x.Longitude.HasValue ? 0 : 1)
            .ThenBy(x => x.Latitude.HasValue && x.Longitude.HasValue
                ? ((x.Latitude ?? 0d) - latitude) * ((x.Latitude ?? 0d) - latitude)
                    + ((x.Longitude ?? 0d) - longitude) * ((x.Longitude ?? 0d) - longitude)
                : double.MaxValue)
            .ThenByDescending(x => x.Featured)
            .ThenByDescending(x => x.CreatedAtUtc);
    }

    private static IQueryable<Publication> ApplyRadiusFilter(
        IQueryable<Publication> items,
        double referenceLatitude,
        double referenceLongitude,
        PublicationSearchFilters? filters)
    {
        if (filters?.RadiusKm is not int radiusKm || radiusKm <= 0)
        {
            return items;
        }

        const double kmPerDegree = 111.32d;
        var latitudeDelta = radiusKm / kmPerDegree;
        var longitudeScale = Math.Max(Math.Abs(Math.Cos(referenceLatitude * Math.PI / 180d)), 0.01d);
        var longitudeDelta = radiusKm / (kmPerDegree * longitudeScale);

        return items.Where(x =>
            x.Latitude.HasValue
            && x.Longitude.HasValue
            && Math.Abs((x.Latitude ?? 0d) - referenceLatitude) <= latitudeDelta
            && Math.Abs((x.Longitude ?? 0d) - referenceLongitude) <= longitudeDelta
            && ((((x.Latitude ?? 0d) - referenceLatitude) / latitudeDelta) * (((x.Latitude ?? 0d) - referenceLatitude) / latitudeDelta)
                + (((x.Longitude ?? 0d) - referenceLongitude) / longitudeDelta) * (((x.Longitude ?? 0d) - referenceLongitude) / longitudeDelta)) <= 1d);
    }

    private static IQueryable<Publication> ApplyDebugVisibility(IQueryable<Publication> items, bool includeDebug)
    {
        if (includeDebug)
        {
            return items;
        }

        return items.Where(x => x.UserId == null || x.User == null || !x.User.IsDebugUser);
    }

    private IQueryable<Publication> ApplySearchFilters(IQueryable<Publication> items, PublicationSearchFilters? filters)
    {
        if (filters is null)
        {
            return items;
        }

        if (filters.PriceFrom is decimal priceFrom)
        {
            items = items.Where(x => x.Price >= priceFrom);
        }

        if (!string.IsNullOrWhiteSpace(filters.Operation))
        {
            var operation = PublicationOperationTypeExtensions.ParseOrNull(filters.Operation.Trim());
            items = operation is null
                ? items.Where(_ => false)
                : items.Where(x => x.OperationType == operation.Value);
        }

        if (filters.CategoryId is int categoryId && categoryId > 0)
        {
            items = items.Where(x => x.CategoryId == categoryId);
        }

        if (filters.PriceTo is decimal priceTo)
        {
            items = items.Where(x => x.Price <= priceTo);
        }

        if (filters.North.HasValue && filters.South.HasValue && filters.East.HasValue && filters.West.HasValue)
        {
            var north = Math.Max(filters.North.Value, filters.South.Value);
            var south = Math.Min(filters.North.Value, filters.South.Value);
            var east = filters.East.Value;
            var west = filters.West.Value;

            items = items.Where(x =>
                !x.HideFromMap
                && x.Latitude.HasValue
                && x.Longitude.HasValue
                && x.Latitude.Value <= north
                && x.Latitude.Value >= south);

            items = west <= east
                ? items.Where(x => x.Longitude!.Value >= west && x.Longitude.Value <= east)
                : items.Where(x => x.Longitude!.Value >= west || x.Longitude.Value <= east);
        }

        foreach (var fieldFilter in filters.FieldFilters)
        {
            var fieldId = fieldFilter.FieldId;
            var rawValue = fieldFilter.Value?.Trim();
            if (fieldId <= 0 || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            var textTerm = $"%{rawValue}%";
            var hasNumber = decimal.TryParse(rawValue, out var numberValue);
            var hasBoolean = bool.TryParse(rawValue, out var booleanValue);

            if (hasNumber)
            {
                items = items.Where(x => x.FieldValues.Any(v =>
                    v.CategoryFieldId == fieldId &&
                    ((v.ValueText != null && EF.Functions.Like(v.ValueText, textTerm)) ||
                     (v.ValueNumber.HasValue && v.ValueNumber.Value == numberValue))));
                continue;
            }

            if (hasBoolean)
            {
                items = items.Where(x => x.FieldValues.Any(v =>
                    v.CategoryFieldId == fieldId &&
                    ((v.ValueText != null && EF.Functions.Like(v.ValueText, textTerm)) ||
                     (v.ValueBoolean.HasValue && v.ValueBoolean.Value == booleanValue))));
                continue;
            }

            items = items.Where(x => x.FieldValues.Any(v =>
                v.CategoryFieldId == fieldId &&
                v.ValueText != null &&
                EF.Functions.Like(v.ValueText, textTerm)));
        }

        return items;
    }

    private static List<PublicationFieldValue> BuildDynamicFieldValues(
        PublicationCreateRequest input,
        IReadOnlyCollection<PublicationCategoryField> definitions)
    {
        return MergeDynamicFieldInputs(input, definitions)
            .Where(x => x.FieldId > 0)
            .Select(x => new PublicationFieldValue
            {
                CategoryFieldId = x.FieldId,
                ValueText = NormalizeValueText(x.ValueText),
                ValueNumber = x.ValueNumber,
                ValueBoolean = x.ValueBoolean
            })
            .Where(HasAnyValue)
            .ToList();
    }

    private static IEnumerable<PublicationDynamicFieldInput> MergeDynamicFieldInputs(
        PublicationCreateRequest input,
        IReadOnlyCollection<PublicationCategoryField> definitions)
    {
        var values = new Dictionary<int, PublicationDynamicFieldInput>();

        foreach (var item in input.DynamicFields)
        {
            if (item.FieldId <= 0)
            {
                continue;
            }

            values[item.FieldId] = item;
        }

        foreach (var legacy in BuildLegacyDynamicFieldInputs(input, definitions))
        {
            if (!values.ContainsKey(legacy.FieldId))
            {
                values[legacy.FieldId] = legacy;
            }
        }

        return values.Values;
    }

    private static IEnumerable<PublicationDynamicFieldInput> BuildLegacyDynamicFieldInputs(
        PublicationCreateRequest input,
        IReadOnlyCollection<PublicationCategoryField> definitions)
    {
        var byName = definitions.ToDictionary(x => x.InternalName, StringComparer.OrdinalIgnoreCase);

        PublicationDynamicFieldInput? CreateText(string name, string? value)
            => TryResolveField(byName, name, out var field) && !string.IsNullOrWhiteSpace(value)
                ? new PublicationDynamicFieldInput { FieldId = field.Id, ValueText = value.Trim() }
                : null;

        PublicationDynamicFieldInput? CreateNumber(string name, decimal? value)
            => TryResolveField(byName, name, out var field) && value.HasValue
                ? new PublicationDynamicFieldInput { FieldId = field.Id, ValueNumber = value.Value }
                : null;

        PublicationDynamicFieldInput? CreateBoolean(string name, bool value)
            => TryResolveField(byName, name, out var field) && value
                ? new PublicationDynamicFieldInput { FieldId = field.Id, ValueBoolean = value }
                : null;

        var candidates = new PublicationDynamicFieldInput?[]
        {
            CreateText("zona", input.Zone),
            CreateNumber("superficie_total_m2", input.TotalAreaM2),
            CreateNumber("superficie_cubierta_m2", input.CoveredAreaM2),
            CreateText("ambientes", input.RoomsOrBedrooms),
            CreateNumber("banios", input.Bathrooms),
            CreateText("direccion", input.Address),
            CreateNumber("garage", input.GarageSpaces),
            CreateNumber("antiguedad_anios", input.AgeYears),
            CreateNumber("expensas", input.Expenses),
            CreateText("estado", input.Condition),
            CreateBoolean("apto_credito", input.MortgageEligible),
            CreateBoolean("uso_profesional", input.ProfessionalUseAllowed),
            CreateText("servicios", input.Services),
            CreateText("amenities", input.Amenities),
            CreateText("tipo_vehiculo", input.VehicleType),
            CreateText("marca", input.Brand),
            CreateText("modelo", input.Model),
            CreateNumber("anio", input.Year),
            CreateNumber("kilometros", input.Kilometers),
            CreateText("combustible", input.Fuel),
            CreateText("transmision", input.Transmission),
            CreateText("version", input.Version),
            CreateText("color", input.Color),
            CreateText("patente", input.LicensePlate),
            CreateText("motor", input.Engine),
            CreateText("traccion", input.Traction),
            CreateNumber("puertas", input.Doors),
            CreateNumber("titulares", input.OwnersCount),
            CreateBoolean("permuta", input.AcceptsTrade),
            CreateBoolean("financiacion", input.FinancingAvailable),
            CreateText("equipamiento", input.Equipment),
            CreateText("estado_general", input.GeneralCondition),
            CreateText("subcategoria", input.Subcategory),
            CreateText("estado_articulo", input.ItemCondition),
            CreateText("sku", input.Sku),
            CreateNumber("stock", input.Stock),
            CreateText("medida", input.Measure),
            CreateText("peso", input.Weight),
            CreateText("dimensiones", input.Dimensions),
            CreateText("garantia", input.Warranty),
            CreateText("envio", input.Shipping)
        };

        return candidates.Where(x => x is not null).Select(x => x!);
    }

    private static bool TryResolveField(
        IReadOnlyDictionary<string, PublicationCategoryField> byName,
        string internalName,
        out PublicationCategoryField field)
    {
        return byName.TryGetValue(internalName, out field!);
    }

    private static bool IsMissingDynamicValue(PublicationCategoryFieldDataType dataType, PublicationDynamicFieldInput? value)
    {
        if (value is null)
        {
            return true;
        }

        return dataType switch
        {
            PublicationCategoryFieldDataType.Numero => value.ValueNumber is null,
            PublicationCategoryFieldDataType.Booleano => value.ValueBoolean is null,
            _ => string.IsNullOrWhiteSpace(value.ValueText)
        };
    }

    private static bool IsValidDynamicValue(PublicationCategoryFieldDataType dataType, PublicationDynamicFieldInput value)
    {
        return dataType switch
        {
            PublicationCategoryFieldDataType.Numero => value.ValueNumber is not null,
            PublicationCategoryFieldDataType.Booleano => value.ValueBoolean is not null,
            PublicationCategoryFieldDataType.Texto or PublicationCategoryFieldDataType.Lista => !string.IsNullOrWhiteSpace(value.ValueText),
            _ => false
        };
    }

    private static bool HasAnyValue(PublicationFieldValue value)
    {
        return !string.IsNullOrWhiteSpace(value.ValueText)
            || value.ValueNumber is not null
            || value.ValueBoolean is not null;
    }

    private static string? NormalizeValueText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> SplitOperationOptions(string? optionsCsv)
    {
        return string.IsNullOrWhiteSpace(optionsCsv)
            ? []
            : optionsCsv.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

}
