using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Services;

namespace VentaMap.Pages.Account;

[Authorize]
public class SettingsModel(
    VentaMapDbContext db,
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

    public IReadOnlyList<VentaMap.Models.PublicationGroupType> HeaderGroupOptions { get; private set; } = [];

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
        Input.PhoneCountry = user.IsCompany ? "INT" : user.Phone.StartsWith("+54 9 ", StringComparison.Ordinal) ? "AR" : "INT";
        Input.PublishEmail = user.RespondsEmails;
        Input.AcceptsCalls = user.AcceptsCalls;
        Input.RespondsWhatsApp = user.RespondsWhatsApp;
        Input.AllowSiteChat = user.AllowsSiteChat;
        Input.MessageBubbleColor = NormalizeMessageBubbleColor(user.MessageBubbleColor);
        Input.CompanyTagline = user.CompanyTagline ?? string.Empty;
        Input.CompanyContactName = user.CompanyContactName ?? string.Empty;
        Input.CompanyContactName2 = user.CompanyContactName2 ?? string.Empty;
        Input.CompanyContactPhone2 = user.CompanyContactPhone2 ?? string.Empty;
        Input.CompanyContactName3 = user.CompanyContactName3 ?? string.Empty;
        Input.CompanyContactPhone3 = user.CompanyContactPhone3 ?? string.Empty;
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
            LoadCompanyState(await db.Users.FirstOrDefaultAsync(x => x.Id == userId) ?? new VentaMap.Models.ApplicationUser());
            return Page();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        var phoneIsRequired = user.IsCompany
            || Input.AcceptsCalls
            || Input.RespondsWhatsApp;
        var phoneErrorMessage = user.IsCompany
            ? "Ingresa el telefono argentino como +54 9 seguido de 9, 10 u 11 digitos."
            : "Ingresa el telefono argentino como +54 9 seguido de 9, 10 u 11 digitos. O destilda Recibe llamadas y Recibe WhatsApp para obviar la carga de teléfono.";
        if (phoneIsRequired && string.IsNullOrWhiteSpace(Input.Phone))
        {
            ModelState.AddModelError(PhoneField, phoneErrorMessage);
        }
        else if (phoneIsRequired
                 && !string.IsNullOrWhiteSpace(Input.Phone)
                 && Input.PhoneCountry == "AR"
                 && !Regex.IsMatch(Input.Phone.Trim(), @"^\+54 9 \d{9,11}$"))
        {
            ModelState.AddModelError(PhoneField, phoneErrorMessage);
        }

        // Si no se publica por llamadas ni WhatsApp, no conservamos un teléfono
        // parcial ni exigimos su formato para poder guardar la configuración.
        if (!phoneIsRequired)
        {
            Input.Phone = string.Empty;
        }

        if (!Input.PublishEmail && !Input.AcceptsCalls && !Input.RespondsWhatsApp && !Input.AllowSiteChat)
        {
            ModelState.AddModelError(string.Empty, "Selecciona al menos un canal de contacto para publicar.");
        }

        if (user.IsCompany && string.IsNullOrWhiteSpace(Input.CompanyTagline))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.CompanyTagline)}", "Ingresa un lema para la empresa.");
        }

        if (user.IsCompany)
        {
            ValidateAdditionalCompanyContact(Input.CompanyContactName2, Input.CompanyContactPhone2, "2");
            ValidateAdditionalCompanyContact(Input.CompanyContactName3, Input.CompanyContactPhone3, "3");
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
        user.Phone = NormalizePhone(Input.Phone ?? string.Empty, Input.PhoneCountry);
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
            user.CompanyContactName = NullIfWhiteSpace(Input.CompanyContactName);
            user.CompanyContactName2 = NullIfWhiteSpace(Input.CompanyContactName2);
            user.CompanyContactPhone2 = NullIfWhiteSpace(Input.CompanyContactPhone2);
            user.CompanyContactName3 = NullIfWhiteSpace(Input.CompanyContactName3);
            user.CompanyContactPhone3 = NullIfWhiteSpace(Input.CompanyContactPhone3);
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

    private void LoadCompanyState(VentaMap.Models.ApplicationUser user)
    {
        IsCompany = user.IsCompany;
        CompanyName = user.CompanyName;
        CompanyPublicUrl = user.IsCompany && !string.IsNullOrWhiteSpace(user.CompanySlug)
            ? $"/catalogo/{user.CompanySlug}"
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

        return digits.Length is >= 9 and <= 11 ? $"+54 9 {digits}" : trimmed;
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

    private void ValidateAdditionalCompanyContact(string? name, string? phone, string number)
    {
        var hasName = !string.IsNullOrWhiteSpace(name);
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        if (hasName && !hasPhone)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.CompanyContactPhone2)}".Replace("2", number), "Ingresa el teléfono de este contacto.");
        }
        else if (hasPhone && !hasName)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.CompanyContactName2)}".Replace("2", number), "Ingresa quién atiende este teléfono.");
        }
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public class InputModel
    {
        public int? ArgentineLocalityId { get; set; }

        public string ArgentineLocalityLabel { get; set; } = string.Empty;

        public string? SelectedExternalLocalityId { get; set; }

        public string? Phone { get; set; }

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

        [StringLength(120)]
        public string? CompanyContactName { get; set; }

        [StringLength(120)]
        public string? CompanyContactName2 { get; set; }

        [StringLength(32)]
        public string? CompanyContactPhone2 { get; set; }

        [StringLength(120)]
        public string? CompanyContactName3 { get; set; }

        [StringLength(32)]
        public string? CompanyContactPhone3 { get; set; }
    }
}
