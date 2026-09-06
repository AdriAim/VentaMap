using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class ReportService(
    VentagramDbContext db,
    IEmailSender emailSender,
    ILogger<ReportService> logger)
{
    private const int WarningThreshold = 5;
    private const int TrashThreshold = 10;
    private const int BadReportsThreshold = 5;

    public async Task<(bool Success, int StatusCode, string Message)> CreateAsync(int publicationId, int reporterUserId, int reasonId, string? comment)
    {
        var publication = await db.Publications
            .Include(x => x.User)
            .Include(x => x.Reports)
            .FirstOrDefaultAsync(x => x.Id == publicationId);

        if (publication is null)
        {
            return (false, 404, "El anuncio indicado no existe.");
        }

        if (publication.UserId == reporterUserId)
        {
            return (false, 400, "No puedes denunciar tu propio anuncio.");
        }

        var reporter = await db.Users.FirstOrDefaultAsync(x => x.Id == reporterUserId);
        if (reporter is null)
        {
            return (false, 401, "No se encontró el usuario autenticado.");
        }

        if (!reporter.CanReport)
        {
            return (false, 403, "No puedes denunciar nuevos anuncios porque acumulaste 5 denuncias incorrectas.");
        }

        var createdPublications = await db.Publications.CountAsync(x => x.UserId == reporterUserId);
        if (createdPublications < 2)
        {
            return (false, 403, "Para denunciar debes tener al menos 2 anuncios realizados.");
        }

        var alreadyReported = await db.PublicationReports.AnyAsync(x =>
            x.PublicationId == publicationId &&
            x.ReporterUserId == reporterUserId);

        if (alreadyReported)
        {
            return (false, 409, "Ya denunciaste este anuncio.");
        }

        db.PublicationReports.Add(new PublicationReport
        {
            PublicationId = publicationId,
            ReporterUserId = reporterUserId,
            ReasonId = reasonId,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });

        if (publication.ModerationStatus == "None")
        {
            publication.ModerationStatus = "Reported";
        }

        await db.SaveChangesAsync();
        await ApplyAutomaticModerationAsync(publicationId);

        return (true, 200, "La denuncia fue enviada para revisión.");
    }

    public async Task<(bool Success, string Message)> RestorePublicationAsync(int publicationId, int adminUserId)
    {
        var publication = await db.Publications
            .Include(x => x.User)
            .Include(x => x.Reports.Where(r => r.ReviewStatus == "Pending"))
            .FirstOrDefaultAsync(x => x.Id == publicationId);

        if (publication is null)
        {
            return (false, "El anuncio no existe.");
        }

        publication.IsActive = true;
        publication.Status = "Activa";
        publication.ModerationStatus = "Restored";
        publication.TrashedAtUtc = null;

        if (publication.User is not null)
        {
            publication.User.CanPublish = true;
        }

        foreach (var report in publication.Reports.Where(r => r.ReviewStatus == "Pending"))
        {
            report.ReviewStatus = "Rejected";
            report.ReviewedAtUtc = DateTime.UtcNow;
            report.ReviewedByUserId = adminUserId;
            report.CountsTowardThreshold = false;
        }

        await db.SaveChangesAsync();
        await UpdateReporterBlockingAsync();

        return (true, "El anuncio fue restaurado y el usuario volvió a quedar habilitado para publicar.");
    }

    public async Task<(bool Success, string Message)> ConfirmTrashAsync(int publicationId, int adminUserId)
    {
        var publication = await db.Publications
            .Include(x => x.User)
            .Include(x => x.Reports.Where(r => r.ReviewStatus == "Pending"))
            .FirstOrDefaultAsync(x => x.Id == publicationId);

        if (publication is null)
        {
            return (false, "El anuncio no existe.");
        }

        publication.IsActive = false;
        publication.Status = "En papelera";
        publication.ModerationStatus = "Confirmed";
        publication.TrashedAtUtc ??= DateTime.UtcNow;

        if (publication.User is not null)
        {
            publication.User.CanPublish = false;
        }

        foreach (var report in publication.Reports.Where(r => r.ReviewStatus == "Pending"))
        {
            report.ReviewStatus = "Confirmed";
            report.ReviewedAtUtc = DateTime.UtcNow;
            report.ReviewedByUserId = adminUserId;
        }

        await db.SaveChangesAsync();
        return (true, "El anuncio quedó confirmado en papelera y el usuario sigue bloqueado para publicar.");
    }

    public async Task ApplyAutomaticModerationAsync(int publicationId)
    {
        var publication = await db.Publications
            .Include(x => x.User)
            .Include(x => x.Reports.Where(r => r.CountsTowardThreshold && r.ReviewStatus == "Pending"))
                .ThenInclude(x => x.ReporterUser)
            .FirstOrDefaultAsync(x => x.Id == publicationId);

        if (publication is null)
        {
            return;
        }

        var distinctReportersCount = publication.Reports
            .Where(r => r.CountsTowardThreshold && r.ReviewStatus == "Pending")
            .Select(r => r.ReporterUserId)
            .Distinct()
            .Count();

        if (distinctReportersCount >= WarningThreshold && publication.ReportWarningSentAtUtc is null)
        {
            publication.ReportWarningSentAtUtc = DateTime.UtcNow;
            publication.ModerationStatus = "Warned";
            await TrySendWarningEmailAsync(publication);
        }

        if (distinctReportersCount >= TrashThreshold)
        {
            publication.IsActive = false;
            publication.Status = "En papelera";
            publication.ModerationStatus = "PendingReview";
            publication.TrashedAtUtc ??= DateTime.UtcNow;

            if (publication.User is not null)
            {
                publication.User.CanPublish = false;
            }

            if (publication.ReportTrashSentAtUtc is null)
            {
                publication.ReportTrashSentAtUtc = DateTime.UtcNow;
                await TrySendTrashEmailAsync(publication);
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task UpdateReporterBlockingAsync()
    {
        var rejectedCounts = await db.PublicationReports
            .Where(x => x.ReviewStatus == "Rejected")
            .GroupBy(x => x.ReporterUserId)
            .Select(group => new
            {
                ReporterUserId = group.Key,
                Count = group.Count()
            })
            .ToListAsync();

        var users = await db.Users.ToListAsync();
        foreach (var user in users)
        {
            var rejectedCount = rejectedCounts.FirstOrDefault(x => x.ReporterUserId == user.Id)?.Count ?? 0;
            user.CanReport = rejectedCount < BadReportsThreshold;
        }

        await db.SaveChangesAsync();
    }

    public async Task<int> CountRejectedReportsByUserAsync(int userId)
    {
        return await db.PublicationReports.CountAsync(x => x.ReporterUserId == userId && x.ReviewStatus == "Rejected");
    }

    private async Task TrySendWarningEmailAsync(Publication publication)
    {
        var email = publication.ContactEmail ?? publication.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        const string subject = "Tu anuncio recibió denuncias";
        var publicationLabel = GetPublicationEmailLabel(publication);
        var html = $"""
            <p>Tu anuncio <strong>{System.Net.WebUtility.HtmlEncode(publicationLabel)}</strong> recibió varias denuncias de otros usuarios.</p>
            <p>Te recomendamos revisar la información, imágenes y datos publicados para verificar que sean correctos y cumplan con las normas del sitio.</p>
            <p>Por el momento, el anuncio continúa visible.</p>
            """;
        var text = $"""
            Tu anuncio "{publicationLabel}" recibió varias denuncias de otros usuarios.

            Revisa la información, imágenes y datos publicados para verificar que sean correctos y cumplan con las normas del sitio.

            Por el momento, el anuncio continúa visible.
            """;

        await SafeSendEmailAsync(email, subject, html, text);
    }

    private async Task TrySendTrashEmailAsync(Publication publication)
    {
        var email = publication.ContactEmail ?? publication.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        const string subject = "Tu anuncio pasó a revisión";
        var publicationLabel = GetPublicationEmailLabel(publication);
        var html = $"""
            <p>Tu anuncio <strong>{System.Net.WebUtility.HtmlEncode(publicationLabel)}</strong> fue enviado a papelera para revisión administrativa luego de acumular 10 denuncias de 10 usuarios diferentes.</p>
            <p>Mientras el caso se encuentra en revisión, no podrás publicar nuevos anuncios hasta nuevo aviso.</p>
            <p>Un administrador del sitio evaluará la situación y podrá restituir el anuncio y rehabilitar tu cuenta si corresponde.</p>
            """;
        var text = $"""
            Tu anuncio "{publicationLabel}" fue enviado a papelera para revisión administrativa luego de acumular 10 denuncias de 10 usuarios diferentes.

            Mientras el caso se encuentra en revisión, no podrás publicar nuevos anuncios hasta nuevo aviso.

            Un administrador del sitio evaluará la situación y podrá restituir el anuncio y rehabilitar tu cuenta si corresponde.
            """;

        await SafeSendEmailAsync(email, subject, html, text);
    }

    private async Task SafeSendEmailAsync(string email, string subject, string htmlBody, string textBody)
    {
        try
        {
            await emailSender.SendAsync(email, subject, htmlBody, textBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo enviar el correo de moderación a {Email}.", email);
        }
    }

    private static string GetPublicationEmailLabel(Publication publication)
    {
        return string.IsNullOrWhiteSpace(publication.ShortDescription)
            ? publication.Title
            : publication.ShortDescription.Trim();
    }
}
