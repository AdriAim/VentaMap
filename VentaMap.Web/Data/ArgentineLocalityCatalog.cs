using VentaMap.Models;

namespace VentaMap.Data;

public static class ArgentineLocalityCatalog
{
    public static IReadOnlyList<ArgentineLocality> All { get; } =
    [
        Create("Buenos Aires", "Ciudad Autonoma de Buenos Aires", -34.6037, -58.3816, 1),
        Create("La Plata", "Buenos Aires", -34.9214, -57.9544, 2),
        Create("Mar del Plata", "Buenos Aires", -38.0055, -57.5426, 3),
        Create("Bahia Blanca", "Buenos Aires", -38.7196, -62.2724, 4),
        Create("Tandil", "Buenos Aires", -37.3217, -59.1332, 5),
        Create("San Fernando del Valle de Catamarca", "Catamarca", -28.4696, -65.7852, 6),
        Create("Resistencia", "Chaco", -27.4514, -58.9867, 7),
        Create("Comodoro Rivadavia", "Chubut", -45.8641, -67.4966, 8),
        Create("Trelew", "Chubut", -43.2490, -65.3094, 9),
        Create("Cordoba", "Cordoba", -31.4201, -64.1888, 10),
        Create("Villa Carlos Paz", "Cordoba", -31.4241, -64.4978, 11),
        Create("Rio Cuarto", "Cordoba", -33.1307, -64.3499, 12),
        Create("Corrientes", "Corrientes", -27.4691, -58.8306, 13),
        Create("Parana", "Entre Rios", -31.7319, -60.5238, 14),
        Create("Concordia", "Entre Rios", -31.3929, -58.0209, 15),
        Create("Formosa", "Formosa", -26.1775, -58.1781, 16),
        Create("San Salvador de Jujuy", "Jujuy", -24.1858, -65.2995, 17),
        Create("Santa Rosa", "La Pampa", -36.6203, -64.2900, 18),
        Create("La Rioja", "La Rioja", -29.4131, -66.8558, 19),
        Create("Mendoza", "Mendoza", -32.8895, -68.8458, 20),
        Create("San Rafael", "Mendoza", -34.6177, -68.3301, 21),
        Create("Posadas", "Misiones", -27.3621, -55.9009, 22),
        Create("Neuquen", "Neuquen", -38.9516, -68.0591, 23),
        Create("San Carlos de Bariloche", "Rio Negro", -41.1335, -71.3103, 24),
        Create("Viedma", "Rio Negro", -40.8135, -62.9967, 25),
        Create("Salta", "Salta", -24.7821, -65.4232, 26),
        Create("San Juan", "San Juan", -31.5375, -68.5364, 27),
        Create("San Luis", "San Luis", -33.2950, -66.3356, 28),
        Create("Rio Gallegos", "Santa Cruz", -51.6230, -69.2168, 29),
        Create("Rosario", "Santa Fe", -32.9442, -60.6505, 30),
        Create("Santa Fe", "Santa Fe", -31.6333, -60.7000, 31),
        Create("Santiago del Estero", "Santiago del Estero", -27.7951, -64.2615, 32),
        Create("Ushuaia", "Tierra del Fuego", -54.8019, -68.3030, 33),
        Create("San Miguel de Tucuman", "Tucuman", -26.8083, -65.2176, 34)
    ];

    private static ArgentineLocality Create(string locality, string province, double latitude, double longitude, int sortOrder)
    {
        return new ArgentineLocality
        {
            Locality = locality,
            Province = province,
            Latitude = latitude,
            Longitude = longitude,
            SortOrder = sortOrder,
            IsActive = true
        };
    }
}
