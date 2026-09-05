namespace Ventagram.Services;

public interface IEmailSender
{
    Task<bool> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        string? replyToEmail = null,
        string? replyToName = null);
}
