using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class AuthService(
    VentagramDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IDataProtectionProvider dataProtectionProvider)
{
    private readonly ITimeLimitedDataProtector passwordResetProtector =
        dataProtectionProvider.CreateProtector("Ventagram.PasswordReset").ToTimeLimitedDataProtector();

    public static string HashPassword(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    public static string GenerateAnonymousPassword()
    {
        return Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
    }

    public async Task<(bool Success, string? Error, ApplicationUser? User)> RegisterAsync(
        string name,
        string email,
        string phone,
        string password,
        string phoneCountry,
        bool allowsSiteChat,
        bool respondsEmails,
        bool acceptsCalls,
        bool respondsWhatsApp,
        int? argentineLocalityId = null,
        string provider = "Local",
        bool isCompany = false,
        string? companyName = null,
        string? companySlug = null,
        string? companyLogoUrl = null,
        string? companyHeroBackgroundUrl = null,
        string? companyTagline = null,
        string? companyIndustry = null,
        string? headerPublicationGroupsCsv = null)
    {
        email = email.Trim().ToLowerInvariant();
        phone = NormalizePhone(phone, phoneCountry, provider);
        var exists = await db.Users.AnyAsync(x => x.Email == email);
        if (exists)
        {
            return (false, "Ya existe un usuario con ese email.", null);
        }

        var normalizedCompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        var normalizedCompanySlug = string.IsNullOrWhiteSpace(companySlug) ? null : NormalizeCompanySlug(companySlug);

        if (isCompany)
        {
            if (string.IsNullOrWhiteSpace(normalizedCompanyName))
            {
                return (false, "Ingresa el nombre de la empresa.", null);
            }

            if (string.IsNullOrWhiteSpace(normalizedCompanySlug))
            {
                return (false, "No se pudo generar una dirección válida para la empresa.", null);
            }

            if (await db.Users.AnyAsync(x => x.CompanyName != null && x.CompanyName.ToLower() == normalizedCompanyName.ToLower()))
            {
                return (false, "Ya existe una empresa con ese nombre en el sitio.", null);
            }

            if (await db.Users.AnyAsync(x => x.CompanySlug == normalizedCompanySlug))
            {
                return (false, "La dirección pública de la empresa ya está en uso.", null);
            }
        }

        var user = new ApplicationUser
        {
            Name = name.Trim(),
            IsCompany = isCompany,
            CompanyName = normalizedCompanyName,
            CompanySlug = normalizedCompanySlug,
            CompanyLogoUrl = string.IsNullOrWhiteSpace(companyLogoUrl) ? null : companyLogoUrl.Trim(),
            CompanyHeroBackgroundUrl = string.IsNullOrWhiteSpace(companyHeroBackgroundUrl) ? null : companyHeroBackgroundUrl.Trim(),
            CompanyTagline = string.IsNullOrWhiteSpace(companyTagline) ? null : companyTagline.Trim(),
            CompanyIndustry = string.IsNullOrWhiteSpace(companyIndustry) ? null : companyIndustry.Trim(),
            Email = email,
            Phone = phone.Trim(),
            AllowsSiteChat = allowsSiteChat,
            RespondsEmails = respondsEmails,
            AcceptsCalls = acceptsCalls,
            RespondsWhatsApp = respondsWhatsApp,
            ArgentineLocalityId = argentineLocalityId,
            HeaderPublicationGroupsCsv = string.IsNullOrWhiteSpace(headerPublicationGroupsCsv) ? null : headerPublicationGroupsCsv.Trim(),
            ContactPreference = BuildContactPreference(allowsSiteChat, respondsEmails, acceptsCalls, respondsWhatsApp),
            PasswordHash = provider == "Google" ? string.Empty : HashPassword(password),
            AuthProvider = provider
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (true, null, user);
    }

    public static string NormalizeCompanySlug(string? companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return string.Empty;
        }

        var normalized = companyName.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var lastWasSeparator = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    public async Task<ApplicationUser?> ValidateUserAsync(string email, string password)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var hash = HashPassword(password);
        return await db.Users.FirstOrDefaultAsync(x => x.Email == normalized && x.PasswordHash == hash);
    }

    public async Task<ApplicationUser> FindOrCreateGoogleUserAsync(string name, string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(x => x.Email == normalized);
        if (existing is not null)
        {
            return existing;
        }

        var created = await RegisterAsync(name, normalized, string.Empty, string.Empty, "AR", true, false, true, true, null, "Google");
        return created.User!;
    }

    public async Task SignInAsync(ApplicationUser user)
    {
        httpContextAccessor.HttpContext?.Response.Cookies.Delete(NavigationLocalityService.CookieName);
        httpContextAccessor.HttpContext?.Response.Cookies.Delete(PublicationGroupPreferenceService.CookieName);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new("phone", user.Phone ?? string.Empty),
            new("contact-preference", BuildContactPreference(user.AllowsSiteChat, user.RespondsEmails, user.AcceptsCalls, user.RespondsWhatsApp)),
            new("provider", user.AuthProvider),
            new("is-admin", user.IsAdmin ? "true" : "false")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContextAccessor.HttpContext!.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });
    }

    public async Task SignOutAsync()
    {
        httpContextAccessor.HttpContext?.Response.Cookies.Delete(PublicationGroupPreferenceService.CookieName);
        await httpContextAccessor.HttpContext!.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task<ApplicationUser?> FindUserByEmailAsync(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await db.Users.FirstOrDefaultAsync(x => x.Email == normalized);
    }

    public string GeneratePasswordResetToken(ApplicationUser user)
    {
        var payload = $"{user.Id}|{user.Email}";
        return passwordResetProtector.Protect(payload, TimeSpan.FromHours(1));
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword)
    {
        email = email.Trim().ToLowerInvariant();
        if (!TryReadPasswordResetToken(token, out var userId, out var tokenEmail))
        {
            return (false, "El enlace de recuperación no es válido o expiró.");
        }

        if (!string.Equals(email, tokenEmail, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "El enlace de recuperación no coincide con el email indicado.");
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.Email == email);
        if (user is null)
        {
            return (false, "No se encontró una cuenta para ese enlace.");
        }

        user.PasswordHash = HashPassword(newPassword);
        await db.SaveChangesAsync();
        return (true, null);
    }

    private static string BuildContactPreference(bool allowsSiteChat, bool respondsEmails, bool acceptsCalls, bool respondsWhatsApp)
    {
        var preferences = new List<string>();
        if (allowsSiteChat)
        {
            preferences.Add("SiteChat");
        }

        if (respondsEmails)
        {
            preferences.Add("Email");
        }

        if (acceptsCalls)
        {
            preferences.Add("Calls");
        }

        if (respondsWhatsApp)
        {
            preferences.Add("WhatsApp");
        }

        return preferences.Count == 0 ? "None" : string.Join("|", preferences);
    }

    private static string NormalizePhone(string phone, string phoneCountry, string provider)
    {
        var trimmed = phone.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return provider == "Google" ? string.Empty : trimmed;
        }

        if (!string.Equals(phoneCountry, "AR", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("549"))
        {
            digits = digits[3..];
        }
        else if (digits.StartsWith("54"))
        {
            digits = digits[2..];
        }

        if (digits.StartsWith("9"))
        {
            digits = digits[1..];
        }

        if (digits.Length >= 10)
        {
            return $"+54 9 {digits[..10]}";
        }

        return trimmed;
    }

    private bool TryReadPasswordResetToken(string token, out int userId, out string email)
    {
        userId = 0;
        email = string.Empty;

        try
        {
            var payload = passwordResetProtector.Unprotect(token);
            var parts = payload.Split('|', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out userId))
            {
                return false;
            }

            email = parts[1];
            return !string.IsNullOrWhiteSpace(email);
        }
        catch
        {
            return false;
        }
    }
}
