namespace Ventagram.Models;

public static class PublicationGroupExtensions
{
    public static string ToAdCodePrefix(this PublicationGroup group)
    {
        return group switch
        {
            PublicationGroup.Inmuebles => "INM",
            PublicationGroup.Rodados => "ROD",
            PublicationGroup.Electronica => "ELE",
            PublicationGroup.Generales => "GEN",
            PublicationGroup.Embarcaciones => "EMB",
            PublicationGroup.Agro => "AGR",
            PublicationGroup.Moda => "MOD",
            _ => "PUB"
        };
    }

    public static string ToAdCode(this Publication publication)
    {
        if (publication is null)
        {
            return string.Empty;
        }

        return $"{publication.Group.ToAdCodePrefix()}-{publication.Id:D3}";
    }

    public static string ToDisplayName(this PublicationGroup group)
    {
        return group switch
        {
            PublicationGroup.Rodados => "Rodados",
            PublicationGroup.Electronica => "Electronica",
            PublicationGroup.Embarcaciones => "Embarcaciones",
            PublicationGroup.Agro => "Agro",
            PublicationGroup.Generales => "Generales",
            PublicationGroup.Moda => "Moda",
            _ => "Inmuebles"
        };
    }

    public static PublicationGroup ParseOrDefault(string? value, PublicationGroup fallback = PublicationGroup.Inmuebles)
    {
        if (byte.TryParse(value, out var byteValue) && Enum.IsDefined(typeof(PublicationGroup), byteValue))
        {
            return (PublicationGroup)byteValue;
        }

        if (Enum.TryParse<PublicationGroup>(value, true, out var byName))
        {
            return byName;
        }

        return value?.Trim() switch
        {
            "Rodados" => PublicationGroup.Rodados,
            "Electronica" => PublicationGroup.Electronica,
            "Electrónica" => PublicationGroup.Electronica,
            "Generales" => PublicationGroup.Generales,
            "Moda" => PublicationGroup.Moda,
            "Embarcaciones" => PublicationGroup.Embarcaciones,
            "Lanchas" => PublicationGroup.Embarcaciones,
            "Agro" => PublicationGroup.Agro,
            "Inmuebles" => PublicationGroup.Inmuebles,
            _ => fallback
        };
    }
}
