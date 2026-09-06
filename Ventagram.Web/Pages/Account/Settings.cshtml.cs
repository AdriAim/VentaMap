using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Services;

namespace Ventagram.Pages.Account;

[Authorize]
public class SettingsModel(
    VentagramDbContext db,
    CurrentUserAccessor currentUserAccessor,
    CloudflareR2ImageStorageService imageStorageService,
    ArgentineLocalityLookupService localityLookupService,
    NavigationLocalityService navigationLocalityService,
    PublicationGroupPreferenceService publicationGroupPreferenceService,
    PublicationGroupTypeService publicationGroupTypeService) : PageModel
{
    private const string PhoneField = $"{nameof(Input)}.{nameof(InputModel.Phone)}";
    private const string LocalityField = $"{nameof(Input)}.{nameof(InputModel.ArgentineLocalityId)}";
    private const string CompanyLogoField = nameof(CompanyLogo);
    private const string CompanyHeroBackgroundField = nameof(CompanyHeroBackground);

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? CompanyLogo { get; set; }

    [BindProperty]
    public IFormFile? CompanyHeroBackground { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public bool IsCompany { get; private set; }

    public string? CompanyName { get; private set; }

    public string? CompanyPublicUrl { get; private set; }

    public string? CurrentCompanyLogoUrl { get; private set; }

    public string? CurrentCompanyHeroBackgroundUrl { get; private set; }

    public IReadOnlyList<Ventagram.Models.PublicationGroupType> HeaderGroupOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadHeaderGroupOptionsAsync();

        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        Input.ArgentineLocalityId = user.ArgentineLocalityId;
        Input.ArgentineLocalityLabel = user.ArgentineLocalityId.HasValue
            ? await db.ArgentineLocalities
                .AsNoTracking()
                .Where(x => x.Id == user.ArgentineLocalityId.Value)
                .Select(x => x.Locality + ", " + x.Province)
                .FirstOrDefaultAsync() ?? string.Empty
            : string.Empty;
        if (!Input.ArgentineLocalityId.HasValue)
        {
            var navigationLocality = await navigationLocalityService.GetEffectiveLocalityAsync(HttpContext);
            Input.ArgentineLocalityId = navigationLocality?.ArgentineLocalityId;
            Input.ArgentineLocalityLabel = navigationLocality?.DisplayLabel ?? string.Empty;
        }
        Input.Phone = user.Phone;
        Input.PhoneCountry = user.Phone.StartsWith("+54 9 ", StringComparison.Ordinal) ? "AR" : "INT";
        Input.PublishEmail = user.RespondsEmails;
        Input.AcceptsCalls = user.AcceptsCalls;
        Input.RespondsWhatsApp = user.RespondsWhatsApp;
        Input.AllowSiteChat = user.AllowsSiteChat;
        Input.MessageBubbleColor = NormalizeMessageBubbleColor(user.MessageBubbleColor);
        Input.CompanyTagline = user.CompanyTagline ?? string.Empty;
        Input.HeaderPublicationGroups = (await publicationGroupPreferenceService.GetHeaderGroupsAsync(HttpContext))
            .Select(x => x.Name).ToList();
        LoadCompanyState(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadHeaderGroupOptionsAsync();

        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        if (!await TryResolveLocalityAsync())
        {
            LoadCompanyState(await db.Users.FirstOrDefaultAsync(x => x.Id == userId) ?? new Ventagram.Models.ApplicationUser());
            return Page();
        }

        if (Input.PhoneCountry == "AR" && !Regex.IsMatch(Input.Phone.Trim(), @"^\+54 9 \d{10}$"))
        {
            ModelState.AddModelError(PhoneField, "Ingresa el telefono argentino como +54 9 seguido de 10 digitos.");
        }

        if (!Input.PublishEmail && !Input.AcceptsCalls && !Input.RespondsWhatsApp && !Input.AllowSiteChat)
        {
            ModelState.AddModelError(string.Empty, "Selecciona al menos un canal de contacto para publicar.");
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        if (user.IsCompany && string.IsNullOrWhiteSpace(Input.CompanyTagline))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.CompanyTagline)}", "Ingresa un lema para la empresa.");
        }

        if (!ModelState.IsValid)
        {
            LoadCompanyState(user);
            return Page();
        }

        var previousLogoUrl = user.CompanyLogoUrl;
        var previousHeroBackgroundUrl = user.CompanyHeroBackgroundUrl;
        var uploadedLogoUrl = user.CompanyLogoUrl;
        var uploadedHeroBackgroundUrl = user.CompanyHeroBackgroundUrl;

        if (user.IsCompany && CompanyLogo is not null && CompanyLogo.Length > 0)
        {
            try
            {
                uploadedLogoUrl = await imageStorageService.UploadCompanyLogoAsync(CompanyLogo);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(CompanyLogoField, ex.Message);
                LoadCompanyState(user);
                return Page();
            }
        }

        if (user.IsCompany && CompanyHeroBackground is not null && CompanyHeroBackground.Length > 0)
        {
            try
            {
                uploadedHeroBackgroundUrl = await imageStorageService.UploadCompanyHeroBackgroundAsync(CompanyHeroBackground);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(CompanyHeroBackgroundField, ex.Message);
                LoadCompanyState(user);
                return Page();
            }
        }

        user.ArgentineLocalityId = Input.ArgentineLocalityId;
        user.Phone = NormalizePhone(Input.Phone, Input.PhoneCountry);
        user.RespondsEmails = Input.PublishEmail;
        user.AcceptsCalls = Input.AcceptsCalls;
        user.RespondsWhatsApp = Input.RespondsWhatsApp;
        user.AllowsSiteChat = Input.AllowSiteChat;
        user.MessageBubbleColor = NormalizeMessageBubbleColor(Input.MessageBubbleColor);
        user.ContactPreference = BuildContactPreference(user.RespondsEmails, user.AcceptsCalls, user.RespondsWhatsApp, user.AllowsSiteChat);
        user.HeaderPublicationGroupsCsv = await publicationGroupPreferenceService.NormalizeGroupNamesCsvAsync(Input.HeaderPublicationGroups);
        if (user.IsCompany)
        {
            user.CompanyTagline = Input.CompanyTagline?.Trim();
            user.CompanyLogoUrl = string.IsNullOrWhiteSpace(uploadedLogoUrl) ? null : uploadedLogoUrl.Trim();
            user.CompanyHeroBackgroundUrl = string.IsNullOrWhiteSpace(uploadedHeroBackgroundUrl) ? null : uploadedHeroBackgroundUrl.Trim();
        }
        await db.SaveChangesAsync();
        Response.Cookies.Delete(NavigationLocalityService.CookieName);

        var obsoleteAssets = new List<string>();
        if (!string.IsNullOrWhiteSpace(previousLogoUrl)
            && !string.Equals(previousLogoUrl, user.CompanyLogoUrl, StringComparison.OrdinalIgnoreCase))
        {
            obsoleteAssets.Add(previousLogoUrl);
        }

        if (!string.IsNullOrWhiteSpace(previousHeroBackgroundUrl)
            && !string.Equals(previousHeroBackgroundUrl, user.CompanyHeroBackgroundUrl, StringComparison.OrdinalIgnoreCase))
        {
            obsoleteAssets.Add(previousHeroBackgroundUrl);
        }

        if (obsoleteAssets.Count > 0)
        {
            await imageStorageService.DeletePublicObjectsAsync(obsoleteAssets);
        }

        SuccessMessage = "Configuraciones actualizadas.";
        return RedirectToPage();
    }

    private async Task<bool> TryResolveLocalityAsync()
    {
        try
        {
            var locality = await localityLookupService.EnsureLocalityAsync(
                Input.ArgentineLocalityId,
                Input.SelectedExternalLocalityId,
                HttpContext.RequestAborted);

            Input.ArgentineLocalityId = locality.Id;
            Input.ArgentineLocalityLabel = $"{locality.Locality}, {locality.Province}";
            Input.SelectedExternalLocalityId = string.Empty;
            ModelState.Remove($"{nameof(Input)}.{nameof(InputModel.ArgentineLocalityId)}");
            ModelState.Remove($"{nameof(Input)}.{nameof(InputModel.ArgentineLocalityLabel)}");
            ModelState.Remove($"{nameof(Input)}.{nameof(InputModel.SelectedExternalLocalityId)}");
            return true;
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(LocalityField, ex.Message);
            return false;
        }
    }

    private void LoadCompanyState(Ventagram.Models.ApplicationUser user)
    {
        IsCompany = user.IsCompany;
        CompanyName = user.CompanyName;
        CompanyPublicUrl = user.IsCompany && !string.IsNullOrWhiteSpace(user.CompanySlug)
            ? $"/{user.CompanySlug}"
            : null;
        CurrentCompanyLogoUrl = user.CompanyLogoUrl;
        CurrentCompanyHeroBackgroundUrl = user.CompanyHeroBackgroundUrl;
    }

    private async Task LoadHeaderGroupOptionsAsync()
    {
        HeaderGroupOptions = await publicationGroupTypeService.GetActiveAsync();
    }

    private static string NormalizePhone(string phone, string phoneCountry)
    {
        var trimmed = phone.Trim();
        if (phoneCountry != "AR")
        {
            return trimmed;
        }

        var digits = Regex.Replace(trimmed, @"\D", "");
        if (digits.StartsWith("549", StringComparison.Ordinal))
        {
            digits = digits[3..];
        }
        else if (digits.StartsWith("54", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith("9", StringComparison.Ordinal) && digits.Length == 11)
        {
            digits = digits[1..];
        }

        return digits.Length == 10 ? $"+54 9 {digits}" : trimmed;
    }

    private static string NormalizeMessageBubbleColor(string? color)
    {
        return string.Equals(color, "blue", StringComparison.OrdinalIgnoreCase) ? "blue" : "rose";
    }

    private static string BuildContactPreference(bool publishEmail, bool publishPhone, bool publishWhatsApp, bool allowSiteChat)
    {
        var preferences = new List<string>();
        if (publishEmail) preferences.Add("Email");
        if (publishPhone) preferences.Add("Calls");
        if (publishWhatsApp) preferences.Add("WhatsApp");
        if (allowSiteChat) preferences.Add("SiteChat");
        return preferences.Count == 0 ? "None" : string.Join(",", preferences);
    }

    public class InputModel
    {
        public int? ArgentineLocalityId { get; set; }

        public string ArgentineLocalityLabel { get; set; } = string.Empty;

        public string? SelectedExternalLocalityId { get; set; }

        [Required(ErrorMessage = "Ingresa tu telefono.")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string PhoneCountry { get; set; } = "AR";

        public bool PublishEmail { get; set; }

        public bool AcceptsCalls { get; set; }

        public bool RespondsWhatsApp { get; set; }

        public bool AllowSiteChat { get; set; } = true;

        public string MessageBubbleColor { get; set; } = "rose";

        public List<string> HeaderPublicationGroups { get; set; } = [];

        [StringLength(180)]
        public string? CompanyTagline { get; set; }
    }
}
