using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Services;

public class SuggestionService(VentaMapDbContext db)
{
    public async Task<(bool Success, string Message)> SubmitAsync(int? userId, string? senderName, string? senderEmail, string? message)
    {
        var normalizedMessage = (message ?? string.Empty).Trim();
        if (normalizedMessage.Length < 5)
        {
            return (false, "Escribe una sugerencia un poco más detallada.");
        }

        if (normalizedMessage.Length > 2000)
        {
            normalizedMessage = normalizedMessage[..2000];
        }

        var suggestion = new SiteSuggestion
        {
            UserId = userId,
            SenderName = string.IsNullOrWhiteSpace(senderName) ? null : senderName.Trim(),
            SenderEmail = string.IsNullOrWhiteSpace(senderEmail) ? null : senderEmail.Trim(),
            Message = normalizedMessage,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Add(suggestion);
        await db.SaveChangesAsync();
        return (true, "Gracias. Tu sugerencia fue enviada al sitio.");
    }

    public Task<List<SiteSuggestion>> GetRecentAsync()
    {
        return db.Set<SiteSuggestion>()
            .AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }
}
