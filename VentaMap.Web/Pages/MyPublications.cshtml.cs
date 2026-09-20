using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Models;
using VentaMap.Services;
using VentaMap.Data;
using VentaMap.ViewModels;

namespace VentaMap.Pages;

[Authorize]
public class MyPublicationsModel(
    PublicationService publicationService,
    CurrentUserAccessor currentUserAccessor,
    VentaMapDbContext db,
    ReviewService reviewService,
    BillingService billingService,
    PricingService pricingService) : PageModel
{
    public static readonly IReadOnlyList<string> DeactivationReasons =
    [
        "Operacion concretada",
        "Reservado",
        "Otro motivo"
    ];

    public List<MyPublicationAdminItemViewModel> Publications { get; private set; } = [];
    public List<PublicationReportReason> ReportReasons { get; private set; } = [];
    public bool PublishingBlocked { get; private set; }
    public bool ReportingBlocked { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool IsCompany { get; private set; }
    public string? CompanyName { get; private set; }
    public string? CompanyPublicUrl { get; private set; }
    public string? CompanyHeroBackgroundUrl { get; private set; }
    public bool ReviewsEnabled { get; private set; }
    public bool BillingEnabled { get; private set; }
    public int PendingChargeCount { get; private set; }
    public Dictionary<int, int> PendingChargeIdsByPublicationId { get; private set; } = [];
    public Dictionary<int, decimal> PendingChargeAmountsByPublicationId { get; private set; } = [];
    public Dictionary<int, List<BillingCharge>> PaymentHistoryByPublicationId { get; private set; } = [];
    public string PersonPublicationAmountText { get; private set; } = string.Empty;
    public string CompanyMonthlyAmountText { get; private set; } = string.Empty;

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        PublishingBlocked = !user.CanPublish;
        ReportingBlocked = !user.CanReport;
        IsAdmin = user.IsAdmin;
        IsCompany = user.IsCompany;
        CompanyName = user.CompanyName;
        CompanyHeroBackgroundUrl = user.CompanyHeroBackgroundUrl;
        CompanyPublicUrl = user.IsCompany && !string.IsNullOrWhiteSpace(user.CompanySlug)
            ? $"/{user.CompanySlug}"
            : null;
        ReviewsEnabled = await reviewService.IsEnabledAsync();
        BillingEnabled = user.IsCompany
            || user.IsBillingExempt == 2
            || await db.VentaMapParameters.AnyAsync(x => x.Key == VentaMapParameterService.PaidSiteEnabled && x.Value == "true");
        PersonPublicationAmountText = PricingService.FormatAmount(await pricingService.GetCurrentPersonPublicationAmountAsync());
        CompanyMonthlyAmountText = PricingService.FormatAmount(await pricingService.GetCurrentCompanyMonthlyAmountAsync());
        if (BillingEnabled)
        {
            await billingService.EnsureCompanyCurrentMonthChargeAsync(user);
            var charges = await billingService.GetChargesAsync(userId);
            PendingChargeCount = charges.Count(x => x.Status == "Pending");
            if (!user.IsCompany)
            {
                PendingChargeIdsByPublicationId = charges
                    .Where(x => x.Status == "Pending" && x.PublicationId.HasValue)
                    .GroupBy(x => x.PublicationId!.Value)
                    .ToDictionary(x => x.Key, x => x.OrderByDescending(charge => charge.CreatedAtUtc).First().Id);
                PendingChargeAmountsByPublicationId = charges
                    .Where(x => x.Status == "Pending" && x.PublicationId.HasValue)
                    .GroupBy(x => x.PublicationId!.Value)
                    .ToDictionary(x => x.Key, x => x.OrderByDescending(charge => charge.CreatedAtUtc).First().Amount);
                PaymentHistoryByPublicationId = charges
                    .Where(x => x.PublicationId.HasValue && x.Type == BillingService.PersonPublicationType && x.PaidAtUtc.HasValue && x.Status is "Paid" or "Used")
                    .GroupBy(x => x.PublicationId!.Value)
                    .ToDictionary(x => x.Key, x => x.OrderByDescending(charge => charge.PaidAtUtc).ToList());
            }
        }
        Publications = await publicationService.GetOwnedPublicationsAsync(userId);
        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(
        int id,
        string reason,
        string? comment,
        string? counterpartyKind,
        string? counterpartyEmail)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || !DeactivationReasons.Contains(normalizedReason, StringComparer.Ordinal))
        {
            ErrorMessage = "Selecciona un motivo para dar de baja el anuncio.";
            return RedirectToPage();
        }

        if (string.Equals(normalizedReason, "Otro motivo", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(comment))
        {
            ErrorMessage = "Escribe el motivo antes de dar de baja el anuncio.";
            return RedirectToPage();
        }

        if (string.Equals(normalizedReason, "Operacion concretada", StringComparison.Ordinal)
            && await reviewService.IsEnabledAsync())
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var result = await reviewService.CreateOperationAsync(
                id,
                userId,
                counterpartyKind ?? CounterpartyKinds.External,
                counterpartyEmail ?? string.Empty,
                baseUrl);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return RedirectToPage();
            }

            SuccessMessage = "El anuncio fue dado de baja y enviamos un email a la otra persona para confirmar la operacion.";
            return RedirectToPage();
        }

        var success = await publicationService.DeactivateOwnedAsync(id, userId, normalizedReason, comment);
        if (success)
        {
            SuccessMessage = "El anuncio fue dado de baja.";
        }
        else
        {
            ErrorMessage = "No se pudo dar de baja el anuncio indicado.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRepublishAsync(int id)
    {
        var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal);
        if (currentUserAccessor.UserId is not int userId)
        {
            if (isAjax) return StatusCode(401, new { message = "Tu sesión venció. Ingresá nuevamente para republicar." });
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            if (isAjax) return StatusCode(401, new { message = "No se encontró tu usuario." });
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        if (!user.CanPublish)
        {
            if (isAjax) return StatusCode(403, new { message = "No puedes republicar anuncios hasta que un administrador revise tu cuenta." });
            ErrorMessage = "No puedes republicar anuncios hasta que un administrador revise tu cuenta.";
            return RedirectToPage();
        }

        if (await billingService.IsPublishingBlockedAsync(user))
        {
            const string billingMessage = "Tenés un pago mensual pendiente vencido. Regularizalo desde Pagos mensuales para republicar anuncios.";
            if (isAjax) return StatusCode(403, new { message = billingMessage, billingUrl = "/Account/Billing" });
            ErrorMessage = billingMessage;
            return RedirectToPage();
        }

        var publication = await publicationService.GetOwnedByIdAsync(id, userId);
        if (publication is null)
        {
            if (isAjax) return BadRequest(new { message = "No se encontró el anuncio indicado." });
            ErrorMessage = "No se encontró el anuncio indicado.";
            return RedirectToPage();
        }

        if (await billingService.HasReachedCompanyPublicationLimitAsync(user))
        {
            const string limitMessage = "Tu cuenta empresa ya tiene 50 anuncios activos simultáneos. Da de baja uno para republicar este anuncio.";
            if (isAjax) return StatusCode(403, new { message = limitMessage });
            ErrorMessage = limitMessage;
            return RedirectToPage();
        }

        var needsPersonPack = await billingService.IsPersonRepublishPackRequiredAsync(user, publication);
        var charge = await billingService.RequirePersonRepublishPackAsync(user, publication);
        if (charge is not null)
        {
            var amountText = PricingService.FormatAmount(await pricingService.GetCurrentPersonPublicationAmountAsync());
            if (isAjax) return StatusCode(402, new { message = $"La republicación requiere el anuncio completo de {amountText}. Podés pagarlo desde Mis anuncios.", billingUrl = "/MisAnuncios?status=pending", chargeId = charge.Id });
            ErrorMessage = $"La republicación requiere el anuncio completo de {amountText}. Podés pagarlo desde Mis anuncios.";
            return RedirectToPage();
        }

        var success = await publicationService.RepublishOwnedAsync(id, userId);
        if (success)
        {
            if (needsPersonPack) await billingService.ConsumePaidPersonPublicationPackAsync(userId);
            if (isAjax) return new JsonResult(new
            {
                message = "El anuncio fue republicado por 30 días más.",
                detailsUrl = Url.Page("/Publications/Details", new { id })
            });
            SuccessMessage = "El anuncio fue republicado por 30 dias mas.";
        }
        else
        {
            if (isAjax) return BadRequest(new { message = "No se pudo republicar el anuncio indicado." });
            ErrorMessage = "No se pudo republicar el anuncio indicado.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeletePermanentAsync(int id)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        var success = await publicationService.DeleteOwnedPermanentlyAsync(id, userId);
        if (success)
        {
            SuccessMessage = "El anuncio fue eliminado de Mis anuncios y sus archivos multimedia fueron borrados.";
        }
        else
        {
            ErrorMessage = "No se pudo eliminar el anuncio indicado.";
        }

        return RedirectToPage();
    }
}
