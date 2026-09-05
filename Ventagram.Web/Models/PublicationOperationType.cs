namespace Ventagram.Models;

public enum PublicationOperationType : byte
{
    Venta = 1,
    Alquiler = 2,
    Temporario = 3,
    Permuta = 4,
    Financiacion = 5
}

public static class PublicationOperationTypeExtensions
{
    public static string ToDisplayName(this PublicationOperationType value)
    {
        return value switch
        {
            PublicationOperationType.Venta => "Venta",
            PublicationOperationType.Alquiler => "Alquiler",
            PublicationOperationType.Temporario => "Temporario",
            PublicationOperationType.Permuta => "Permuta",
            PublicationOperationType.Financiacion => "Financiacion",
            _ => value.ToString()
        };
    }

    public static string ToBadgeLetter(this PublicationOperationType value)
    {
        return value.ToDisplayName()[0].ToString().ToUpperInvariant();
    }

    public static PublicationOperationType? ParseOrNull(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "venta" => PublicationOperationType.Venta,
            "alquiler" => PublicationOperationType.Alquiler,
            "temporario" => PublicationOperationType.Temporario,
            "permuta" => PublicationOperationType.Permuta,
            "financiacion" => PublicationOperationType.Financiacion,
            _ => null
        };
    }
}
