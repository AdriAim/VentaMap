using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Pages.Reviews;

public class RespondModel(ReviewService reviewService, CurrentUserAccessor currentUserAccessor) : PageModel
{
    public VerifiedOperation? Operation { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public bool RequiresLogin { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        Token = token;
        Operation = await reviewService.GetByResponseTokenAsync(token);
        if (Operation is null)
        {
            return Page();
        }

        RequiresLogin = Operation.CounterpartyUserId.HasValue
            && Operation.CounterpartyUserId != currentUserAccessor.UserId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string token, string response)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var result = await reviewService.RespondAsync(token, response, currentUserAccessor.UserId, email);
        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return RedirectToPage(new { token });
        }

        StatusMessage = response == "reject"
            ? "Informamos que no reconoces esta operacion."
            : "La operacion quedo confirmada. Ya puedes reseñarla desde Mis operaciones y reseñas.";
        return RedirectToPage(new { token });
    }
}
