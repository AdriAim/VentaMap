using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VentaMap.Data;

var config = new ConfigurationBuilder()
    .SetBasePath(Path.GetFullPath(".."))
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var connectionString = config.GetConnectionString("DefaultConnection");
var optionsBuilder = new DbContextOptionsBuilder<VentaMapDbContext>();
optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

await using var db = new VentaMapDbContext(optionsBuilder.Options);
var rows = await db.Publications
    .Where(x => x.Title.Contains("Candioti Norte"))
    .OrderByDescending(x => x.Id)
    .Select(x => new { x.Id, x.Title, x.ImagesCsv })
    .Take(5)
    .ToListAsync();

foreach (var row in rows)
{
    Console.WriteLine($"ID={row.Id}");
    Console.WriteLine(row.Title);
    Console.WriteLine(row.ImagesCsv);
    Console.WriteLine("---");
}
