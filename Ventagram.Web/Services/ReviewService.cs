using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class ReviewService(
    VentagramDbContext db,
    VentagramParameterService parameters,
    IEmailSender emailSender,
    ILogger<ReviewService> logger)
{
    public async Task<bool> IsEnabledAsync() =>
        await parameters.GetBoolAsync(VentagramParameterService.ReviewsEnabled);

    public async Task<(bool Success, string? Error, VerifiedOperation? Operation)> CreateOperationAsync(
        int publicationId,
        int advertiserUserId,
        string counterpartyKind,
        string counterpartyEmail,
        string baseUrl)
    {
        if (!await IsEnabledAsync())
        {
            return (false, "El sistema de reseñas esta desactivado.", null);
        }

        var normalizedEmail = counterpartyEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            return (false, "Ingresa un email valido para la otra persona.", null);
        }

        var publication = await db.Publications
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == publicationId && x.UserId == advertiserUserId && x.IsActive);
        if (publication is null)
        {
            return (false, "No se encontro el anuncio activo.", null);
        }

        var counterparty = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);
        if (counterparty?.Id == advertiserUserId)
        {
            return (false, "No puedes registrarte como contraparte de tu propio anuncio.", null);
        }

        var expectsRegistered = string.Equals(counterpartyKind, CounterpartyKinds.Registered, StringComparison.Ordinal);
        if (expectsRegistered && counterparty is null)
        {
            return (false, "No existe un usuario registrado con ese email. Elige persona no registrada.", null);
        }

        if (await db.VerifiedOperations.AnyAsync(x =>
                x.PublicationId == publicationId
                && x.Status != VerifiedOperationStatuses.Rejected))
        {
            return (false, "Este anuncio ya tiene una operacion registrada.", null);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var operation = new VerifiedOperation
        {
            PublicationId = publication.Id,
            OperationType = publication.OperationType,
            AdvertiserUserId = advertiserUserId,
            CounterpartyUserId = counterparty?.Id,
            CounterpartyEmail = normalizedEmail,
            CounterpartyKind = counterparty is null ? CounterpartyKinds.External : CounterpartyKinds.Registered,
            Status = VerifiedOperationStatuses.PendingCounterpartyConfirmation,
            ResponseTokenHash = HashToken(token),
            ResponseTokenExpiresAtUtc = now.AddDays(30),
            CreatedAtUtc = now
        };

        publication.IsActive = false;
        publication.Status = "Baja solicitada";
        publication.DeactivationReason = "Ya se vendio / alquilo";
        publication.DeactivationComment = $"Contraparte: {normalizedEmail}";
        publication.DeactivatedAtUtc = now;
        db.VerifiedOperations.Add(operation);
        await db.SaveChangesAsync();

        if (await parameters.GetBoolAsync(VentagramParameterService.ReviewEmailsEnabled))
        {
            var responseUrl = $"{baseUrl.TrimEnd('/')}/Reviews/Respond?token={Uri.EscapeDataString(token)}";
            var counterpartyName = counterparty?.Name ?? "usuario/a";
            var advertiserName = publication.User?.Name ?? publication.ContactName;
            var safeTitle = WebUtility.HtmlEncode(publication.Title);
            var safeCounterpartyName = WebUtility.HtmlEncode(counterpartyName);
            var safeAdvertiserName = WebUtility.HtmlEncode(advertiserName);
            var safeUrl = WebUtility.HtmlEncode(responseUrl);
            var subject = "Confirma una operacion en Ventagram";
            var html = $"""
                <p>Hola {safeCounterpartyName},</p>
                <p>{safeAdvertiserName} informo que concreto contigo una operacion relacionada con <strong>{safeTitle}</strong>.</p>
                <p>Confirma si la operacion existio. Confirmarla no significa que estes conforme: tambien podras indicar un problema y dejar una reseña.</p>
                <p><a href="{safeUrl}">Revisar y responder la operacion</a></p>
                <p>El enlace vence en 30 dias.</p>
                """;
            var text = $"{advertiserName} informo que concreto contigo una operacion relacionada con '{publication.Title}'. Revisa y responde en: {responseUrl}";

            try
            {
                await emailSender.SendAsync(normalizedEmail, subject, html, text);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo enviar el aviso de operacion {OperationId}", operation.Id);
            }
        }

        return (true, null, operation);
    }

    public async Task<VerifiedOperation?> GetByResponseTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = HashToken(token);
        return await OperationQuery()
            .FirstOrDefaultAsync(x => x.ResponseTokenHash == hash);
    }

    public async Task<(bool Success, string? Error, VerifiedOperation? Operation)> RespondAsync(
        string token,
        string response,
        int? currentUserId,
        string? currentUserEmail)
    {
        var hash = HashToken(token);
        var operation = await db.VerifiedOperations
            .Include(x => x.Publication)
            .FirstOrDefaultAsync(x => x.ResponseTokenHash == hash);
        if (operation is null || operation.ResponseTokenExpiresAtUtc < DateTime.UtcNow)
        {
            return (false, "El enlace no existe o ha vencido.", null);
        }

        if (operation.Status != VerifiedOperationStatuses.PendingCounterpartyConfirmation)
        {
            return (false, "La operacion ya fue respondida.", operation);
        }

        if (operation.CounterpartyUserId.HasValue && operation.CounterpartyUserId != currentUserId)
        {
            return (false, "Inicia sesion con la cuenta vinculada a la operacion para responder.", operation);
        }

        if (!operation.CounterpartyUserId.HasValue
            && currentUserId.HasValue
            && string.Equals(operation.CounterpartyEmail, currentUserEmail, StringComparison.OrdinalIgnoreCase))
        {
            operation.CounterpartyUserId = currentUserId;
            operation.CounterpartyKind = CounterpartyKinds.Registered;
        }

        response = response.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        operation.CounterpartyRespondedAtUtc = now;
        if (string.Equals(response, "reject", StringComparison.OrdinalIgnoreCase))
        {
            operation.Status = VerifiedOperationStatuses.Rejected;
        }
        else if (response is "confirm" or "problem")
        {
            operation.Status = VerifiedOperationStatuses.Confirmed;
            operation.ConfirmedAtUtc = now;
            operation.CounterpartyReportedProblem = response == "problem";
        }
        else
        {
            return (false, "Selecciona una respuesta valida.", operation);
        }

        await db.SaveChangesAsync();
        return (true, null, operation);
    }

    public async Task LinkExternalOperationsAsync(int userId, string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var operations = await db.VerifiedOperations
            .Where(x => x.CounterpartyUserId == null && x.CounterpartyEmail == normalizedEmail)
            .ToListAsync();
        if (operations.Count == 0)
        {
            return;
        }

        foreach (var operation in operations)
        {
            operation.CounterpartyUserId = userId;
            operation.CounterpartyKind = CounterpartyKinds.Registered;
        }
        await db.SaveChangesAsync();
    }

    public async Task<List<OperationReviewItem>> GetOperationsForUserAsync(int userId, string email)
    {
        await LinkExternalOperationsAsync(userId, email);
        var operations = await OperationQuery()
            .Where(x => x.AdvertiserUserId == userId || x.CounterpartyUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        return operations.Select(operation =>
        {
            var isAdvertiser = operation.AdvertiserUserId == userId;
            var otherUser = isAdvertiser ? operation.CounterpartyUser : operation.AdvertiserUser;
            var ownReview = operation.Reviews.FirstOrDefault(x => x.ReviewerUserId == userId);
            return new OperationReviewItem(
                operation,
                isAdvertiser,
                otherUser,
                ownReview,
                operation.Status == VerifiedOperationStatuses.Confirmed && otherUser is not null && ownReview is null);
        }).ToList();
    }

    public async Task<(bool Success, string? Error)> SubmitReviewAsync(
        int operationId,
        int reviewerUserId,
        int? stars,
        string? comment,
        bool declined)
    {
        if (!await IsEnabledAsync())
        {
            return (false, "El sistema de reseñas esta desactivado.");
        }

        var operation = await db.VerifiedOperations
            .Include(x => x.Reviews)
            .FirstOrDefaultAsync(x => x.Id == operationId);
        if (operation is null || operation.Status != VerifiedOperationStatuses.Confirmed)
        {
            return (false, "La operacion no esta confirmada.");
        }

        var reviewerIsAdvertiser = operation.AdvertiserUserId == reviewerUserId;
        var reviewerIsCounterparty = operation.CounterpartyUserId == reviewerUserId;
        if (!reviewerIsAdvertiser && !reviewerIsCounterparty)
        {
            return (false, "No participaste de esta operacion.");
        }

        var reviewedUserId = reviewerIsAdvertiser ? operation.CounterpartyUserId : operation.AdvertiserUserId;
        if (!reviewedUserId.HasValue)
        {
            return (false, "La otra persona debe estar registrada para recibir una reseña.");
        }

        var reviewedRole = reviewerIsAdvertiser ? ReviewRoles.Counterparty : ReviewRoles.Advertiser;
        var roleEnabledKey = reviewerIsAdvertiser
            ? VentagramParameterService.CounterpartyReviewsEnabled
            : VentagramParameterService.AdvertiserReviewsEnabled;
        if (!await parameters.GetBoolAsync(roleEnabledKey))
        {
            return (false, "Este tipo de reseña esta desactivado.");
        }

        if (operation.Reviews.Any(x => x.ReviewerUserId == reviewerUserId))
        {
            return (false, "Ya respondiste esta reseña.");
        }

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (!declined && (!stars.HasValue || stars.Value < 1 || stars.Value > 5 || normalizedComment is null))
        {
            return (false, "Selecciona entre 1 y 5 estrellas y escribe un comentario.");
        }

        var now = DateTime.UtcNow;
        var review = new OperationReview
        {
            VerifiedOperationId = operation.Id,
            ReviewerUserId = reviewerUserId,
            ReviewedUserId = reviewedUserId.Value,
            ReviewedRole = reviewedRole,
            Stars = declined ? null : (byte)stars!.Value,
            Comment = declined ? null : normalizedComment,
            DeclinedToRate = declined,
            SubmittedAtUtc = now
        };
        operation.Reviews.Add(review);
        db.OperationReviews.Add(review);

        if (operation.Reviews.Select(x => x.ReviewerUserId).Distinct().Count() >= 2)
        {
            var delayDays = await parameters.GetIntAsync(
                VentagramParameterService.ReviewPublicationDelayDays, 7, 0, 90);
            var publishAt = operation.Reviews.Max(x => x.SubmittedAtUtc).AddDays(delayDays);
            foreach (var submittedReview in operation.Reviews)
            {
                submittedReview.PublishAtUtc = publishAt;
            }
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<UserReviewSummary?> GetUserSummaryAsync(int userId, string role)
    {
        if (!await IsEnabledAsync()
            || !await parameters.GetBoolAsync(VentagramParameterService.DisplayExistingReviewsEnabled))
        {
            return null;
        }

        var roleKey = role == ReviewRoles.Advertiser
            ? VentagramParameterService.AdvertiserReviewsEnabled
            : VentagramParameterService.CounterpartyReviewsEnabled;
        if (!await parameters.GetBoolAsync(roleKey))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var responseDays = await parameters.GetIntAsync(
            VentagramParameterService.ReviewResponseDeadlineDays, 14, 1, 90);
        var delayDays = await parameters.GetIntAsync(
            VentagramParameterService.ReviewPublicationDelayDays, 7, 0, 90);
        var candidates = await db.OperationReviews
            .AsNoTracking()
            .Include(x => x.VerifiedOperation)
            .Where(x => x.ReviewedUserId == userId
                && x.ReviewedRole == role
                && x.Stars != null
                && x.ModerationStatus == "Published")
            .ToListAsync();

        var visible = candidates
            .Where(x => GetEffectivePublishAt(x, responseDays, delayDays) <= now)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .ToList();
        if (visible.Count == 0)
        {
            return new UserReviewSummary(0, 0, []);
        }

        var comments = visible
            .Where(x => !string.IsNullOrWhiteSpace(x.Comment))
            .Take(10)
            .Select(x => new ReviewComment(x.Stars!.Value, x.Comment!, x.SubmittedAtUtc))
            .ToList();
        return new UserReviewSummary(
            Math.Round(visible.Average(x => x.Stars!.Value), 1),
            visible.Count,
            comments);
    }

    private IQueryable<VerifiedOperation> OperationQuery() => db.VerifiedOperations
        .Include(x => x.Publication)
        .Include(x => x.AdvertiserUser)
        .Include(x => x.CounterpartyUser)
        .Include(x => x.Reviews);

    private static DateTime GetEffectivePublishAt(OperationReview review, int responseDays, int delayDays)
    {
        if (review.PublishAtUtc.HasValue)
        {
            return review.PublishAtUtc.Value;
        }

        var confirmedAt = review.VerifiedOperation?.ConfirmedAtUtc ?? review.SubmittedAtUtc;
        return confirmedAt.AddDays(responseDays + delayDays);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes);
    }
}

public record OperationReviewItem(
    VerifiedOperation Operation,
    bool CurrentUserIsAdvertiser,
    ApplicationUser? OtherUser,
    OperationReview? OwnReview,
    bool CanReview);

public record UserReviewSummary(double Average, int Count, IReadOnlyList<ReviewComment> RecentComments);
public record ReviewComment(byte Stars, string Comment, DateTime SubmittedAtUtc);
