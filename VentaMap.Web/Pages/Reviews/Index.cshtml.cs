using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Services;

namespace VentaMap.Pages.Reviews;

[Authorize]
public class IndexModel(ReviewService reviewService, CurrentUserAccessor currentUserAccessor) : PageModel
{
    public List<OperationReviewItem> Operations { get; private set; } = [];
    public bool ReviewsEnabled { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login");
        }

        ReviewsEnabled = await reviewService.IsEnabledAsync();
        Operations = await reviewService.GetOperationsForUserAsync(
            userId,
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty);
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(
        int operationId,
        int? stars,
        string? comment,
        bool declinedToReview = false)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login");
        }

        var result = await reviewService.SubmitReviewAsync(
            operationId,
            userId,
            stars,
            comment,
            declinedToReview);
        if (!result.Success)
        {
            ErrorMessage = result.Error;
        }
        else
        {
            StatusMessage = declinedToReview
                ? "Registramos que no deseas dejar una reseña."
                : "Tu reseña fue guardada y permanecera oculta durante el periodo de espera.";
        }

        return RedirectToPage();
    }
}
