using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VentaMap.Data;
using VentaMap.Models;

var config = new ConfigurationBuilder()
    .SetBasePath(Path.GetFullPath(".."))
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var connectionString = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing connection string.");

var optionsBuilder = new DbContextOptionsBuilder<VentaMapDbContext>();
optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

await using var db = new VentaMapDbContext(optionsBuilder.Options);
var user = await db.Users.OrderBy(x => x.Id).FirstOrDefaultAsync()
    ?? throw new InvalidOperationException("No users found to assign publication.");

var images = new[]
{
    "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1494526585095-c41746248156?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1560185007-cde436f6a4d0?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1600585154205-2d6b1f2211b7?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1600047509807-ba8f99d2cdde?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1600607687644-aac4c3eac7f4?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1600566753086-00f18fb6b3ea?auto=format&fit=crop&w=1200&q=80",
    "https://images.unsplash.com/photo-1600607687126-8a3414349a51?auto=format&fit=crop&w=1200&q=80"
};

var publication = new Publication
{
    Group = PublicationGroup.Inmuebles,
    Category = "Terreno",
    Title = $"Inmueble prueba 10 fotos {DateTime.UtcNow:yyyyMMddHHmmss}",
    Price = 125000,
    Currency = "USD",
    Locality = "Candioti Norte",
    ShortDescription = "Publicacion de prueba con 10 fotos para validar galeria.",
    LongDescription = "Alta tecnica creada desde Codex para probar el detalle y popup con diez imagenes consistentes.",
    ImagesCsv = string.Join(",", images),
    ContactName = user.Name,
    ContactPhone = user.Phone,
    ContactEmail = user.Email,
    Status = "Activa",
    Featured = false,
    IsActive = true,
    CreatedAtUtc = DateTime.UtcNow,
    Latitude = -31.6238,
    Longitude = -60.6903,
    UserId = user.Id,
    PropertyDetail = new PropertyDetail
    {
        PropertyType = "Terreno",
        Operation = "Venta",
        Zone = "Candioti Norte",
        TotalAreaM2 = 420,
        CoveredAreaM2 = null,
        RoomsOrBedrooms = "Terreno",
        Bathrooms = 0,
        Address = "Candioti Norte",
        GarageSpaces = 0,
        AgeYears = null,
        Expenses = null,
        Condition = "Muy bueno",
        MortgageEligible = false,
        ProfessionalUseAllowed = false,
        Services = "Agua, Luz",
        Amenities = "Lote cercado"
    }
};

db.Publications.Add(publication);
await db.SaveChangesAsync();

Console.WriteLine($"Created publication ID={publication.Id}");
Console.WriteLine(publication.Title);
Console.WriteLine(publication.ImagesCsv.Split(',').Length);
