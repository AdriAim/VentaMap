using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Ventagram.ChatService.Data;

namespace Ventagram.ChatService.Services;

public class SmtpEmailSender(
    IConfiguration configuration,
    ILogger<SmtpEmailSender> logger,
    VentagramLookupDbContext lookupDb) : IEmailSender
{
    public async Task<bool> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        string? replyToEmail = null,
        string? replyToName = null)
    {
        var smtpHost = configuration["Email:SmtpHost"];
        var smtpPort = configuration.GetValue<int?>("Email:SmtpPort") ?? 587;
        var smtpUser = configuration["Email:SmtpUser"];
        var smtpPassword = configuration["Email:SmtpPassword"];
        var fromEmail = configuration["Email:FromEmail"];
        var fromName = configuration["Email:FromName"] ?? "VentaMap";
        var useSsl = configuration.GetValue("Email:UseSsl", true);
        var timeoutSeconds = Math.Clamp(configuration.GetValue<int?>("Email:TimeoutSeconds") ?? 25, 5, 60);
        var normalizedToEmail = toEmail.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(fromEmail))
        {
            logger.LogWarning("SMTP no configurado. No se envio el correo a {ToEmail}.", toEmail);
            return false;
        }

        var isDebugUser = await lookupDb.Users
            .AsNoTracking()
            .AnyAsync(x => x.Email == normalizedToEmail && x.IsDebugUser);
        if (isDebugUser)
        {
            logger.LogInformation("Se omitio el correo a {ToEmail} porque el usuario esta marcado como debug.", toEmail);
            return true;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = textBody,
            IsBodyHtml = false
        };

        message.To.Add(normalizedToEmail);
        if (!string.IsNullOrWhiteSpace(replyToEmail))
        {
            message.ReplyToList.Add(new MailAddress(replyToEmail.Trim(), replyToName?.Trim()));
        }

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html"));

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = useSsl,
            Timeout = timeoutSeconds * 1000,
            Credentials = string.IsNullOrWhiteSpace(smtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(smtpUser, smtpPassword)
        };

        try
        {
            await client.SendMailAsync(message).WaitAsync(TimeSpan.FromSeconds(timeoutSeconds));
            return true;
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "El envio de correo a {ToEmail} supero el timeout SMTP de {TimeoutSeconds} segundos.", toEmail, timeoutSeconds);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo enviar el correo a {ToEmail}.", toEmail);
            return false;
        }
    }
}
