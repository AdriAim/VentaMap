using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using Ventagram.Data;
using Ventagram.Models;
using Ventagram.Services;

namespace Ventagram.Pages.Account;

public class RegisterModel(
    AuthService authService,
    IConfiguration configuration,
    VentagramDbContext db,
    CloudflareR2ImageStorageService imageStorageService,
    ArgentineLocalityLookupService localityLookupService,
    PublicationGroupPreferenceService publicationGroupPreferenceService,
    PublicationGroupTypeService publicationGroupTypeService,
    VentagramParameterService parameters,
    ILogger<RegisterModel> logger) : PageModel
{
    private const string EmailField = $"{nameof(Input)}.{nameof(InputModel.Email)}";
    private const string PhoneField = $"{nameof(Input)}.{nameof(InputModel.Phone)}";
    private const string LocalityField = $"{nameof(Input)}.{nameof(InputModel.ArgentineLocalityId)}";
    private const string CompanyNameField = $"{nameof(Input)}.{nameof(InputModel.CompanyName)}";
    private const string CompanyIndustryField = $"{nameof(Input)}.{nameof(InputModel.CompanyIndustry)}";
    private const string CompanyLogoField = nameof(CompanyLogo);
    private const string CompanyHeroBackgroundField = nameof(CompanyHeroBackground);
    private static readonly HashSet<string> ReservedCompanySlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "account", "api", "browse", "contacto", "favorites", "index", "legales", "messages", "mispublicaciones",
        "privacy", "publications", "reglas", "trash", "empresa", "contact", "settings", "login", "register"
    };

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? CompanyLogo { get; set; }

    [BindProperty]
    public IFormFile? CompanyHeroBackground { get; set; }

    public IReadOnlyList<string> CompanyIndustryOptions { get; } =
    [
        "Inmobiliaria",
        "Constructora",
        "Aberturas",
        "Corralon",
        "Ferreteria",
        "Materiales de construcción",
        "Rodados",
        "Muebles y decoración",
        "Servicios",
        "Otro"
    ];

    public bool IsGoogleEnabled => !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]);

    public bool IsPaidSiteEnabled { get; private set; }

    public IReadOnlyList<PublicationGroupType> HeaderGroupOptions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadPageOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadPageOptionsAsync();

        if (!await TryResolveLocalityAsync())
        {
            LogFailedRegistrationAttempt("Locality resolution failed.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            LogFailedRegistrationAttempt("ModelState invalid after locality resolution.");
            return Page();
        }

        if (Input.AccountType == "Person" && string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.Name)}", "Ingresa tu nombre.");
        }

        if (Input.PhoneCountry == "AR" && !Regex.IsMatch(Input.Phone.Trim(), @"^\+54 9 \d{10}$"))
        {
            ModelState.AddModelError(PhoneField, "Ingresa el telefono argentino como +54 9 seguido de 10 digitos.");
            LogFailedRegistrationAttempt("Invalid Argentine phone format.");
            return Page();
        }

        string? companyLogoUrl = null;
        string? companyHeroBackgroundUrl = null;
        string? companySlug = null;

        if (Input.AccountType == "Company")
        {
            var normalizedCompanyName = Input.CompanyName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Input.CompanyName))
            {
                ModelState.AddModelError(CompanyNameField, "Ingresa el nombre de la empresa.");
            }

            if (string.IsNullOrWhiteSpace(Input.CompanyTagline))
            {
                ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.CompanyTagline)}", "Ingresa un lema para la empresa.");
            }

            if (string.IsNullOrWhiteSpace(Input.CompanyIndustry))
            {
                ModelState.AddModelError(CompanyIndustryField, "Selecciona el rubro de la empresa.");
            }
            else if (!CompanyIndustryOptions.Contains(Input.CompanyIndustry.Trim(), StringComparer.Ordinal))
            {
                ModelState.AddModelError(CompanyIndustryField, "Selecciona un rubro válido para la empresa.");
            }

            companySlug = AuthService.NormalizeCompanySlug(Input.CompanyName);
            if (string.IsNullOrWhiteSpace(companySlug) || ReservedCompanySlugs.Contains(companySlug))
            {
                ModelState.AddModelError(CompanyNameField, "El nombre de la empresa genera una dirección no disponible. Usa otro nombre.");
            }

            if (CompanyLogo is null || CompanyLogo.Length <= 0)
            {
                ModelState.AddModelError(CompanyLogoField, "Sube un logo cuadrado para la empresa.");
            }

            if (!Input.RespondsWhatsApp)
            {
                ModelState.AddModelError(string.Empty, "Las cuentas empresa deben ofrecer contacto por WhatsApp.");
            }

            if (!Input.RespondsEmails)
            {
                ModelState.AddModelError(string.Empty, "Las cuentas empresa deben ofrecer contacto por email.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedCompanyName)
                && await db.Users.AnyAsync(x => x.CompanyName != null && x.CompanyName.ToLower() == normalizedCompanyName.ToLower()))
            {
                ModelState.AddModelError(CompanyNameField, "Ya existe una empresa con ese nombre en el sitio.");
            }

            if (!string.IsNullOrWhiteSpace(companySlug)
                && await db.Users.AnyAsync(x => x.CompanySlug == companySlug))
            {
                ModelState.AddModelError(CompanyNameField, "La dirección pública de esa empresa ya existe en el sitio.");
            }
        }

        if (!Input.AllowSiteChat && !Input.RespondsEmails && !Input.AcceptsCalls && !Input.RespondsWhatsApp)
        {
            ModelState.AddModelError(string.Empty, "Marca al menos una forma de contacto.");
            LogFailedRegistrationAttempt("No contact channel selected.");
            return Page();
        }

        var headerGroupsCsv = await publicationGroupPreferenceService.NormalizeGroupNamesCsvAsync(Input.HeaderPublicationGroups);

        if (!ModelState.IsValid)
        {
            LogFailedRegistrationAttempt("ModelState invalid before registration service call.");
            return Page();
        }

        if (Input.AccountType == "Company")
        {
            try
            {
                companyLogoUrl = await imageStorageService.UploadCompanyLogoAsync(CompanyLogo!);

                if (CompanyHeroBackground is not null && CompanyHeroBackground.Length > 0)
                {
                    companyHeroBackgroundUrl = await imageStorageService.UploadCompanyHeroBackgroundAsync(CompanyHeroBackground);
                }
            }
            catch (InvalidOperationException ex)
            {
                var failedField = companyLogoUrl is null ? CompanyLogoField : CompanyHeroBackgroundField;

                if (!string.IsNullOrWhiteSpace(companyLogoUrl))
                {
                    await imageStorageService.DeletePublicObjectsAsync([companyLogoUrl]);
                    companyLogoUrl = null;
                }

                ModelState.AddModelError(failedField, ex.Message);
                LogFailedRegistrationAttempt("Company media upload failed.");
                return Page();
            }
            catch (UnknownImageFormatException ex)
            {
                await CleanupUploadedCompanyAssetsAsync();
                ModelState.AddModelError(
                    companyLogoUrl is null ? CompanyLogoField : CompanyHeroBackgroundField,
                    "La imagen seleccionada no tiene un formato compatible. Usa JPG, PNG o WEBP.");
                logger.LogWarning(ex, "Company media upload failed because the image format could not be decoded.");
                LogFailedRegistrationAttempt("Company media upload failed due to unsupported image format.");
                return Page();
            }
            catch (AmazonS3Exception ex)
            {
                await CleanupUploadedCompanyAssetsAsync();
                ModelState.AddModelError(
                    string.Empty,
                    "No se pudo subir la imagen de la empresa en este momento. Intenta otra vez en unos minutos.");
                logger.LogError(ex, "Company media upload failed while sending assets to R2/S3.");
                LogFailedRegistrationAttempt("Company media upload failed due to storage provider error.");
                return Page();
            }
            catch (Exception ex)
            {
                await CleanupUploadedCompanyAssetsAsync();
                ModelState.AddModelError(
                    string.Empty,
                    "No se pudo completar el alta de la empresa por un error interno. Intenta otra vez.");
                logger.LogError(ex, "Unexpected company registration failure during media upload.");
                LogFailedRegistrationAttempt("Unexpected company media upload failure.");
                return Page();
            }
        }

        var registrationName = Input.AccountType == "Company"
            ? Input.CompanyName!.Trim()
            : Input.Name!.Trim();

        var result = await authService.RegisterAsync(
            registrationName,
            Input.Email,
            Input.PhoneCountry == "AR" ? Input.Phone : Input.Phone.Trim(),
            Input.Password,
            Input.PhoneCountry,
            Input.AllowSiteChat,
            Input.RespondsEmails,
            Input.AcceptsCalls,
            Input.RespondsWhatsApp,
            Input.ArgentineLocalityId,
            isCompany: Input.AccountType == "Company",
            companyName: Input.AccountType == "Company" ? Input.CompanyName : null,
            companySlug: companySlug,
            companyLogoUrl: companyLogoUrl,
            companyHeroBackgroundUrl: companyHeroBackgroundUrl,
            companyTagline: Input.AccountType == "Company" ? Input.CompanyTagline : null,
            companyIndustry: Input.AccountType == "Company" ? Input.CompanyIndustry : null,
            headerPublicationGroupsCsv: headerGroupsCsv);
        if (!result.Success || result.User is null)
        {
            if (!string.IsNullOrWhiteSpace(result.Error) && result.Error.Contains("email", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(EmailField, result.Error);
            }
            else if (!string.IsNullOrWhiteSpace(result.Error) && result.Error.Contains("empresa", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(CompanyNameField, result.Error);
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "No se pudo crear la cuenta.");
            }

            LogFailedRegistrationAttempt("AuthService.RegisterAsync returned failure.");
            return Page();
        }

        await authService.SignInAsync(result.User);
        TempData["AccountCreatedMessage"] = "El usuario se creó correctamente.";
        return RedirectToPage("/Index");

        async Task CleanupUploadedCompanyAssetsAsync()
        {
            if (string.IsNullOrWhiteSpace(companyLogoUrl) && string.IsNullOrWhiteSpace(companyHeroBackgroundUrl))
            {
                return;
            }

            try
            {
                await imageStorageService.DeletePublicObjectsAsync(
                [
                    companyLogoUrl ?? string.Empty,
                    companyHeroBackgroundUrl ?? string.Empty
                ]);
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(cleanupEx, "Failed to cleanup partially uploaded company assets.");
            }

            companyLogoUrl = null;
            companyHeroBackgroundUrl = null;
        }
    }

    public IActionResult OnPostGoogle()
    {
        if (!IsGoogleEnabled)
        {
            ModelState.AddModelError(string.Empty, "Google no esta configurado.");
            return Page();
        }

        var properties = new AuthenticationProperties { RedirectUri = "/" };
        return Challenge(properties, "Google");
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
            return true;
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(LocalityField, ex.Message);
            return false;
        }
    }

    private void LogFailedRegistrationAttempt(string reason)
    {
        var errors = ModelState
            .Where(entry => entry.Value is not null && entry.Value.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => $"{entry.Key}: {error.ErrorMessage}"))
            .ToArray();

        logger.LogWarning(
            "Failed register attempt. Reason={Reason} AccountType={AccountType} EmailHash={EmailHash} PhoneCountry={PhoneCountry} PhoneLength={PhoneLength} HasLocalityId={HasLocalityId} HasLocalityLabel={HasLocalityLabel} HasExternalLocalityId={HasExternalLocalityId} HeaderGroupsCount={HeaderGroupsCount} AllowSiteChat={AllowSiteChat} RespondsEmails={RespondsEmails} AcceptsCalls={AcceptsCalls} RespondsWhatsApp={RespondsWhatsApp} HasCompanyName={HasCompanyName} CompanyIndustry={CompanyIndustry} CompanyLogoProvided={CompanyLogoProvided} CompanyHeroBackgroundProvided={CompanyHeroBackgroundProvided} Errors={Errors}",
            reason,
            Input.AccountType,
            AuthService.HashPassword(Input.Email.Trim().ToLowerInvariant()),
            Input.PhoneCountry,
            Input.Phone?.Trim().Length ?? 0,
            Input.ArgentineLocalityId is not null,
            !string.IsNullOrWhiteSpace(Input.ArgentineLocalityLabel),
            !string.IsNullOrWhiteSpace(Input.SelectedExternalLocalityId),
            Input.HeaderPublicationGroups?.Count ?? 0,
            Input.AllowSiteChat,
            Input.RespondsEmails,
            Input.AcceptsCalls,
            Input.RespondsWhatsApp,
            !string.IsNullOrWhiteSpace(Input.CompanyName),
            Input.CompanyIndustry,
            CompanyLogo is not null && CompanyLogo.Length > 0,
            CompanyHeroBackground is not null && CompanyHeroBackground.Length > 0,
            string.Join(" | ", errors));
    }

    private async Task LoadHeaderGroupOptionsAsync()
    {
        HeaderGroupOptions = await publicationGroupTypeService.GetActiveAsync();
    }

    private async Task LoadPageOptionsAsync()
    {
        await LoadHeaderGroupOptionsAsync();
        IsPaidSiteEnabled = await parameters.GetBoolAsync(VentagramParameterService.PaidSiteEnabled, fallback: false);
    }

    public class InputModel
    {
        [Required]
        public string AccountType { get; set; } = "Person";

        public string? Name { get; set; }

        public string? CompanyName { get; set; }

        public string? CompanyTagline { get; set; }

        public string? CompanyIndustry { get; set; }

        [Required(ErrorMessage = "Ingresa tu email.")]
        [EmailAddress(ErrorMessage = "Ingresa un email valido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu telefono.")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string PhoneCountry { get; set; } = "AR";

        public int? ArgentineLocalityId { get; set; }

        public string ArgentineLocalityLabel { get; set; } = string.Empty;

        public string? SelectedExternalLocalityId { get; set; }

        public List<string> HeaderPublicationGroups { get; set; } = [];

        public bool RespondsEmails { get; set; }

        public bool AcceptsCalls { get; set; }

        public bool RespondsWhatsApp { get; set; }

        public bool AllowSiteChat { get; set; } = true;

        [Required(ErrorMessage = "Ingresa una contrasena.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repite la contrasena.")]
        [Compare(nameof(Password), ErrorMessage = "Las contrasenas no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
