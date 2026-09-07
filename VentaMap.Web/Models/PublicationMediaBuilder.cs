namespace VentaMap.Models;

public static class PublicationMediaBuilder
{
    public static IReadOnlyList<string> ParseLegacyImagesCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsValidMediaUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(11)
            .ToList();
    }

    public static string NormalizeLegacyImagesCsv(string? raw)
        => string.Join(",", ParseLegacyImagesCsv(raw));

    public static string? NormalizeOptionalUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        return IsValidMediaUrl(value) ? value : null;
    }

    public static List<PublicationMedia> Build(
        string? imagesCsv,
        string? videoUrl,
        DateTime createdAtUtc)
    {
        var items = new List<PublicationMedia>();
        var normalizedVideoUrl = NormalizeOptionalUrl(videoUrl);
        var normalizedImages = ParseLegacyImagesCsv(imagesCsv);
        var sortOrder = 1;

        if (!string.IsNullOrWhiteSpace(normalizedVideoUrl))
        {
            items.Add(new PublicationMedia
            {
                SortOrder = sortOrder++,
                MediaType = PublicationMediaType.Video,
                Url = normalizedVideoUrl,
                IsPrimary = true,
                CreatedAtUtc = createdAtUtc
            });
        }

        for (var index = 0; index < normalizedImages.Count; index++)
        {
            items.Add(new PublicationMedia
            {
                SortOrder = sortOrder++,
                MediaType = PublicationMediaType.Image,
                Url = normalizedImages[index],
                IsPrimary = normalizedVideoUrl is null && index == 0,
                CreatedAtUtc = createdAtUtc
            });
        }

        return items;
    }

    private static bool IsValidMediaUrl(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && (Uri.IsWellFormedUriString(value, UriKind.Absolute)
                || value.StartsWith("/", StringComparison.Ordinal));
}
