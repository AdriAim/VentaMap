using Microsoft.EntityFrameworkCore;
using Ventagram.Models;
using Ventagram.Services;

namespace Ventagram.Data;

public static class SeedData
{
    private const int TargetPublicationsPerGroup = 50;
    private static readonly PublicationGroupType[] PublicationGroupCatalog =
    [
        new() { Id = (byte)PublicationGroup.Inmuebles, Name = "Inmuebles", SortOrder = 1, IsActive = true },
        new() { Id = (byte)PublicationGroup.Rodados, Name = "Rodados", SortOrder = 2, IsActive = true },
        new() { Id = (byte)PublicationGroup.Embarcaciones, Name = "Embarcaciones", SortOrder = 3, IsActive = true },
        new() { Id = (byte)PublicationGroup.Agro, Name = "Agro", SortOrder = 4, IsActive = true },
        new() { Id = (byte)PublicationGroup.Electronica, Name = "Electronica", SortOrder = 5, IsActive = true },
        new() { Id = (byte)PublicationGroup.Generales, Name = "Generales", SortOrder = 6, IsActive = true },
        new() { Id = (byte)PublicationGroup.Moda, Name = "Moda", SortOrder = 7, IsActive = true }
    ];
    private static readonly PublicationReportReason[] PublicationReportReasonCatalog =
    [
        new() { Id = 1, Name = "Spam/Publicidad", SortOrder = 1, IsActive = true },
        new() { Id = 2, Name = "Fraude", SortOrder = 2, IsActive = true },
        new() { Id = 3, Name = "Precio no corresponde", SortOrder = 3, IsActive = true },
        new() { Id = 4, Name = "Foto no corresponde", SortOrder = 4, IsActive = true },
        new() { Id = 5, Name = "Ubicacion erronea", SortOrder = 5, IsActive = true }
    ];
    private static readonly (PublicationGroup Group, string[] Categories)[] PublicationCategoryCatalog =
    [
        (PublicationGroup.Inmuebles,
        [
            "Departamento",
            "Casa",
            "PH",
            "Terreno",
            "Local comercial",
            "Campo",
            "Quinta vacacional",
            "Oficina comercial",
            "Cochera",
            "Bodega-Galpón",
            "Fondo de comercio",
            "Hotel",
            "Depósito",
            "Bóveda, nicho o parcela",
            "Cama náutica",
            "Consultorio",
            "Edificio",
            "Desarrollo horizontal",
            "Desarrollo vertical"
        ]),
        (PublicationGroup.Rodados,
        [
            "Autos",
            "Utilitarios y Camionetas",
            "Motos",
            "Planes",
            "Cuatris",
            "Camiones",
            "Otros"
        ]),
        (PublicationGroup.Electronica,
        [
            "Celulares y Telefonos",
            "Computacion",
            "Consolas y Videojuegos",
            "Camaras y Accesorios",
            "Electronica, Audio y Video",
            "Otros"
        ]),
        (PublicationGroup.Generales,
        [
            "Muebles",
            "Bicicletas",
            "Hogar y Jardin",
            "Herramientas",
            "Deportes y Fitness",
            "Ropa y Accesorios",
            "Juguetes y Bebes",
            "Instrumentos Musicales",
            "Libros y Coleccionables",
            "Otros"
        ]),
        (PublicationGroup.Moda,
        [
            "Indumentaria mujer",
            "Indumentaria hombre",
            "Indumentaria infantil",
            "Calzado",
            "Carteras y bolsos",
            "Accesorios",
            "Relojes y joyas",
            "Ropa deportiva",
            "Uniformes y ropa de trabajo",
            "Lotes de ropa",
            "Otros"
        ]),
        (PublicationGroup.Embarcaciones,
        [
            "Lanchas",
            "Veleros",
            "Motos de agua",
            "Botes y semirrigidos",
            "Accesorios nauticos"
        ]),
        (PublicationGroup.Agro,
        [
            "Animales",
            "Generadores de Energia",
            "Infraestructura Rural",
            "Insumos Agricolas",
            "Insumos Ganaderos",
            "Maquinas y Herramientas",
            "Otros",
            "Repuestos Maquinaria Agricola"
        ])
    ];
    private static readonly Dictionary<PublicationGroup, CategoryFieldSeed[]> CategoryFieldCatalog = new()
    {
        [PublicationGroup.Inmuebles] =
        [
            new("operacion", "Tipo de operacion", PublicationCategoryFieldDataType.Lista, true, 1,null,"Venta,Alquiler,Temporario"),
            new("zona", "Zona", PublicationCategoryFieldDataType.Texto, false, 2),
            new("superficie_total_m2", "Superficie total", PublicationCategoryFieldDataType.Numero, false, 3, "m2"),
            new("superficie_cubierta_m2", "Superficie cubierta", PublicationCategoryFieldDataType.Numero, false, 4, "m2"),
            new("ambientes", "Ambientes", PublicationCategoryFieldDataType.Texto, false, 5),
            new("banios", "Banios", PublicationCategoryFieldDataType.Numero, false, 6),
            new("direccion", "Direccion", PublicationCategoryFieldDataType.Texto, false, 7),
            new("garage", "Cochera", PublicationCategoryFieldDataType.Numero, false, 8),
            new("antiguedad_anios", "Antiguedad", PublicationCategoryFieldDataType.Numero, false, 9, "anios"),
            new("expensas", "Expensas", PublicationCategoryFieldDataType.Numero, false, 10, "ARS"),
            new("estado", "Estado", PublicationCategoryFieldDataType.Texto, false, 11),
            new("apto_credito", "Apto credito", PublicationCategoryFieldDataType.Booleano, false, 12),
            new("uso_profesional", "Uso profesional", PublicationCategoryFieldDataType.Booleano, false, 13),
            new("servicios", "Servicios", PublicationCategoryFieldDataType.Texto, false, 14),
            new("amenities", "Amenities", PublicationCategoryFieldDataType.Texto, false, 15)
        ],
        [PublicationGroup.Rodados] =
        [
            new("operacion", "Tipo de operacion", PublicationCategoryFieldDataType.Lista, true, 1, null, "Venta,Permuta,Financiacion"),
            new("tipo_vehiculo", "Tipo de vehiculo", PublicationCategoryFieldDataType.Lista, true, 2),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, true, 3),
            new("modelo", "Modelo", PublicationCategoryFieldDataType.Texto, true, 4),
            new("anio", "Anio", PublicationCategoryFieldDataType.Numero, true, 5),
            new("kilometros", "Kilometros", PublicationCategoryFieldDataType.Numero, false, 6, "km"),
            new("combustible", "Combustible", PublicationCategoryFieldDataType.Texto, false, 7),
            new("transmision", "Transmision", PublicationCategoryFieldDataType.Texto, false, 8),
            new("version", "Version", PublicationCategoryFieldDataType.Texto, false, 9),
            new("color", "Color", PublicationCategoryFieldDataType.Texto, false, 10),
            new("patente", "Patente", PublicationCategoryFieldDataType.Texto, false, 11),
            new("motor", "Motor", PublicationCategoryFieldDataType.Texto, false, 12),
            new("traccion", "Traccion", PublicationCategoryFieldDataType.Texto, false, 13),
            new("puertas", "Puertas", PublicationCategoryFieldDataType.Numero, false, 14),
            new("titulares", "Titulares", PublicationCategoryFieldDataType.Numero, false, 15),
            new("permuta", "Permuta", PublicationCategoryFieldDataType.Booleano, false, 16),
            new("financiacion", "Financiacion", PublicationCategoryFieldDataType.Booleano, false, 17),
            new("equipamiento", "Equipamiento", PublicationCategoryFieldDataType.Texto, false, 18),
            new("estado_general", "Estado general", PublicationCategoryFieldDataType.Texto, false, 19)
        ],
        [PublicationGroup.Electronica] =
        [
            new("operacion", "Tipo de operacion", PublicationCategoryFieldDataType.Lista, true, 1, null, "Venta,Permuta"),
            new("subcategoria", "Subcategoria", PublicationCategoryFieldDataType.Texto, true, 2),
            new("estado_articulo", "Estado", PublicationCategoryFieldDataType.Texto, false, 3),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, false, 4),
            new("modelo", "Modelo", PublicationCategoryFieldDataType.Texto, false, 5),
            new("sku", "SKU", PublicationCategoryFieldDataType.Texto, false, 6),
            new("stock", "Stock", PublicationCategoryFieldDataType.Numero, false, 7, "unid"),
            new("color", "Color", PublicationCategoryFieldDataType.Texto, false, 8),
            new("medida", "Medida", PublicationCategoryFieldDataType.Texto, false, 9),
            new("peso", "Peso", PublicationCategoryFieldDataType.Texto, false, 10),
            new("dimensiones", "Dimensiones", PublicationCategoryFieldDataType.Texto, false, 11),
            new("garantia", "Garantia", PublicationCategoryFieldDataType.Texto, false, 12),
            new("envio", "Envio", PublicationCategoryFieldDataType.Texto, false, 13),
            new("permuta", "Permuta", PublicationCategoryFieldDataType.Booleano, false, 14)
        ],
        [PublicationGroup.Generales] =
        [
            new("operacion", "Tipo de operacion", PublicationCategoryFieldDataType.Lista, true, 1, null, "Venta,Permuta"),
            new("estado_articulo", "Estado", PublicationCategoryFieldDataType.Texto, false, 2),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, false, 3),
            new("modelo", "Modelo", PublicationCategoryFieldDataType.Texto, false, 4),
            new("sku", "SKU", PublicationCategoryFieldDataType.Texto, false, 5),
            new("stock", "Stock", PublicationCategoryFieldDataType.Numero, false, 6, "unid"),
            new("color", "Color", PublicationCategoryFieldDataType.Texto, false, 7),
            new("medida", "Medida", PublicationCategoryFieldDataType.Texto, false, 8),
            new("peso", "Peso", PublicationCategoryFieldDataType.Texto, false, 9),
            new("dimensiones", "Dimensiones", PublicationCategoryFieldDataType.Texto, false, 10),
            new("garantia", "Garantia", PublicationCategoryFieldDataType.Texto, false, 11),
            new("envio", "Envio", PublicationCategoryFieldDataType.Texto, false, 12),
            new("permuta", "Permuta", PublicationCategoryFieldDataType.Booleano, false, 13)
        ],
        [PublicationGroup.Moda] =
        [
            new("operacion", "Tipo de operacion", PublicationCategoryFieldDataType.Lista, true, 1, null, "Venta,Permuta"),
            new("genero", "Genero", PublicationCategoryFieldDataType.Lista, false, 2, null, "Mujer,Hombre,Unisex,Infantil"),
            new("talle", "Talle", PublicationCategoryFieldDataType.Texto, false, 3),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, false, 4),
            new("estado_articulo", "Estado", PublicationCategoryFieldDataType.Texto, false, 5),
            new("color", "Color", PublicationCategoryFieldDataType.Texto, false, 6),
            new("material", "Material", PublicationCategoryFieldDataType.Texto, false, 7),
            new("temporada", "Temporada", PublicationCategoryFieldDataType.Texto, false, 8),
            new("envio", "Envio", PublicationCategoryFieldDataType.Texto, false, 9),
            new("permuta", "Permuta", PublicationCategoryFieldDataType.Booleano, false, 10)
        ],
        [PublicationGroup.Embarcaciones] =
        [
            new("operacion", "Tipo de operacion", PublicationCategoryFieldDataType.Lista, true, 1, null, "Venta,Alquiler,Permuta"),
            new("tipo_embarcacion", "Tipo de embarcacion", PublicationCategoryFieldDataType.Texto, true, 2),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, false, 3),
            new("modelo", "Modelo", PublicationCategoryFieldDataType.Texto, false, 4),
            new("anio", "Anio", PublicationCategoryFieldDataType.Numero, false, 5),
            new("eslora", "Eslora", PublicationCategoryFieldDataType.Texto, false, 6),
            new("motor", "Motor", PublicationCategoryFieldDataType.Texto, false, 7)
        ],
        [PublicationGroup.Agro] =
        [
            new("operacion", "Tipo de operacion", PublicationCategoryFieldDataType.Lista, true, 1, null, "Venta,Alquiler,Permuta"),
            new("tipo_agro", "Tipo agro", PublicationCategoryFieldDataType.Texto, true, 2),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, false, 3),
            new("modelo", "Modelo", PublicationCategoryFieldDataType.Texto, false, 4),
            new("anio", "Anio", PublicationCategoryFieldDataType.Numero, false, 5),
            new("estado", "Estado", PublicationCategoryFieldDataType.Texto, false, 6),
            new("horas_uso", "Horas de uso", PublicationCategoryFieldDataType.Numero, false, 7, "hs"),
            new("potencia", "Potencia", PublicationCategoryFieldDataType.Texto, false, 8),
            new("stock", "Stock", PublicationCategoryFieldDataType.Numero, false, 9, "unid")
        ]
    };
    private static readonly string[] PropertyGalleryPool =
    [
        "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1600585154526-990dced4db0d?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1494526585095-c41746248156?auto=format&fit=crop&w=1200&q=80"
    ];
    private static readonly string[] VehicleGalleryPool =
    [
        "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1558981806-ec527fa84c39?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1553440569-bcc63803a83d?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1511919884226-fd3cad34687c?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1485463611174-f302f6a5c1c9?auto=format&fit=crop&w=1200&q=80"
    ];
    private static readonly string[] BoatGalleryPool =
    [
        "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_315_motorboat%2C_Kivik.jpg",
        "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_35_motorboat%2C_Kivik.jpg",
        "https://commons.wikimedia.org/wiki/Special:FilePath/Small_yacht.jpg",
        "https://commons.wikimedia.org/wiki/Special:FilePath/Yacht-attessa-2009-sf.jpg",
        "https://commons.wikimedia.org/wiki/Special:FilePath/Bayliner_175%2C_stern_view.JPG"
    ];
    private static readonly string[] GeneralGalleryPool =
    [
        "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1541625602330-2277a4c46182?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1606813907291-d86efa9b94db?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1584568694244-14fbdf83bd30?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1504148455328-c376907d081c?auto=format&fit=crop&w=1200&q=80"
    ];

    public static async Task InitializeAsync(VentagramDbContext db)
    {
        await EnsurePublicationGroupsAsync(db);
        await EnsurePublicationCategoriesAsync(db);
        await EnsurePublicationCategoryFieldsAsync(db);
        await EnsurePublicationReportReasonsAsync(db);

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == "demo@ventagram.local");
        if (user is null)
        {
            user = new ApplicationUser
            {
                Name = "VentaMap Demo",
                Email = "demo@ventagram.local",
                Phone = "3515550101",
                PasswordHash = AuthService.HashPassword("Demo1234!")
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var companyUser = await EnsureFictionalRealEstateCompanyAsync(db);
        var autoCompanyUser = await EnsureFictionalAutoCompanyAsync(db);
        var boatCompanyUser = await EnsureFictionalBoatCompanyAsync(db);

        var existingTitles = new HashSet<string>(await db.Publications
            .Select(x => x.Title)
            .ToListAsync(), StringComparer.OrdinalIgnoreCase);
        var existingCounts = await db.Publications
            .GroupBy(x => x.Group)
            .Select(x => new { Group = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Group, x => x.Count);

        var categoryLookup = await BuildCategoryLookupAsync(db);
        var catalog = BuildCatalog(user, categoryLookup);
        var available = catalog
            .Where(x => !existingTitles.Contains(x.Title))
            .ToList();

        var missing = new List<Publication>();
        foreach (var group in new[] { PublicationGroup.Inmuebles, PublicationGroup.Rodados, PublicationGroup.Electronica, PublicationGroup.Generales })
        {
            var current = existingCounts.TryGetValue(group, out var count) ? count : 0;
            var needed = Math.Max(0, TargetPublicationsPerGroup - current);
            if (needed == 0)
            {
                continue;
            }

            var selected = available
                .Where(x => x.Group == group)
                .Take(needed)
                .ToList();

            if (selected.Count < needed)
            {
                throw new InvalidOperationException(
                    $"No hay suficientes publicaciones candidatas para completar {TargetPublicationsPerGroup} registros del grupo {group}.");
            }

            missing.AddRange(selected);
            available.RemoveAll(x => selected.Contains(x));
        }

        if (missing.Count > 0)
        {
            db.Publications.AddRange(missing);
            await db.SaveChangesAsync();
            foreach (var title in missing.Select(x => x.Title))
            {
                existingTitles.Add(title);
            }
        }

        var companyCatalog = BuildCompanyRealEstateCatalog(companyUser, categoryLookup);
        var missingCompanyPublications = companyCatalog
            .Where(x => !existingTitles.Contains(x.Title))
            .ToList();

        if (missingCompanyPublications.Count > 0)
        {
            db.Publications.AddRange(missingCompanyPublications);
            await db.SaveChangesAsync();
        }

        var autoCatalog = BuildCompanyAutoCatalog(autoCompanyUser, categoryLookup);
        var missingAutoPublications = autoCatalog
            .Where(x => !existingTitles.Contains(x.Title))
            .ToList();

        if (missingAutoPublications.Count > 0)
        {
            db.Publications.AddRange(missingAutoPublications);
            await db.SaveChangesAsync();
            foreach (var title in missingAutoPublications.Select(x => x.Title))
            {
                existingTitles.Add(title);
            }
        }

        var boatCatalog = BuildCompanyBoatCatalog(boatCompanyUser, categoryLookup);
        var missingBoatPublications = boatCatalog
            .Where(x => !existingTitles.Contains(x.Title))
            .ToList();

        if (missingBoatPublications.Count > 0)
        {
            db.Publications.AddRange(missingBoatPublications);
            await db.SaveChangesAsync();
        }

        await BackfillDynamicFieldValuesAsync(db);
        await BackfillGalleryImagesAsync(db, user.Id, companyUser.Id, autoCompanyUser.Id, boatCompanyUser.Id);
    }

    private static async Task EnsurePublicationGroupsAsync(VentagramDbContext db)
    {
        var existing = await db.PublicationGroupTypes.ToListAsync();
        var existingById = existing.ToDictionary(x => x.Id);
        var missing = new List<PublicationGroupType>();
        var hasChanges = false;

        foreach (var group in PublicationGroupCatalog)
        {
            if (existingById.TryGetValue(group.Id, out var current))
            {
                if (current.Name != group.Name
                    || current.SortOrder != group.SortOrder
                    || current.IsActive != group.IsActive)
                {
                    current.Name = group.Name;
                    current.SortOrder = group.SortOrder;
                    current.IsActive = group.IsActive;
                    hasChanges = true;
                }

                continue;
            }

            missing.Add(new PublicationGroupType
            {
                Id = group.Id,
                Name = group.Name,
                SortOrder = group.SortOrder,
                IsActive = group.IsActive
            });
        }

        if (missing.Count > 0)
        {
            db.PublicationGroupTypes.AddRange(missing);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsurePublicationCategoriesAsync(VentagramDbContext db)
    {
        var legacyGarageCategory = await db.PublicationCategories
            .FirstOrDefaultAsync(x => x.Group == PublicationGroup.Inmuebles && x.Name == "Garage");
        var existingCocheraCategory = await db.PublicationCategories
            .FirstOrDefaultAsync(x => x.Group == PublicationGroup.Inmuebles && x.Name == "Cochera");
        var garageRenamedInMemory = false;

        if (legacyGarageCategory is not null && existingCocheraCategory is not null && legacyGarageCategory.Id != existingCocheraCategory.Id)
        {
            await db.Publications
                .Where(x => x.CategoryId == legacyGarageCategory.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.CategoryId, existingCocheraCategory.Id));

            await db.PublicationCategoryFields
                .Where(x => x.CategoryId == legacyGarageCategory.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.CategoryId, existingCocheraCategory.Id));

            db.PublicationCategories.Remove(legacyGarageCategory);
            await db.SaveChangesAsync();
            legacyGarageCategory = null;
        }

        if (legacyGarageCategory is not null)
        {
            legacyGarageCategory.Name = "Cochera";
            legacyGarageCategory.IsActive = true;
            garageRenamedInMemory = true;
        }

        var existingKeys = new HashSet<string>(
            await db.PublicationCategories
                .Select(x => $"{(byte)x.Group}|{x.Name}")
                .ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        if (garageRenamedInMemory)
        {
            existingKeys.Remove($"{(byte)PublicationGroup.Inmuebles}|Garage");
            existingKeys.Add($"{(byte)PublicationGroup.Inmuebles}|Cochera");
        }

        var existingCategories = await db.PublicationCategories
            .Where(x => x.Group == PublicationGroup.Electronica)
            .ToListAsync();
        var desiredElectronicCategoryNames = PublicationCategoryCatalog
            .Where(x => x.Group == PublicationGroup.Electronica)
            .SelectMany(x => x.Categories)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasElectronicCategoryChanges = false;

        foreach (var existingCategory in existingCategories)
        {
            var shouldBeActive = desiredElectronicCategoryNames.Contains(existingCategory.Name);
            if (existingCategory.IsActive != shouldBeActive)
            {
                existingCategory.IsActive = shouldBeActive;
                hasElectronicCategoryChanges = true;
            }
        }

        var missing = new List<PublicationCategory>();
        foreach (var (group, categories) in PublicationCategoryCatalog)
        {
            for (var index = 0; index < categories.Length; index++)
            {
                var name = categories[index];
                var key = $"{(byte)group}|{name}";
                if (existingKeys.Contains(key))
                {
                    var current = existingCategories.FirstOrDefault(x =>
                        x.Group == group
                        && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (current is not null && current.SortOrder != index + 1)
                    {
                        current.SortOrder = index + 1;
                        hasElectronicCategoryChanges = true;
                    }

                    continue;
                }

                missing.Add(new PublicationCategory
                {
                    Group = group,
                    Name = name,
                    SortOrder = index + 1,
                    IsActive = true
                });
            }
        }

        if (missing.Count == 0)
        {
            if (legacyGarageCategory is not null || hasElectronicCategoryChanges)
            {
                await db.SaveChangesAsync();
            }

            return;
        }

        db.PublicationCategories.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static async Task EnsurePublicationReportReasonsAsync(VentagramDbContext db)
    {
        var existingIds = await db.PublicationReportReasons
            .Select(x => x.Id)
            .ToListAsync();

        var missing = PublicationReportReasonCatalog
            .Where(x => !existingIds.Contains(x.Id))
            .Select(x => new PublicationReportReason
            {
                Id = x.Id,
                Name = x.Name,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.PublicationReportReasons.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static async Task EnsurePublicationCategoryFieldsAsync(VentagramDbContext db)
    {
        var existing = await db.PublicationCategoryFields
            .Where(x => x.CategoryId == null && x.GroupId != null)
            .ToListAsync();

        var existingByKey = existing.ToDictionary(
            x => $"{x.GroupId}|{x.InternalName}",
            StringComparer.OrdinalIgnoreCase);

        var hasChanges = false;
        foreach (var (group, fields) in CategoryFieldCatalog)
        {
            foreach (var field in fields)
            {
                var key = $"{(byte)group}|{field.InternalName}";
                if (existingByKey.TryGetValue(key, out var current))
                {
                    if (current.Label != field.Label
                        || current.DataType != field.DataType
                        || current.Required != field.Required
                        || current.SortOrder != field.SortOrder
                        || current.IsActive != true
                        || current.OptionsCsv != field.OptionsCsv
                        || current.Unit != field.Unit
                        || current.ShowInBasicData != field.Required)
                    {
                        current.Label = field.Label;
                        current.DataType = field.DataType;
                        current.Required = field.Required;
                        current.SortOrder = field.SortOrder;
                        current.IsActive = true;
                        current.OptionsCsv = field.OptionsCsv;
                        current.Unit = field.Unit;
                        current.ShowInBasicData = field.Required;
                        hasChanges = true;
                    }

                    continue;
                }

                db.PublicationCategoryFields.Add(new PublicationCategoryField
                {
                    GroupId = (byte)group,
                    CategoryId = null,
                    InternalName = field.InternalName,
                    Label = field.Label,
                    DataType = field.DataType,
                    Required = field.Required,
                    SortOrder = field.SortOrder,
                    IsActive = true,
                    OptionsCsv = field.OptionsCsv,
                    Unit = field.Unit,
                    ShowInBasicData = field.Required
                });
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync();
        }
    }

    private static List<Publication> BuildCatalog(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var catalog = new List<Publication>
        {
            CreateProperty(
                user,
                categoryLookup,
                "Depto 2 ambientes con balcon en Nueva Cordoba",
                "Departamentos",
                "Cordoba",
                93000,
                "Luminoso, reciclado y listo para entrar.",
                "A 4 cuadras del Patio Olmos, cocina integrada y balcon al frente.",
                "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
                -31.4201, -64.1888,
                "Departamento", "Venta", "Nueva Cordoba", 58, 52, "2 ambientes", 1, 0, "Muy bueno",
                "Gas natural, Internet", "Balcon, Ascensor, Terraza",
                ("Apto credito", "Si")),
            CreateProperty(
                user,
                categoryLookup,
                "Casa 3 dormitorios con patio en Yerba Buena",
                "Casas",
                "Tucuman",
                145000,
                "Amplia, luminosa y con quincho.",
                "Casa familiar con jardin, galeria y cochera doble en zona residencial.",
                "https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=1200&q=80",
                -26.8167, -65.2833,
                "Casa", "Venta", "Yerba Buena", 240, 180, "3 dormitorios", 2, 2, "Excelente",
                "Agua, Gas, Internet", "Quincho, Jardin, Parrilla"),
            CreateProperty(
                user,
                categoryLookup,
                "Lote en barrio cerrado Las Tipas",
                "Terrenos",
                "Rosario",
                38000,
                "Lote interno listo para escriturar.",
                "Buen acceso, seguridad 24 hs y amenities del barrio disponibles.",
                "https://images.unsplash.com/photo-1500382017468-9049fed747ef?auto=format&fit=crop&w=1200&q=80",
                -32.8800, -60.7000,
                "Lote", "Venta", "Funes", 620, null, "Terreno", 0, 0, "Muy bueno",
                "Electricidad, Agua", "Seguridad, Club House"),
            CreateProperty(
                user,
                categoryLookup,
                "PH reciclado de 2 dormitorios en La Plata",
                "PH",
                "La Plata",
                79000,
                "Sin expensas, patio propio y cocina nueva.",
                "Ideal primera vivienda, cerca del centro comercial y transporte.",
                "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80",
                -34.9214, -57.9544,
                "PH", "Venta", "Centro", 92, 78, "2 dormitorios", 1, 0, "Reciclado",
                "Agua, Cloacas, Gas", "Patio"),
            CreateProperty(
                user,
                categoryLookup,
                "Monoambiente amoblado para alquiler temporal",
                "Departamentos",
                "Buenos Aires",
                550,
                "Equipado y listo para ingresar.",
                "Incluye wifi, ropa blanca y expensas en Palermo Hollywood.",
                "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
                -34.5895, -58.4300,
                "Departamento", "Alquiler", "Palermo", 36, 34, "Monoambiente", 1, 0, "Excelente",
                "Internet, Agua", "Laundry, Terraza"),
            CreateProperty(
                user,
                categoryLookup,
                "Duplex a estrenar en Godoy Cruz",
                "Duplex",
                "Mendoza",
                118000,
                "Dos plantas, cochera y patio seco.",
                "Ubicado en barrio tranquilo con rapido acceso al centro.",
                "https://images.unsplash.com/photo-1600585154526-990dced4db0d?auto=format&fit=crop&w=1200&q=80",
                -32.9286, -68.8440,
                "Duplex", "Venta", "Godoy Cruz", 140, 110, "3 dormitorios", 2, 1, "A estrenar",
                "Gas, Agua, Internet", "Patio, Cochera"),
            CreateProperty(
                user,
                categoryLookup,
                "Oficina premium en microcentro",
                "Oficinas",
                "Cordoba",
                68000,
                "Recepcion, sala de reuniones y seguridad.",
                "Apta profesional, excelente luminosidad y expensas moderadas.",
                "https://images.unsplash.com/photo-1497366811353-6870744d04b2?auto=format&fit=crop&w=1200&q=80",
                -31.4167, -64.1833,
                "Oficina", "Venta", "Microcentro", 74, 74, "Planta libre", 1, 1, "Muy bueno",
                "Internet, Luz, Agua", "Seguridad, Recepcion"),
            CreateProperty(
                user,
                categoryLookup,
                "Campo de 12 hectareas con mejora",
                "Campos",
                "Entre Rios",
                210000,
                "Apto agricultura y fin de semana.",
                "Tiene alambrado, perforacion y casa chica de apoyo.",
                "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=1200&q=80",
                -31.7319, -60.5238,
                "Campo", "Venta", "Parana Campana", 120000, 80, "Campo", 1, 0, "Bueno",
                "Perforacion, Electricidad", "Casa de apoyo"),
            CreateProperty(
                user,
                categoryLookup,
                "Casa quinta con pileta en Canning",
                "Quintas",
                "Buenos Aires",
                189000,
                "Ideal descanso o renta por eventos.",
                "Gran parque arbolado, pileta cercada y quincho completo.",
                "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80",
                -34.8533, -58.5198,
                "Casa quinta", "Venta", "Canning", 1800, 220, "4 ambientes", 3, 3, "Excelente",
                "Luz, Agua, Internet", "Pileta, Quincho, Parque"),
            CreateProperty(
                user,
                categoryLookup,
                "Local comercial sobre avenida principal",
                "Locales",
                "Mar del Plata",
                99000,
                "Vidriera amplia y deposito propio.",
                "Buena circulacion peatonal, apto varios rubros.",
                "https://images.unsplash.com/photo-1441986300917-64674bd600d8?auto=format&fit=crop&w=1200&q=80",
                -38.0055, -57.5426,
                "Local", "Venta", "Guemes", 85, 85, "Salon + deposito", 1, 0, "Muy bueno",
                "Agua, Luz", "Vidriera"),

            CreateVehicle(
                user,
                categoryLookup,
                "Ford Fiesta SE 2018",
                "Autos",
                "Rosario",
                12500,
                "Service al dia, unico dueno.",
                "Nafta, manual, 89.000 km, sensores de estacionamiento y pantalla.",
                "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?auto=format&fit=crop&w=1200&q=80",
                -32.9442, -60.6505,
                "Auto", "Ford", "Fiesta", 2018, 89000, "Nafta", "Manual", "SE", "Gris", "Excelente",
                "Pantalla, Sensores, Llantas",
                ("Sensores", "Si")),
            CreateVehicle(
                user,
                categoryLookup,
                "Toyota Hilux SRX 2021 4x4",
                "Camionetas",
                "Salta",
                36500,
                "Unica mano, impecable y con services oficiales.",
                "Doble cabina, automatica, lista para viajar o trabajar.",
                "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
                -24.7821, -65.4232,
                "Camioneta", "Toyota", "Hilux", 2021, 54000, "Diesel", "Automatica", "SRX", "Blanca", "Excelente",
                "4x4, Cuero, Camara, Navegador"),
            CreateVehicle(
                user,
                categoryLookup,
                "Volkswagen Gol Trend 2016",
                "Autos",
                "Cordoba",
                9800,
                "Economico y muy cuidado.",
                "Aire, direccion, cubiertas nuevas y papeles al dia.",
                "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?auto=format&fit=crop&w=1200&q=80",
                -31.4201, -64.1888,
                "Auto", "Volkswagen", "Gol Trend", 2016, 112000, "Nafta", "Manual", "Pack I", "Rojo", "Muy bueno",
                "Aire, Direccion, Stereo"),
            CreateVehicle(
                user,
                categoryLookup,
                "Honda Wave S 110 2023",
                "Motos",
                "La Plata",
                2400,
                "Primera mano, lista para transferir.",
                "Uso particular, pocos kilometros y service recien hecho.",
                "https://images.unsplash.com/photo-1558981806-ec527fa84c39?auto=format&fit=crop&w=1200&q=80",
                -34.9214, -57.9544,
                "Moto", "Honda", "Wave", 2023, 6400, "Nafta", "Semi", "S 110", "Negra", "Excelente",
                "Alarma, Baulera"),
            CreateVehicle(
                user,
                categoryLookup,
                "Chevrolet Cruze LT 2020",
                "Autos",
                "Buenos Aires",
                18700,
                "Motor turbo, muy equipado.",
                "Automatico, cuero, techo y mantenimiento al dia.",
                "https://images.unsplash.com/photo-1553440569-bcc63803a83d?auto=format&fit=crop&w=1200&q=80",
                -34.6037, -58.3816,
                "Auto", "Chevrolet", "Cruze", 2020, 68000, "Nafta", "Automatica", "LT", "Azul", "Excelente",
                "Cuero, Techo, CarPlay"),
            CreateVehicle(
                user,
                categoryLookup,
                "Renault Kangoo 1.6 furgon 2017",
                "Utilitarios",
                "Mendoza",
                10900,
                "Ideal repartos o trabajo liviano.",
                "Buen estado general, GNC de quinta y mantenimiento reciente.",
                "https://images.unsplash.com/photo-1519641471654-76ce0107ad1b?auto=format&fit=crop&w=1200&q=80",
                -32.8895, -68.8458,
                "Utilitario", "Renault", "Kangoo", 2017, 136000, "Nafta/GNC", "Manual", "Furgon", "Blanco", "Muy bueno",
                "Aire, GNC, Porton lateral"),
            CreateVehicle(
                user,
                categoryLookup,
                "Peugeot 208 Allure 2022",
                "Autos",
                "Santa Fe",
                21400,
                "Casi sin uso, garantia vigente.",
                "Pantalla grande, camara de retroceso y llantas originales.",
                "https://images.unsplash.com/photo-1502877338535-766e1452684a?auto=format&fit=crop&w=1200&q=80",
                -31.6333, -60.7000,
                "Auto", "Peugeot", "208", 2022, 21000, "Nafta", "Manual", "Allure", "Gris", "Excelente",
                "Camara, Pantalla, Llantas"),
            CreateVehicle(
                user,
                categoryLookup,
                "Yamaha MT-03 2021",
                "Motos",
                "Neuquen",
                6900,
                "Titular, escape original y accesorios.",
                "Uso recreativo, sin caidas y con cubiertas en buen estado.",
                "https://images.unsplash.com/photo-1517846693594-1567da72af75?auto=format&fit=crop&w=1200&q=80",
                -38.9516, -68.0591,
                "Moto", "Yamaha", "MT-03", 2021, 18000, "Nafta", "Manual", "ABS", "Azul", "Excelente",
                "ABS, Sliders, Parabrisas"),
            CreateVehicle(
                user,
                categoryLookup,
                "Citroen Berlingo Multispace 2015",
                "Familiares",
                "Parana",
                8700,
                "Amplia, comoda y con mantenimiento hecho.",
                "Ideal familia o trabajo, con gran baul y aire.",
                "https://images.unsplash.com/photo-1552519507-da3b142c6e3d?auto=format&fit=crop&w=1200&q=80",
                -31.7319, -60.5238,
                "Familiar", "Citroen", "Berlingo", 2015, 149000, "Nafta", "Manual", "Multispace", "Bordo", "Bueno",
                "Aire, Direccion, Gran baul"),
            CreateVehicle(
                user,
                categoryLookup,
                "Mercedes Benz Sprinter 415 2019",
                "Utilitarios",
                "Cordoba",
                28900,
                "Lista para trabajar, impecable de mecanica.",
                "Furgon mediano, un solo conductor y kilometraje de ruta.",
                "https://images.unsplash.com/photo-1485463611174-f302f6a5c1c9?auto=format&fit=crop&w=1200&q=80",
                -31.4201, -64.1888,
                "Utilitario", "Mercedes Benz", "Sprinter", 2019, 98000, "Diesel", "Manual", "415", "Blanca", "Muy bueno",
                "Aire, ABS, Cierre centralizado"),

            CreateGeneral(
                user,
                categoryLookup,
                "iPhone 13 128GB",
                PublicationGroup.Electronica,
                "Celulares y Telefonos",
                "Buenos Aires",
                780,
                "Bateria 88%, caja y cable original.",
                "Sin golpes, libre de fabrica. Se prueba al retirar.",
                "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80",
                -34.6037, -58.3816,
                "Celulares", "Usado", "Apple", "iPhone 13", "7 dias de prueba", "Retiro o envio a coordinar",
                ("Memoria", "128GB"),
                ("Bateria", "88%")),
            CreateGeneral(
                user,
                categoryLookup,
                "Notebook Lenovo IdeaPad 5 Ryzen 7",
                PublicationGroup.Electronica,
                "Computacion",
                "Cordoba",
                920,
                "16GB RAM y SSD de 512GB.",
                "Muy buen estado, se entrega con cargador y funda.",
                "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=1200&q=80",
                -31.4201, -64.1888,
                "Notebooks", "Usado", "Lenovo", "IdeaPad 5", "30 dias", "Envio por encomienda",
                ("RAM", "16GB"),
                ("SSD", "512GB")),
            CreateGeneral(
                user,
                categoryLookup,
                "Bicicleta mountain bike rodado 29",
                PublicationGroup.Generales,
                "Bicicletas",
                "Mendoza",
                410,
                "Cuadro aluminio y frenos a disco.",
                "Ideal para ciudad y senderos livianos, lista para usar.",
                "https://images.unsplash.com/photo-1541625602330-2277a4c46182?auto=format&fit=crop&w=1200&q=80",
                -32.8895, -68.8458,
                "Bicicletas", "Usado", "Venzo", "R29", "Sin garantia", "Retiro"),
            CreateGeneral(
                user,
                categoryLookup,
                "Sillon esquinero 5 cuerpos",
                PublicationGroup.Generales,
                "Muebles",
                "Rosario",
                650,
                "Tapizado gris claro, muy comodo.",
                "Se vende por mudanza, sin roturas ni manchas importantes.",
                "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
                -32.9442, -60.6505,
                "Muebles", "Usado", "Nordico", "Esquinero", "Sin garantia", "Retiro a coordinar"),
            CreateGeneral(
                user,
                categoryLookup,
                "PlayStation 5 con joystick extra",
                PublicationGroup.Electronica,
                "Consolas y Videojuegos",
                "La Plata",
                890,
                "Poco uso y caja completa.",
                "Incluye un segundo joystick y base de carga.",
                "https://images.unsplash.com/photo-1606813907291-d86efa9b94db?auto=format&fit=crop&w=1200&q=80",
                -34.9214, -57.9544,
                "Consolas", "Usado", "Sony", "PS5", "15 dias", "Envio o retiro",
                ("Joystick extra", "Si")),
            CreateGeneral(
                user,
                categoryLookup,
                "Heladera no frost 430 litros",
                PublicationGroup.Electronica,
                "Electronica, Audio y Video",
                "Mar del Plata",
                740,
                "En excelente estado de funcionamiento.",
                "Color acero, muy silenciosa y con poco consumo.",
                "https://images.unsplash.com/photo-1584568694244-14fbdf83bd30?auto=format&fit=crop&w=1200&q=80",
                -38.0055, -57.5426,
                "Heladeras", "Usado", "Samsung", "No Frost", "Sin garantia", "Retiro"),
            CreateGeneral(
                user,
                categoryLookup,
                "Mesa de comedor de madera maciza",
                PublicationGroup.Generales,
                "Muebles",
                "Parana",
                320,
                "Para seis personas, muy firme.",
                "Incluye seis sillas tapizadas en buen estado.",
                "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
                -31.7319, -60.5238,
                "Muebles", "Usado", "Roble", "Comedor", "Sin garantia", "Retiro"),
            CreateGeneral(
                user,
                categoryLookup,
                "Camara Canon EOS Rebel T7",
                PublicationGroup.Electronica,
                "Camaras y Accesorios",
                "Salta",
                580,
                "Con lente kit y bolso.",
                "Ideal para empezar fotografia, bateria original y cargador.",
                "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?auto=format&fit=crop&w=1200&q=80",
                -24.7821, -65.4232,
                "Camaras", "Usado", "Canon", "Rebel T7", "7 dias", "Envio"),
            CreateGeneral(
                user,
                categoryLookup,
                "Set de herramientas 120 piezas",
                PublicationGroup.Generales,
                "Herramientas",
                "Neuquen",
                115,
                "Caja completa con criques y puntas.",
                "Ideal hogar o taller liviano, poco uso.",
                "https://images.unsplash.com/photo-1504148455328-c376907d081c?auto=format&fit=crop&w=1200&q=80",
                -38.9516, -68.0591,
                "Herramientas", "Usado", "Stanley", "120 piezas", "Sin garantia", "Envio"),
            CreateGeneral(
                user,
                categoryLookup,
                "Cuna funcional con cajonera",
                PublicationGroup.Generales,
                "Juguetes y Bebes",
                "Santa Fe",
                270,
                "Color blanco, en muy buen estado.",
                "Se entrega desarmada con manual y herrajes completos.",
                "https://images.unsplash.com/photo-1519710164239-da123dc03ef4?auto=format&fit=crop&w=1200&q=80",
                -31.6333, -60.7000,
                "Bebes", "Usado", "Infanti", "Funcional", "Sin garantia", "Retiro")
        };

        catalog.AddRange(BuildGeneratedProperties(user, categoryLookup));
        catalog.AddRange(BuildGeneratedVehicles(user, categoryLookup));
        catalog.AddRange(BuildGeneratedElectronicPublications(user, categoryLookup));
        catalog.AddRange(BuildGeneratedGeneralPublications(user, categoryLookup));

        return catalog;
    }

    private static async Task<ApplicationUser> EnsureFictionalRealEstateCompanyAsync(VentagramDbContext db)
    {
        var companySlug = "horizonte-propiedades";
        var companyUser = await db.Users.FirstOrDefaultAsync(x => x.CompanySlug == companySlug);
        if (companyUser is not null)
        {
            return companyUser;
        }

        companyUser = new ApplicationUser
        {
            Name = "Horizonte Propiedades",
            IsCompany = true,
            CompanyName = "Horizonte Propiedades",
            CompanySlug = companySlug,
            CompanyLogoUrl = "https://images.unsplash.com/photo-1560518883-ce09059eeffa?auto=format&fit=crop&w=600&q=80",
            CompanyTagline = "Tasaciones claras, cierres agiles y propiedades bien presentadas.",
            CompanyIndustry = "Inmobiliaria",
            Email = "contacto@horizonte-propiedades.demo",
            Phone = "3415551200",
            RespondsEmails = true,
            AcceptsCalls = true,
            RespondsWhatsApp = true,
            AllowsSiteChat = true,
            ContactPreference = "SiteChatEmailCallsWhatsApp",
            PasswordHash = AuthService.HashPassword("Demo1234!")
        };

        db.Users.Add(companyUser);
        await db.SaveChangesAsync();
        return companyUser;
    }

    private static async Task<ApplicationUser> EnsureFictionalAutoCompanyAsync(VentagramDbContext db)
    {
        var companySlug = "ruta-media-autos";
        var companyUser = await db.Users.FirstOrDefaultAsync(x => x.CompanySlug == companySlug);
        if (companyUser is not null)
        {
            return companyUser;
        }

        companyUser = new ApplicationUser
        {
            Name = "Ruta Media Autos",
            IsCompany = true,
            CompanyName = "Ruta Media Autos",
            CompanySlug = companySlug,
            CompanyLogoUrl = "https://commons.wikimedia.org/wiki/Special:FilePath/2012_Ford_Focus_S_sedan_--_07-14-2011.jpg",
            CompanyTagline = "Autos usados reales, precio visible y stock pensado para mover publicidad local.",
            CompanyIndustry = "Concesionaria de autos",
            Email = "ventas@ruta-media-autos.demo",
            Phone = "3415552200",
            RespondsEmails = true,
            AcceptsCalls = true,
            RespondsWhatsApp = true,
            AllowsSiteChat = true,
            ContactPreference = "SiteChatEmailCallsWhatsApp",
            PasswordHash = AuthService.HashPassword("Demo1234!")
        };

        db.Users.Add(companyUser);
        await db.SaveChangesAsync();
        return companyUser;
    }

    private static async Task<ApplicationUser> EnsureFictionalBoatCompanyAsync(VentagramDbContext db)
    {
        var companySlug = "delta-nautica";
        var companyUser = await db.Users.FirstOrDefaultAsync(x => x.CompanySlug == companySlug);
        if (companyUser is not null)
        {
            return companyUser;
        }

        companyUser = new ApplicationUser
        {
            Name = "Delta Nautica",
            IsCompany = true,
            CompanyName = "Delta Nautica",
            CompanySlug = companySlug,
            CompanyLogoUrl = "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_315_motorboat%2C_Kivik.jpg",
            CompanyTagline = "Lanchas y barcos listos para salir al rio, con fotos reales y precios claros.",
            CompanyIndustry = "Concesionaria nautica",
            Email = "consultas@delta-nautica.demo",
            Phone = "1145553300",
            RespondsEmails = true,
            AcceptsCalls = true,
            RespondsWhatsApp = true,
            AllowsSiteChat = true,
            ContactPreference = "SiteChatEmailCallsWhatsApp",
            PasswordHash = AuthService.HashPassword("Demo1234!")
        };

        db.Users.Add(companyUser);
        await db.SaveChangesAsync();
        return companyUser;
    }

    private static List<Publication> BuildCompanyRealEstateCatalog(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var imageUrls = new[]
        {
            "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1600585154526-990dced4db0d?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1494526585095-c41746248156?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1502005097973-6a7082348e28?auto=format&fit=crop&w=1200&q=80"
        };

        return
        [
            CreateProperty(user, categoryLookup, "Departamento 2 ambientes con balcon en Pichincha", "Departamentos", "Rosario", 118000, "Living al frente, balcon corrido y cocina separada.", "Unidad reciclada en semipiso, a metros de Bv. Oroño y con excelente renta proyectada.", imageUrls[0], -32.9442, -60.6505, "Departamento", "Venta", "Pichincha", 61, 56, "2 ambientes", 1, 0, "Excelente", "Agua, Gas, Internet", "Balcon, Ascensor", ("Codigo", "HZ-001")),
            CreateProperty(user, categoryLookup, "Casa 4 dormitorios con pileta en Fisherton", "Casas", "Rosario", 286000, "Lote amplio, jardin forestado y quincho cerrado.", "Propiedad familiar en calle tranquila, con suite principal, cochera doble y pileta climatizada.", imageUrls[1], -32.9210, -60.7625, "Casa", "Venta", "Fisherton", 420, 255, "6 ambientes", 3, 2, "Excelente", "Agua, Gas, Internet", "Pileta, Quincho, Jardin", ("Codigo", "HZ-002")),
            CreateProperty(user, categoryLookup, "PH 3 ambientes reciclado en Echesortu", "PH", "Rosario", 94000, "Sin expensas, patio seco y terraza utilizable.", "Ingreso independiente, cocina nueva y dos dormitorios con placard en una zona muy buscada.", imageUrls[2], -32.9398, -60.6842, "PH", "Venta", "Echesortu", 104, 82, "3 ambientes", 1, 0, "Reciclado", "Agua, Gas, Cloacas", "Patio, Terraza", ("Codigo", "HZ-003")),
            CreateProperty(user, categoryLookup, "Lote en barrio cerrado Vida Lagoon", "Terrenos", "Funes", 69000, "Ubicacion interna y orientacion norte.", "Terreno listo para escriturar en desarrollo premium con amenities y seguridad 24 horas.", imageUrls[3], -32.9151, -60.8095, "Lote", "Venta", "Funes", 820, null, "Terreno", 0, 0, "Muy bueno", "Agua, Electricidad", "Seguridad, Club House", ("Codigo", "HZ-004")),
            CreateProperty(user, categoryLookup, "Departamento monoambiente premium en Puerto Norte", "Departamentos", "Rosario", 790, "Alquiler temporal con amenities y cochera opcional.", "Edificio nuevo con pileta, gimnasio y laundry. Ideal ejecutivos o estadias mensuales.", imageUrls[4], -32.9257, -60.6619, "Departamento", "Alquiler", "Puerto Norte", 38, 34, "Monoambiente", 1, 0, "A estrenar", "Agua, Internet", "Pileta, Gimnasio, Laundry", ("Codigo", "HZ-005")),
            CreateProperty(user, categoryLookup, "Casa quinta con parque y quincho en Roldan", "Quintas", "Roldan", 198000, "Gran arboleda, galeria y pileta cercada.", "Ideal vivienda permanente o fin de semana, con ambientes amplios y salida rapida a autopista.", imageUrls[5], -32.9096, -60.9056, "Casa quinta", "Venta", "Roldan", 1260, 210, "5 ambientes", 2, 2, "Muy bueno", "Agua, Luz, Internet", "Pileta, Quincho, Parque", ("Codigo", "HZ-006")),
            CreateProperty(user, categoryLookup, "Oficina corporativa en el centro de Rosario", "Oficinas", "Rosario", 132000, "Recepcion, sala de reuniones y 3 privados.", "Piso exclusivo en edificio profesional con control de acceso y bajas expensas.", imageUrls[6], -32.9468, -60.6393, "Oficina", "Venta", "Centro", 128, 128, "Planta dividida", 2, 1, "Excelente", "Internet, Luz, Agua", "Recepcion, Seguridad", ("Codigo", "HZ-007")),
            CreateProperty(user, categoryLookup, "Duplex 3 dormitorios con patio en Funes Town", "Duplex", "Funes", 154000, "Distribucion moderna y cochera semicubierta.", "Desarrollo joven, cocina integrada y patio verde con parrillero.", imageUrls[7], -32.9158, -60.8099, "Duplex", "Venta", "Funes Town", 145, 112, "4 ambientes", 2, 1, "Excelente", "Agua, Gas, Internet", "Patio, Parrilla", ("Codigo", "HZ-008")),
            CreateProperty(user, categoryLookup, "Local comercial en esquina sobre Pellegrini", "Locales", "Rosario", 164000, "Doble vidriera y deposito con entrepiso.", "Excelente flujo peatonal y vehicular, apto gastronomia ligera o retail.", imageUrls[1], -32.9569, -60.6395, "Local", "Venta", "Pellegrini", 97, 97, "Salon + deposito", 2, 0, "Muy bueno", "Agua, Luz, Gas", "Vidriera, Deposito", ("Codigo", "HZ-009")),
            CreateProperty(user, categoryLookup, "Campo agricola de 18 hectareas en Alvarez", "Campos", "Alvarez", 342000, "Tierra pareja con acceso consolidado.", "Fraccion productiva con perforacion, energia y mejoras de alambre perimetral.", imageUrls[3], -33.1245, -60.8024, "Campo", "Venta", "Alvarez", 180000, 120, "Campo", 1, 0, "Muy bueno", "Electricidad, Perforacion", "Casa de apoyo", ("Codigo", "HZ-010")),
            CreateProperty(user, categoryLookup, "Departamento 3 dormitorios con cochera en Lourdes", "Departamentos", "Rosario", 176000, "Piso alto, ventilacion cruzada y balcon aterrazado.", "Edificio de calidad con suite, lavadero independiente y cochera incluida.", imageUrls[0], -32.9449, -60.6664, "Departamento", "Venta", "Lourdes", 116, 101, "4 ambientes", 2, 1, "Excelente", "Agua, Gas, Internet", "Balcon, Cochera", ("Codigo", "HZ-011")),
            CreateProperty(user, categoryLookup, "Casa interna 2 dormitorios en barrio Abasto", "Casas", "Rosario", 87000, "Patio con parrillero y cocina comedor amplia.", "Muy buena opcion para primera vivienda, en pasillo tranquilo y bien mantenido.", imageUrls[2], -32.9598, -60.6507, "Casa", "Venta", "Abasto", 92, 74, "3 ambientes", 1, 0, "Muy bueno", "Agua, Gas, Cloacas", "Patio, Parrillero", ("Codigo", "HZ-012")),
            CreateProperty(user, categoryLookup, "Departamento 1 dormitorio en alquiler en Martin", "Departamentos", "Rosario", 520, "Semipiso luminoso a pocas cuadras del parque.", "Contrato tradicional, cocina separada, balcon al contrafrente y bajas expensas.", imageUrls[5], -32.9544, -60.6252, "Departamento", "Alquiler", "Martin", 49, 44, "2 ambientes", 1, 0, "Muy bueno", "Agua, Gas, Internet", "Balcon, Ascensor", ("Codigo", "HZ-013")),
            CreateProperty(user, categoryLookup, "Terreno en esquina para desarrollo en Ibarlucea", "Terrenos", "Ibarlucea", 58000, "Frente amplio sobre dos calles.", "Ideal para proyecto de viviendas o deposito liviano, con rapido acceso a Rosario.", imageUrls[7], -32.8522, -60.7427, "Lote", "Venta", "Ibarlucea", 940, null, "Terreno", 0, 0, "Muy bueno", "Electricidad, Agua", "Esquina", ("Codigo", "HZ-014")),
            CreateProperty(user, categoryLookup, "Casa moderna a estrenar en Tierra de Suenos 3", "Casas", "Roldan", 228000, "Una planta, galeria y pileta con solarium.", "Terminaciones premium, aberturas DVH y lote de muy buenas dimensiones.", imageUrls[1], -32.9071, -60.9188, "Casa", "Venta", "Tierra de Suenos 3", 360, 168, "4 ambientes", 2, 2, "A estrenar", "Agua, Luz, Internet", "Pileta, Galeria, Jardin", ("Codigo", "HZ-015")),
            CreateProperty(user, categoryLookup, "Oficina en alquiler con vista al rio en Puerto Norte", "Oficinas", "Rosario", 980, "Planta libre con kitchenette y cochera.", "Ideal estudio profesional o equipo comercial, edificio con seguridad y amenities corporativos.", imageUrls[6], -32.9281, -60.6612, "Oficina", "Alquiler", "Puerto Norte", 72, 72, "Planta libre", 1, 1, "Excelente", "Internet, Luz, Agua", "Seguridad, Cochera", ("Codigo", "HZ-016")),
            CreateProperty(user, categoryLookup, "PH en planta alta con terraza exclusiva en Arroyito", "PH", "Rosario", 112000, "Ingreso independiente y posibilidad de ampliacion.", "Dos dormitorios, living al frente y gran terraza con lavadero y parrillero.", imageUrls[2], -32.9164, -60.6755, "PH", "Venta", "Arroyito", 118, 83, "3 ambientes", 1, 0, "Muy bueno", "Agua, Gas, Cloacas", "Terraza, Parrillero", ("Codigo", "HZ-017")),
            CreateProperty(user, categoryLookup, "Casa de pasillo 3 dormitorios en barrio Parque", "Casas", "Rosario", 126000, "Reciclada, luminosa y con terraza transitable.", "Excelente equilibrio entre ubicacion y metros, cerca de facultades y avenidas principales.", imageUrls[4], -32.9521, -60.6574, "Casa", "Venta", "Barrio Parque", 133, 96, "4 ambientes", 2, 0, "Reciclado", "Agua, Gas, Internet", "Terraza, Patio", ("Codigo", "HZ-018")),
            CreateProperty(user, categoryLookup, "Local comercial con renta en Funes centro", "Locales", "Funes", 143000, "Vidriera al frente y contrato vigente.", "Opcion ideal para inversor, ubicado sobre corredor comercial consolidado.", imageUrls[6], -32.9154, -60.8088, "Local", "Venta", "Centro", 74, 74, "Salon", 1, 0, "Excelente", "Agua, Luz", "Vidriera", ("Codigo", "HZ-019")),
            CreateProperty(user, categoryLookup, "Departamento de categoria con amenities en Forum", "Departamentos", "Rosario", 312000, "Vista al rio, cochera y terminaciones premium.", "Unidad de alta gama con balcon terraza, suite principal y acceso a amenities completos.", imageUrls[0], -32.9396, -60.6258, "Departamento", "Venta", "Forum", 154, 136, "4 ambientes", 3, 2, "Excelente", "Agua, Gas, Internet", "Pileta, Gimnasio, SUM", ("Codigo", "HZ-020"))
        ];
    }

    private static List<Publication> BuildCompanyAutoCatalog(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        return
        [
            CreateVehicle(user, categoryLookup, "Ford Focus S 2012 con service al dia", "Autos", "Rosario", 11800, "Sedan mediano, muy parejo de mecanica y listo para transferir.", "Unidad de uso familiar con historial simple, aire, direccion y buen andar para ciudad o ruta.", "https://commons.wikimedia.org/wiki/Special:FilePath/2012_Ford_Focus_S_sedan_--_07-14-2011.jpg", -32.9442, -60.6505, "Auto", "Ford", "Focus S", 2012, 126000, "Nafta", "Manual", "1.6", "Gris plata", "Muy bueno", "Aire, direccion, cierre centralizado", ("Codigo", "RMA-001")),
            CreateVehicle(user, categoryLookup, "Toyota Corolla XEi 2009 automatico", "Autos", "Rosario", 12900, "Corolla de uso civil, comodo y conocido por su bajo mantenimiento.", "Ideal para quien busca un sedan confiable de segmento medio con caja automatica y papeles en regla.", "https://commons.wikimedia.org/wiki/Special:FilePath/09_Toyota_Corolla.jpg", -32.9506, -60.6662, "Auto", "Toyota", "Corolla XEi", 2009, 138000, "Nafta", "Automatica", "1.8", "Azul", "Muy bueno", "Climatizador, llantas, airbags", ("Codigo", "RMA-002")),
            CreateVehicle(user, categoryLookup, "Mazda 3 sedan 2010 full", "Autos", "Funes", 11200, "Segmento medio con muy buena presencia y andar firme.", "Version full con interior cuidado, pantalla de audio y mantenimiento de uso particular.", "https://commons.wikimedia.org/wiki/Special:FilePath/2010_Mazda3_sedan.jpg", -32.9154, -60.8091, "Auto", "Mazda", "3", 2010, 141000, "Nafta", "Automatica", "2.0", "Negro", "Muy bueno", "Techo, cuero, control crucero", ("Codigo", "RMA-003")),
            CreateVehicle(user, categoryLookup, "Hyundai Accent GLS 2012 economico", "Autos", "San Lorenzo", 9300, "Compacto rendidor con buen estado general y mecanica simple.", "Opcion para primer auto o movilidad diaria, con consumo contenido y mantenimiento accesible.", "https://commons.wikimedia.org/wiki/Special:FilePath/2012_Hyundai_Accent_GLS_sedan_--_12-14-2011.jpg", -32.7453, -60.7333, "Auto", "Hyundai", "Accent GLS", 2012, 119000, "Nafta", "Manual", "1.6", "Blanco", "Muy bueno", "Aire, ABS, doble airbag", ("Codigo", "RMA-004")),
            CreateVehicle(user, categoryLookup, "Nissan Versa 1.8 Sense 2011", "Autos", "Villa Gobernador Galvez", 8700, "Baul grande, interior comodo y mantenimiento al dia.", "Sedan practico para uso familiar o remis privado, con papeles listos para transferir.", "https://commons.wikimedia.org/wiki/Special:FilePath/2010-2011_Nissan_Versa_1.8S_sedan_--_11-26-2011.jpg", -33.0302, -60.6405, "Auto", "Nissan", "Versa Sense", 2011, 152000, "Nafta", "Manual", "1.8", "Plata", "Muy bueno", "Aire, levanta vidrios, cierre", ("Codigo", "RMA-005")),
            CreateVehicle(user, categoryLookup, "Chevrolet Cruze LT 2011 impecable", "Autos", "Rosario", 13900, "Sedan mediano con buena pisada y equipamiento superior.", "Version LT con confort completo, uso particular y detalles esteticos muy cuidados.", "https://commons.wikimedia.org/wiki/Special:FilePath/2011_Chevrolet_Cruze_LT_--_12-31-2010.jpg", -32.9595, -60.6618, "Auto", "Chevrolet", "Cruze LT", 2011, 131000, "Nafta", "Manual", "1.8", "Gris oscuro", "Muy bueno", "Climatizador, multimedia, sensores", ("Codigo", "RMA-006")),
            CreateVehicle(user, categoryLookup, "Kia Rio EX 2012 hatchback", "Autos", "Perez", 9800, "Hatch agil y bien parado, con equipamiento correcto para todos los dias.", "Alternativa compacta de clase media baja, con buen consumo y formato practico para ciudad.", "https://commons.wikimedia.org/wiki/Special:FilePath/2012_Kia_Rio_EX_five-door_--_04-23-2012.JPG", -32.9983, -60.7679, "Auto", "Kia", "Rio EX", 2012, 127000, "Nafta", "Manual", "1.4", "Rojo", "Muy bueno", "Aire, USB, comando al volante", ("Codigo", "RMA-007")),
            CreateVehicle(user, categoryLookup, "BMW 320i Executive 2010", "Autos", "Rosario", 24900, "El unico mas arriba de precio del lote, con perfil premium pero sin irse a lujo extremo.", "Sedan mediano premium para destacar la pauta con una unidad aspiracional y una foto real de catalogo abierto.", "https://commons.wikimedia.org/wiki/Special:FilePath/2008-2011_BMW_320i_%28E90%29_sedan_%282011-03-23%29.jpg", -32.9398, -60.6554, "Auto", "BMW", "320i Executive", 2010, 98000, "Nafta", "Automatica", "2.0", "Negro", "Excelente", "Cuero, climatizador, control crucero", ("Codigo", "RMA-008")),
            CreateVehicle(user, categoryLookup, "Ford Focus SFE 2012 segunda mano", "Autos", "Casilda", 11600, "Auto medio con andar rutero y motor simple.", "Publicado para un publico que busca un usado serio, conocido y con imagen real visible.", "https://commons.wikimedia.org/wiki/Special:FilePath/2012_Ford_Focus_SFE_sedan_--_08-12-2011.jpg", -33.0442, -61.1681, "Auto", "Ford", "Focus SFE", 2012, 134000, "Nafta", "Manual", "2.0", "Blanco", "Muy bueno", "Aire, ABS, control de estabilidad", ("Codigo", "RMA-009")),
            CreateVehicle(user, categoryLookup, "Toyota Corolla 2010 azul sedan", "Autos", "Roldan", 13100, "Sedan mediano muy buscado por confort y reventa.", "Unidad de uso familiar con presencia sobria y rango de precio razonable para campana local.", "https://commons.wikimedia.org/wiki/Special:FilePath/2010_Toyota_Corolla.jpg", -32.8981, -60.9063, "Auto", "Toyota", "Corolla", 2010, 129000, "Nafta", "Automatica", "1.6", "Azul", "Muy bueno", "Aire, llantas, comando al volante", ("Codigo", "RMA-010")),
            CreateVehicle(user, categoryLookup, "Honda Fit 2009 automatico full", "Autos", "Rosario", 10400, "Compacto confiable con muy buen aprovechamiento interior.", "Ideal para ciudad, con mantenimiento conocido y foto real de unidad de referencia.", "https://commons.wikimedia.org/wiki/Special:FilePath/2009_Honda_Fit_Base.jpg", -32.9478, -60.6309, "Auto", "Honda", "Fit", 2009, 143000, "Nafta", "Automatica", "1.5", "Gris", "Muy bueno", "Aire, ABS, doble airbag", ("Codigo", "RMA-011")),
            CreateVehicle(user, categoryLookup, "Hyundai Elantra GLS 2011 sedan", "Autos", "Santa Fe", 12800, "Sedan mediano comodo, bien equipado y de lineas actuales.", "Opcion de clase media equilibrada para una vidriera con stock variado y sin humo.", "https://commons.wikimedia.org/wiki/Special:FilePath/2011_Hyundai_Elantra_GLS_--_06-02-2011_1.jpg", -31.6333, -60.7000, "Auto", "Hyundai", "Elantra GLS", 2011, 136000, "Nafta", "Automatica", "1.8", "Plata", "Muy bueno", "Climatizador, sensores, llantas", ("Codigo", "RMA-012")),
            CreateVehicle(user, categoryLookup, "Volkswagen Jetta SE 2011", "Autos", "Parana", 13700, "Sedan mediano para quien prioriza ruta y baul.", "Unidad pareja con presencia ejecutiva y precio ubicado dentro del rango util para pauta.", "https://commons.wikimedia.org/wiki/Special:FilePath/2011_Volkswagen_Jetta_SE_--_05-06-2011.jpg", -31.7319, -60.5238, "Auto", "Volkswagen", "Jetta SE", 2011, 147000, "Nafta", "Automatica", "2.5", "Negro", "Muy bueno", "Cuero, techo, control crucero", ("Codigo", "RMA-013")),
            CreateVehicle(user, categoryLookup, "Chevrolet Aveo 2011 hatchback", "Autos", "Cordoba", 7600, "Entrada de gama prolija para primer auto.", "Publicacion pensada para atraer consultas por precio bajo con foto real clara.", "https://commons.wikimedia.org/wiki/Special:FilePath/20110402_chevrolet_aveo_01.jpg", -31.4201, -64.1888, "Auto", "Chevrolet", "Aveo", 2011, 158000, "Nafta", "Manual", "1.6", "Azul", "Muy bueno", "Aire, cierre, audio", ("Codigo", "RMA-014")),
            CreateVehicle(user, categoryLookup, "Kia Rio sedan 2012 uso particular", "Autos", "Mendoza", 10100, "Sedan chico de mecanica rendidora y buen equipamiento.", "Alternativa realista para clientes que buscan algo moderno sin subir a premium.", "https://commons.wikimedia.org/wiki/Special:FilePath/2012_Kia_Rio_sedan_front_--_2012_DC.JPG", -32.8895, -68.8458, "Auto", "Kia", "Rio Sedan", 2012, 121000, "Nafta", "Manual", "1.4", "Blanco", "Muy bueno", "ABS, USB, aire", ("Codigo", "RMA-015")),
            CreateVehicle(user, categoryLookup, "Mazda 3 sedan 2010 touring", "Autos", "Buenos Aires", 11900, "Sedan medio con buena imagen y respuesta.", "Unidad orientada a publico joven o familiar que quiere subir de segmento sin pagar de mas.", "https://commons.wikimedia.org/wiki/Special:FilePath/2010_Mazda3_sedan_--_08-25-2009.jpg", -34.6037, -58.3816, "Auto", "Mazda", "3 Touring", 2010, 149000, "Nafta", "Manual", "2.0", "Rojo", "Muy bueno", "Techo, six airbags, control crucero", ("Codigo", "RMA-016")),
            CreateVehicle(user, categoryLookup, "BMW 320i 2005 sedan", "Autos", "San Nicolas", 16800, "BMW usado de entrada para mostrar una opcion aspiracional intermedia.", "Mantiene la idea de gama media con una unidad algo mas distinguida pero todavia razonable.", "https://commons.wikimedia.org/wiki/Special:FilePath/2005-2008_BMW_320i_%28E90%29_sedan_01.jpg", -33.3358, -60.2252, "Auto", "BMW", "320i", 2005, 164000, "Nafta", "Manual", "2.0", "Plata", "Muy bueno", "Cuero, llantas, climatizador", ("Codigo", "RMA-017")),
            CreateVehicle(user, categoryLookup, "Toyota Corolla XLi 2010", "Autos", "Venado Tuerto", 12600, "Sedan muy confiable para uso diario o ruta.", "Publicacion limpia, directa y con una referencia visual real que suma credibilidad.", "https://commons.wikimedia.org/wiki/Special:FilePath/Toyota_Corolla_1.6_XLi_2010_%2813412798114%29.jpg", -33.7456, -61.9688, "Auto", "Toyota", "Corolla XLi", 2010, 145000, "Nafta", "Manual", "1.6", "Gris", "Muy bueno", "Aire, airbags, cierre", ("Codigo", "RMA-018")),
            CreateVehicle(user, categoryLookup, "Hyundai Accent SE 2012 impecable", "Autos", "Rafaela", 9500, "Compacto moderno con buen consumo y mecanica accesible.", "Pensado para generar leads por precio y aspecto prolijo en anuncios pagos.", "https://commons.wikimedia.org/wiki/Special:FilePath/2012_Hyundai_Accent_SE_--_01-11-2012_front.jpg", -31.2503, -61.4867, "Auto", "Hyundai", "Accent SE", 2012, 118000, "Nafta", "Manual", "1.6", "Negro", "Muy bueno", "Aire, bluetooth, ABS", ("Codigo", "RMA-019")),
            CreateVehicle(user, categoryLookup, "Chevrolet Cruze Eco 2011 automatico", "Autos", "La Plata", 14200, "Cruze de segmento medio con imagen actual y manejo suave.", "Sirve bien para una grilla de publicidad donde hace falta un sedan algo mas equipado sin salir del rango.", "https://commons.wikimedia.org/wiki/Special:FilePath/2011_Chevrolet_Cruze_--_2011_DC.jpg", -34.9214, -57.9544, "Auto", "Chevrolet", "Cruze Eco", 2011, 128000, "Nafta", "Automatica", "1.4", "Bordo", "Muy bueno", "Climatizador, multimedia, llantas", ("Codigo", "RMA-020"))
        ];
    }

    private static List<Publication> BuildCompanyBoatCatalog(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        return
        [
            CreateBoat(user, categoryLookup, "Bayliner 175 2008 lista para rio", "Lanchas", "Tigre", 17800, "Lancha de entrada muy buscada para paseo y fines de semana.", "Casco conocido, tamano manejable y perfil ideal para una nautica que quiera mostrar opciones de acceso.", "https://commons.wikimedia.org/wiki/Special:FilePath/Bayliner_175%2C_stern_view.JPG", -34.4260, -58.5792, "Lancha", "Bayliner", "175", 2008, "17.5 pies", "MerCruiser", "Muy bueno", ("Codigo", "DNA-001")),
            CreateBoat(user, categoryLookup, "Saga 315 cabinada 2008", "Lanchas", "San Fernando", 19900, "Cabinada de porte medio para paseo largo y uso familiar.", "Unidad con presencia nautica seria, pensada para mostrar una opcion de clase media alta sin salirse del rango pedido.", "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_315_motorboat%2C_Kivik.jpg", -34.4461, -58.5575, "Lancha cabinada", "Saga", "315", 2008, "31 pies", "Volvo Penta", "Muy bueno", ("Codigo", "DNA-002")),
            CreateBoat(user, categoryLookup, "Lancha open 18 pies 2009", "Lanchas", "Tigre", 12500, "Lancha abierta para uso recreativo, simple de mover y mantener.", "Publicacion pensada para captar consultas por una unidad accesible y de lectura rapida en pauta.", "https://commons.wikimedia.org/wiki/Special:FilePath/Motorboat.jpg", -34.4098, -58.5867, "Lancha open", "Open", "18", 2009, "18 pies", "Fuera de borda 90 HP", "Muy bueno", ("Codigo", "DNA-003")),
            CreateBoat(user, categoryLookup, "Saga 35 2008 con cabina", "Lanchas", "Escobar", 18900, "Barco cabinado para quien busca mas eslora sin irse a valores altos.", "Opcion intermedia con foco en navegacion de paseo, buena presencia y foto real no generada.", "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_35_motorboat%2C_Kivik.jpg", -34.3483, -58.7946, "Barco cabinado", "Saga", "35", 2008, "35 pies", "Volvo Penta", "Muy bueno", ("Codigo", "DNA-004")),
            CreateBoat(user, categoryLookup, "Yate cabinado compacto 2009", "Lanchas", "San Isidro", 32900, "El unico mas caro del catalogo nautico, pensado como yatesito aspiracional.", "Se carga como pieza de destaque para publicidad, manteniendo el resto del stock en valores bastante mas terrenales.", "https://commons.wikimedia.org/wiki/Special:FilePath/Small_yacht.jpg", -34.4721, -58.5136, "Yate", "Custom", "Cabinado 28", 2009, "28 pies", "Inboard", "Excelente", ("Codigo", "DNA-005")),
            CreateBoat(user, categoryLookup, "Motorboat recreativa 2009", "Lanchas", "Tigre", 14800, "Lancha simple para paseo de fin de semana y guardado facil.", "Alternativa de acceso con foto real, precio visible y lectura rapida para anuncios.", "https://commons.wikimedia.org/wiki/Special:FilePath/Motorboat.JPG", -34.4189, -58.5787, "Lancha", "Generic", "Recreativa 19", 2009, "19 pies", "Inboard", "Muy bueno", ("Codigo", "DNA-006")),
            CreateBoat(user, categoryLookup, "Lancha Bayliner compacta 2010", "Lanchas", "San Fernando", 16900, "Lancha para familia chica con uso recreativo y mantenimiento razonable.", "Pensada para una nautica local que necesita variedad sin llenar el catalogo de unidades fuera de rango.", "https://commons.wikimedia.org/wiki/Special:FilePath/Moscow%2C_small_boats%2C_Bayliner%2C_Aug_2026_01.jpg", -34.4394, -58.5648, "Lancha", "Bayliner", "Compact", 2010, "18 pies", "MerCruiser", "Muy bueno", ("Codigo", "DNA-007")),
            CreateBoat(user, categoryLookup, "Yate de paseo 2009 vista lateral", "Lanchas", "Tigre", 18400, "Casco de paseo con presencia sobria para salidas tranquilas.", "La idea es mostrar una embarcacion intermedia creible, no una pieza de lujo irreal.", "https://commons.wikimedia.org/wiki/Special:FilePath/Yacht_%2810509226505%29.jpg", -34.4230, -58.5728, "Barco de paseo", "Cruiser", "25", 2009, "25 pies", "Inboard", "Muy bueno", ("Codigo", "DNA-008")),
            CreateBoat(user, categoryLookup, "Lancha cabinada 2008 con toldilla", "Lanchas", "Escobar", 17200, "Cabinada corta para paseo y guardado de elementos.", "Se suma para darle volumen real al seed con una foto nautica visible y limpia.", "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_315_motorboat%2C_Kivik.jpg", -34.3406, -58.7913, "Lancha cabinada", "Saga", "280", 2008, "28 pies", "Volvo Penta", "Muy bueno", ("Codigo", "DNA-009")),
            CreateBoat(user, categoryLookup, "Lancha open 17 pies 2008", "Lanchas", "San Isidro", 13900, "Lancha abierta para uso diario sobre rio y arroyos.", "Publicacion de entrada para captar consultas por rango medio bajo.", "https://commons.wikimedia.org/wiki/Special:FilePath/Motorboat.jpg", -34.4683, -58.5303, "Lancha open", "Open", "17", 2008, "17 pies", "Fuera de borda 75 HP", "Muy bueno", ("Codigo", "DNA-010")),
            CreateBoat(user, categoryLookup, "Cabinada de paseo 2008", "Lanchas", "Tigre", 19800, "Cabinada rendidora para escapadas cortas y dias completos en el agua.", "Se ubica al tope del rango medio sin competir con el yatesito destacado.", "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_35_motorboat%2C_Kivik.jpg", -34.4055, -58.5851, "Lancha cabinada", "Saga", "300", 2008, "30 pies", "Volvo Penta", "Muy bueno", ("Codigo", "DNA-011")),
            CreateBoat(user, categoryLookup, "Lancha Bayliner 18 pies 2009", "Lanchas", "Campana", 17600, "Unidad de paseo con marca conocida y formato comercial fuerte.", "Funciona bien para publicidad porque el nombre Bayliner tiene reconocimiento visual.", "https://commons.wikimedia.org/wiki/Special:FilePath/Bayliner_175%2C_stern_view.JPG", -34.1687, -58.9591, "Lancha", "Bayliner", "180", 2009, "18 pies", "MerCruiser", "Muy bueno", ("Codigo", "DNA-012")),
            CreateBoat(user, categoryLookup, "Semirrigido familiar 2010", "Botes y semirrigidos", "Tigre", 15400, "Semirrigido para salidas cortas, pesca y recreacion.", "Completa el mix con un formato muy consultado sin subir demasiado el presupuesto.", "https://commons.wikimedia.org/wiki/Special:FilePath/Motorboat.jpg", -34.4122, -58.5814, "Semirrigido", "Generic", "SR 520", 2010, "17 pies", "Fuera de borda 90 HP", "Muy bueno", ("Codigo", "DNA-013")),
            CreateBoat(user, categoryLookup, "Lancha cabinada 26 pies 2009", "Lanchas", "San Fernando", 18700, "Cabina compacta con buen porte para fines de semana.", "Alternativa de valor medio alto, con imagen real y enfoque comercial sobrio.", "https://commons.wikimedia.org/wiki/Special:FilePath/Yacht-attessa-2009-sf.jpg", -34.4478, -58.5601, "Lancha cabinada", "Cruiser", "26", 2009, "26 pies", "Inboard", "Muy bueno", ("Codigo", "DNA-014")),
            CreateBoat(user, categoryLookup, "Open 19 pies 2010 con trailer", "Lanchas", "Zarate", 15900, "Open para recreacion con tamano practico y costo de uso contenido.", "Sirve para mostrar opciones de acceso sin caer en fotos artificiales.", "https://commons.wikimedia.org/wiki/Special:FilePath/Motorboat.JPG", -34.0981, -59.0286, "Lancha open", "Open", "19", 2010, "19 pies", "Fuera de borda 115 HP", "Muy bueno", ("Codigo", "DNA-015")),
            CreateBoat(user, categoryLookup, "Barco de paseo 24 pies 2008", "Lanchas", "Escobar", 18100, "Casco de paseo con espacio para familia y amigos.", "Precio pensado para entrar en campanas de conversion sin parecer inventado.", "https://commons.wikimedia.org/wiki/Special:FilePath/Yacht_%2810509226505%29.jpg", -34.3519, -58.7831, "Barco de paseo", "Cruiser", "24", 2008, "24 pies", "Inboard", "Muy bueno", ("Codigo", "DNA-016")),
            CreateBoat(user, categoryLookup, "Semicabinada 20 pies 2009", "Lanchas", "San Isidro", 16600, "Punto medio entre open y cabinada para uso mixto.", "La publicacion apunta a un cliente que quiere subir de nivel sin ir a un yate.", "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_315_motorboat%2C_Kivik.jpg", -34.4751, -58.5028, "Semicabinada", "Saga", "20", 2009, "20 pies", "Inboard", "Muy bueno", ("Codigo", "DNA-017")),
            CreateBoat(user, categoryLookup, "Lancha paseo 18 pies 2008", "Lanchas", "Tigre", 13200, "Lancha recreativa simple, ideal para primer ingreso a nautica.", "Se deja en rango bajo con visual real para mejorar la credibilidad del seed.", "https://commons.wikimedia.org/wiki/Special:FilePath/Motorboat.jpg", -34.4166, -58.5704, "Lancha", "Generic", "18", 2008, "18 pies", "Fuera de borda 75 HP", "Muy bueno", ("Codigo", "DNA-018")),
            CreateBoat(user, categoryLookup, "Cabinada 27 pies 2008 para delta", "Lanchas", "San Fernando", 19400, "Cabinada apta para salidas largas y pernocte corto.", "Unidad ubicada cerca del techo del rango medio para dar escalera de precios.", "https://commons.wikimedia.org/wiki/Special:FilePath/Saga_35_motorboat%2C_Kivik.jpg", -34.4418, -58.5531, "Lancha cabinada", "Saga", "27", 2008, "27 pies", "Volvo Penta", "Muy bueno", ("Codigo", "DNA-019")),
            CreateBoat(user, categoryLookup, "Lancha premium compacta 2010", "Lanchas", "Tigre", 19700, "Lancha de apariencia mas cuidada pero aun dentro de un rango comercial util.", "Se suma como opcion de borde antes de llegar al yatesito de destaque.", "https://commons.wikimedia.org/wiki/Special:FilePath/Yacht-attessa-2009-sf.jpg", -34.4211, -58.5773, "Lancha premium", "Cruiser", "22", 2010, "22 pies", "Inboard", "Excelente", ("Codigo", "DNA-020"))
        ];
    }

    private static IEnumerable<Publication> BuildGeneratedProperties(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var cities = new[]
        {
            new CitySeed("Cordoba", "General Paz", -31.4167, -64.1833),
            new CitySeed("Rosario", "Pichincha", -32.9442, -60.6505),
            new CitySeed("Mendoza", "Godoy Cruz", -32.9286, -68.8440),
            new CitySeed("Buenos Aires", "Caballito", -34.6186, -58.4353),
            new CitySeed("La Plata", "City Bell", -34.8686, -58.0718),
            new CitySeed("Mar del Plata", "Guemes", -38.0055, -57.5426),
            new CitySeed("Salta", "Tres Cerritos", -24.7686, -65.3950),
            new CitySeed("Parana", "Centro", -31.7319, -60.5238),
            new CitySeed("Santa Fe", "Candioti", -31.6333, -60.7000),
            new CitySeed("Neuquen", "Alta Barda", -38.9516, -68.0591)
        };
        var kinds = new[]
        {
            new PropertySeed("Departamentos", "Departamento", "Venta"),
            new PropertySeed("Casas", "Casa", "Venta"),
            new PropertySeed("Terrenos", "Lote", "Venta"),
            new PropertySeed("PH", "PH", "Venta"),
            new PropertySeed("Duplex", "Duplex", "Venta"),
            new PropertySeed("Oficinas", "Oficina", "Venta"),
            new PropertySeed("Quintas", "Casa quinta", "Venta"),
            new PropertySeed("Locales", "Local", "Venta"),
            new PropertySeed("Campos", "Campo", "Venta"),
            new PropertySeed("Departamentos", "Departamento", "Alquiler")
        };
        var highlights = new[]
        {
            "con balcon y mucha luz",
            "reciclado y listo para ingresar",
            "apto credito y excelente acceso",
            "con patio y cochera",
            "ideal renta o primera vivienda",
            "sobre calle tranquila y segura",
            "con amenities y bajas expensas",
            "listo para escriturar",
            "con buena orientacion",
            "publicado por tiempo limitado"
        };
        var imageUrls = new[]
        {
            "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1600585154526-990dced4db0d?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80"
        };

        var publications = new List<Publication>();
        for (var i = 0; i < 40; i++)
        {
            var city = cities[i % cities.Length];
            var kind = kinds[i % kinds.Length];
            var highlight = highlights[i % highlights.Length];
            var rooms = kind.PropertyType is "Lote" or "Campo" ? "Terreno" : $"{2 + (i % 4)} ambientes";
            var totalArea = kind.PropertyType switch
            {
                "Lote" => 300 + (i * 15),
                "Campo" => 10000 + (i * 2500),
                _ => 45 + (i * 4)
            };
            decimal? coveredArea = kind.PropertyType switch
            {
                "Lote" => null,
                "Campo" => 90 + (i * 3),
                _ => totalArea - (5 + (i % 12))
            };
            var price = kind.Operation == "Alquiler"
                ? 420 + (i * 18)
                : 42000 + (i * 6200);

            publications.Add(CreateProperty(
                user,
                categoryLookup,
                $"{kind.PropertyType} {highlight} en {city.Zone} - oportunidad {i + 11:00}",
                kind.Category,
                city.Locality,
                price,
                $"{kind.PropertyType} en {city.Zone}, {city.Locality}. {highlight}.",
                $"Operacion {kind.Operation.ToLowerInvariant()} de {kind.PropertyType.ToLowerInvariant()} en {city.Zone}. Buena ubicacion, servicios conectados y publicacion con precio visible.",
                imageUrls[i % imageUrls.Length],
                city.Latitude,
                city.Longitude,
                kind.PropertyType,
                kind.Operation,
                city.Zone,
                totalArea,
                coveredArea,
                rooms,
                1 + (i % 3),
                i % 3,
                i % 2 == 0 ? "Muy bueno" : "Excelente",
                "Agua, Luz, Internet",
                i % 2 == 0 ? "Balcon, Parrilla" : "Patio, Cochera",
                ("Codigo", $"INM-{i + 11:000}")));
        }

        return publications;
    }

    private static IEnumerable<Publication> BuildGeneratedVehicles(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var cities = new[]
        {
            new CitySeed("Cordoba", "Centro", -31.4201, -64.1888),
            new CitySeed("Rosario", "Centro", -32.9442, -60.6505),
            new CitySeed("Mendoza", "Godoy Cruz", -32.8895, -68.8458),
            new CitySeed("Buenos Aires", "Belgrano", -34.5621, -58.4563),
            new CitySeed("La Plata", "Casco", -34.9214, -57.9544),
            new CitySeed("Salta", "Macrocentro", -24.7821, -65.4232),
            new CitySeed("Parana", "Bajada Grande", -31.7440, -60.5330),
            new CitySeed("Santa Fe", "Centro", -31.6333, -60.7000),
            new CitySeed("Neuquen", "Centro", -38.9516, -68.0591),
            new CitySeed("Mar del Plata", "Constitucion", -37.9700, -57.5500)
        };
        var vehicles = new[]
        {
            new VehicleSeed("Autos", "Auto", "Ford", "Focus", "Manual", "Nafta"),
            new VehicleSeed("Autos", "Auto", "Toyota", "Corolla", "Automatica", "Nafta"),
            new VehicleSeed("Camionetas", "Camioneta", "Volkswagen", "Amarok", "Manual", "Diesel"),
            new VehicleSeed("Motos", "Moto", "Honda", "CB 250", "Manual", "Nafta"),
            new VehicleSeed("Utilitarios", "Utilitario", "Renault", "Master", "Manual", "Diesel"),
            new VehicleSeed("Familiares", "Familiar", "Citroen", "C4 Picasso", "Manual", "Nafta"),
            new VehicleSeed("Autos", "Auto", "Chevrolet", "Onix", "Manual", "Nafta"),
            new VehicleSeed("Motos", "Moto", "Yamaha", "FZ", "Manual", "Nafta"),
            new VehicleSeed("Utilitarios", "Utilitario", "Fiat", "Fiorino", "Manual", "Nafta/GNC"),
            new VehicleSeed("Autos", "Auto", "Peugeot", "208", "Manual", "Nafta")
        };
        var colors = new[] { "Blanco", "Gris", "Negro", "Azul", "Rojo", "Plata" };
        var imageUrls = new[]
        {
            "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1558981806-ec527fa84c39?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1553440569-bcc63803a83d?auto=format&fit=crop&w=1200&q=80"
        };

        var publications = new List<Publication>();
        for (var i = 0; i < 40; i++)
        {
            var city = cities[i % cities.Length];
            var vehicle = vehicles[i % vehicles.Length];
            var year = 2012 + (i % 13);
            var kilometers = 18000 + (i * 4200);
            var price = vehicle.VehicleType == "Moto"
                ? 2400 + (i * 170)
                : vehicle.Category == "Utilitarios"
                    ? 9800 + (i * 850)
                    : 8900 + (i * 760);

            publications.Add(CreateVehicle(
                user,
                categoryLookup,
                $"{vehicle.Brand} {vehicle.Model} {year} - oportunidad {i + 11:00}",
                vehicle.Category,
                city.Locality,
                price,
                $"{vehicle.Brand} {vehicle.Model} en {city.Locality}, listo para transferir.",
                $"{vehicle.VehicleType} con precio publicado, mantenimiento al dia y papeles en regla. Disponible en {city.Locality}.",
                imageUrls[i % imageUrls.Length],
                city.Latitude,
                city.Longitude,
                vehicle.VehicleType,
                vehicle.Brand,
                vehicle.Model,
                year,
                kilometers,
                vehicle.Fuel,
                vehicle.Transmission,
                $"Pack {1 + (i % 4)}",
                colors[i % colors.Length],
                i % 3 == 0 ? "Excelente" : "Muy bueno",
                i % 2 == 0 ? "Pantalla, Camara, Llantas" : "Aire, Direccion, ABS",
                ("Codigo", $"ROD-{i + 11:000}")));
        }

        return publications;
    }

    private static IEnumerable<Publication> BuildGeneratedElectronicPublications(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var cities = new[]
        {
            new CitySeed("Buenos Aires", "Centro", -34.6037, -58.3816),
            new CitySeed("Cordoba", "Centro", -31.4201, -64.1888),
            new CitySeed("Rosario", "Centro", -32.9442, -60.6505),
            new CitySeed("Mendoza", "Centro", -32.8895, -68.8458),
            new CitySeed("La Plata", "Centro", -34.9214, -57.9544),
            new CitySeed("Salta", "Centro", -24.7821, -65.4232),
            new CitySeed("Parana", "Centro", -31.7319, -60.5238),
            new CitySeed("Santa Fe", "Centro", -31.6333, -60.7000),
            new CitySeed("Neuquen", "Centro", -38.9516, -68.0591),
            new CitySeed("Mar del Plata", "Centro", -38.0055, -57.5426)
        };
        var items = new[]
        {
            new GeneralSeed("Celulares y Telefonos", "Tablets", "Samsung", "Galaxy Tab", "15 dias", "Envio o retiro"),
            new GeneralSeed("Computacion", "Monitores", "LG", "24 pulgadas", "7 dias", "Retiro"),
            new GeneralSeed("Computacion", "Notebooks", "HP", "Pavilion", "30 dias", "Envio"),
            new GeneralSeed("Consolas y Videojuegos", "Consolas", "Nintendo", "Switch", "7 dias", "Envio"),
            new GeneralSeed("Electronica, Audio y Video", "Audio", "Philips", "Soundbar", "30 dias", "Envio o retiro"),
            new GeneralSeed("Electronica, Audio y Video", "Video", "Samsung", "Smart TV 50", "30 dias", "Retiro"),
            new GeneralSeed("Camaras y Accesorios", "Camaras", "Sony", "Alpha", "7 dias", "Envio"),
            new GeneralSeed("Camaras y Accesorios", "Accesorios", "GoPro", "Kit Hero", "Sin garantia", "Envio"),
            new GeneralSeed("Otros", "Accesorios", "Logitech", "Combo", "Sin garantia", "Retiro"),
            new GeneralSeed("Celulares y Telefonos", "Celulares", "Motorola", "Edge", "10 dias", "Envio o retiro")
        };
        var imageUrls = new[]
        {
            "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1541625602330-2277a4c46182?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1606813907291-d86efa9b94db?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1584568694244-14fbdf83bd30?auto=format&fit=crop&w=1200&q=80"
        };

        var publications = new List<Publication>();
        for (var i = 0; i < 50; i++)
        {
            var city = cities[i % cities.Length];
            var item = items[i % items.Length];
            var price = 95 + (i * 37);
            publications.Add(CreateGeneral(
                user,
                categoryLookup,
                $"{item.Brand} {item.Model} - oportunidad {i + 11:00}",
                PublicationGroup.Electronica,
                item.Category,
                city.Locality,
                price,
                $"{item.Subcategory} en {city.Locality}, publicado con precio visible.",
                $"Articulo de {item.Subcategory.ToLowerInvariant()} en buen estado, publicado por la comunidad para comprar y vender sin verso.",
                imageUrls[i % imageUrls.Length],
                city.Latitude,
                city.Longitude,
                item.Subcategory,
                i % 2 == 0 ? "Usado" : "Nuevo",
                item.Brand,
                item.Model,
                item.Warranty,
                item.Shipping,
                ("Codigo", $"GEN-{i + 11:000}")));
        }

        return publications;
    }

    private static IEnumerable<Publication> BuildGeneratedGeneralPublications(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var cities = new[]
        {
            new CitySeed("Buenos Aires", "Centro", -34.6037, -58.3816),
            new CitySeed("Cordoba", "Centro", -31.4201, -64.1888),
            new CitySeed("Rosario", "Centro", -32.9442, -60.6505),
            new CitySeed("Mendoza", "Centro", -32.8895, -68.8458),
            new CitySeed("La Plata", "Centro", -34.9214, -57.9544),
            new CitySeed("Salta", "Centro", -24.7821, -65.4232),
            new CitySeed("Parana", "Centro", -31.7319, -60.5238),
            new CitySeed("Santa Fe", "Centro", -31.6333, -60.7000),
            new CitySeed("Neuquen", "Centro", -38.9516, -68.0591),
            new CitySeed("Mar del Plata", "Centro", -38.0055, -57.5426)
        };
        var items = new[]
        {
            new GeneralSeed("Muebles", "Muebles", "Madera Viva", "Biblioteca", "Sin garantia", "Retiro"),
            new GeneralSeed("Bicicletas", "Bicicletas", "Venzo", "R29", "Sin garantia", "Retiro"),
            new GeneralSeed("Hogar y Jardin", "Hogar", "Garden Life", "Set exterior", "Sin garantia", "Retiro"),
            new GeneralSeed("Herramientas", "Herramientas", "Bosch", "Taladro", "Sin garantia", "Envio"),
            new GeneralSeed("Deportes y Fitness", "Fitness", "Athletic", "Bicicleta fija", "Sin garantia", "Retiro"),
            new GeneralSeed("Ropa y Accesorios", "Ropa", "Levis", "Campera", "Sin garantia", "Envio"),
            new GeneralSeed("Juguetes y Bebes", "Bebes", "Graco", "Butaca", "Sin garantia", "Retiro"),
            new GeneralSeed("Instrumentos Musicales", "Musica", "Yamaha", "Teclado", "7 dias", "Envio"),
            new GeneralSeed("Libros y Coleccionables", "Coleccionables", "Planeta", "Edicion especial", "Sin garantia", "Envio"),
            new GeneralSeed("Otros", "Otros", "Genérico", "Lote varios", "Sin garantia", "Retiro")
        };
        var imageUrls = new[]
        {
            "https://images.unsplash.com/photo-1541625602330-2277a4c46182?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1504148455328-c376907d081c?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1584568694244-14fbdf83bd30?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1519710164239-da123dc03ef4?auto=format&fit=crop&w=1200&q=80"
        };

        var publications = new List<Publication>();
        for (var i = 0; i < 50; i++)
        {
            var city = cities[i % cities.Length];
            var item = items[i % items.Length];
            var price = 95 + (i * 37);
            publications.Add(CreateGeneral(
                user,
                categoryLookup,
                $"{item.Brand} {item.Model} - oportunidad {i + 51:00}",
                PublicationGroup.Generales,
                item.Category,
                city.Locality,
                price,
                $"{item.Subcategory} en {city.Locality}, publicado con precio visible.",
                $"Articulo de {item.Subcategory.ToLowerInvariant()} en buen estado, publicado por la comunidad para comprar y vender sin verso.",
                imageUrls[i % imageUrls.Length],
                city.Latitude,
                city.Longitude,
                item.Subcategory,
                i % 2 == 0 ? "Usado" : "Nuevo",
                item.Brand,
                item.Model,
                item.Warranty,
                item.Shipping,
                ("Codigo", $"GEN-{i + 51:000}")));
        }

        return publications;
    }

    private static Publication CreateProperty(
        ApplicationUser user,
        PublicationCategoryLookup categoryLookup,
        string title,
        string category,
        string locality,
        decimal price,
        string shortDescription,
        string longDescription,
        string imageUrl,
        double latitude,
        double longitude,
        string propertyType,
        string operation,
        string zone,
        decimal totalArea,
        decimal? coveredArea,
        string rooms,
        int bathrooms,
        int garageSpaces,
        string condition,
        string? services,
        string? amenities,
        params (string Key, string Value)[] extras)
    {
        var publication = new Publication
        {
            Group = PublicationGroup.Inmuebles,
            CategoryId = ResolveCategoryId(categoryLookup, PublicationGroup.Inmuebles, category),
            Title = title,
            Price = price,
            OperationType = PublicationOperationTypeExtensions.ParseOrNull(operation),
            Currency = "USD",
            Locality = locality,
            ShortDescription = shortDescription,
            LongDescription = longDescription,
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = price > 100000,
            Status = "Activa",
            Latitude = latitude,
            Longitude = longitude,
            MediaItems = PublicationMediaBuilder.Build(
                BuildImagesCsv(imageUrl, PropertyGalleryPool),
                null,
                DateTime.UtcNow)
        };
        return publication;
    }

    private static Publication CreateVehicle(
        ApplicationUser user,
        PublicationCategoryLookup categoryLookup,
        string title,
        string category,
        string locality,
        decimal price,
        string shortDescription,
        string longDescription,
        string imageUrl,
        double latitude,
        double longitude,
        string vehicleType,
        string brand,
        string model,
        int year,
        int kilometers,
        string fuel,
        string transmission,
        string version,
        string color,
        string condition,
        string equipment,
        params (string Key, string Value)[] extras)
    {
        var publication = new Publication
        {
            Group = PublicationGroup.Rodados,
            CategoryId = ResolveCategoryId(categoryLookup, PublicationGroup.Rodados, category),
            Title = title,
            Price = price,
            Currency = "USD",
            Locality = locality,
            ShortDescription = shortDescription,
            LongDescription = longDescription,
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = price > 18000,
            Status = "Activa",
            Latitude = latitude,
            Longitude = longitude,
            MediaItems = PublicationMediaBuilder.Build(
                BuildImagesCsv(imageUrl, VehicleGalleryPool),
                null,
                DateTime.UtcNow)
        };
        return publication;
    }

    private static Publication CreateBoat(
        ApplicationUser user,
        PublicationCategoryLookup categoryLookup,
        string title,
        string category,
        string locality,
        decimal price,
        string shortDescription,
        string longDescription,
        string imageUrl,
        double latitude,
        double longitude,
        string boatType,
        string brand,
        string model,
        int year,
        string length,
        string engine,
        string condition,
        params (string Key, string Value)[] extras)
    {
        var publication = new Publication
        {
            Group = PublicationGroup.Embarcaciones,
            CategoryId = ResolveCategoryId(categoryLookup, PublicationGroup.Embarcaciones, category),
            Title = title,
            Price = price,
            Currency = "USD",
            Locality = locality,
            ShortDescription = shortDescription,
            LongDescription = longDescription,
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = price > 18000,
            Status = "Activa",
            Latitude = latitude,
            Longitude = longitude,
            MediaItems = PublicationMediaBuilder.Build(
                BuildImagesCsv(imageUrl, BoatGalleryPool),
                null,
                DateTime.UtcNow)
        };
        return publication;
    }

    private static Publication CreateGeneral(
        ApplicationUser user,
        PublicationCategoryLookup categoryLookup,
        string title,
        PublicationGroup group,
        string category,
        string locality,
        decimal price,
        string shortDescription,
        string longDescription,
        string imageUrl,
        double latitude,
        double longitude,
        string subcategory,
        string itemCondition,
        string? brand,
        string? model,
        string? warranty,
        string? shipping,
        params (string Key, string Value)[] extras)
    {
        var publication = new Publication
        {
            Group = group,
            CategoryId = ResolveCategoryId(categoryLookup, group, category),
            Title = title,
            Price = price,
            Currency = "USD",
            Locality = locality,
            ShortDescription = shortDescription,
            LongDescription = longDescription,
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = price > 700,
            Status = "Activa",
            Latitude = latitude,
            Longitude = longitude,
            MediaItems = PublicationMediaBuilder.Build(
                BuildImagesCsv(imageUrl, GeneralGalleryPool),
                null,
                DateTime.UtcNow)
        };
        return publication;
    }

    private static async Task<PublicationCategoryLookup> BuildCategoryLookupAsync(VentagramDbContext db)
    {
        var categories = await db.PublicationCategories
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Group, x.Name })
            .ToListAsync();

        var byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var fallbackByGroup = new Dictionary<PublicationGroup, int>();

        foreach (var category in categories)
        {
            var key = $"{(byte)category.Group}|{NormalizeCategoryKey(category.Name)}";
            byKey[key] = category.Id;

            if (!fallbackByGroup.ContainsKey(category.Group))
            {
                fallbackByGroup[category.Group] = category.Id;
            }
        }

        return new PublicationCategoryLookup(byKey, fallbackByGroup);
    }

    private static int ResolveCategoryId(PublicationCategoryLookup lookup, PublicationGroup group, string category)
    {
        var key = $"{(byte)group}|{NormalizeCategoryKey(category)}";
        if (lookup.ByKey.TryGetValue(key, out var id))
        {
            return id;
        }

        if (lookup.FallbackByGroup.TryGetValue(group, out id))
        {
            return id;
        }

        throw new InvalidOperationException($"No se encontro una categoria activa para el grupo {group}.");
    }

    private static string NormalizeCategoryKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static async Task BackfillDynamicFieldValuesAsync(VentagramDbContext db)
    {
        var publications = await db.Publications
            .Include(x => x.Category)
            .Include(x => x.FieldValues)
            .ToListAsync();

        var fieldDefinitions = await db.PublicationCategoryFields
            .Where(x => x.IsActive)
            .ToListAsync();

        var definitionsByGroup = fieldDefinitions
            .Where(x => x.GroupId.HasValue && x.CategoryId == null)
            .GroupBy(x => x.GroupId)
            .ToDictionary(
                x => (PublicationGroup)x.Key!.Value,
                x => x.ToDictionary(y => y.InternalName, StringComparer.OrdinalIgnoreCase));

        var changed = false;
        foreach (var publication in publications)
        {
            if (!definitionsByGroup.TryGetValue(publication.Group, out _))
            {
                continue;
            }

            var operationType = PublicationOperationTypeExtensions.ParseOrNull(InferOperation(publication));
            if (publication.OperationType != operationType)
            {
                publication.OperationType = operationType;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static string InferOperation(Publication publication)
    {
        var searchableText = string.Join(" ",
            new[]
            {
                publication.Title,
                publication.ShortDescription,
                publication.LongDescription
            }.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();

        return publication.Group switch
        {
            PublicationGroup.Inmuebles when searchableText.Contains("tempor") => "Temporario",
            PublicationGroup.Inmuebles when searchableText.Contains("alquiler") => "Alquiler",
            PublicationGroup.Rodados when searchableText.Contains("financi") => "Financiacion",
            PublicationGroup.Rodados when searchableText.Contains("permut") => "Permuta",
            PublicationGroup.Embarcaciones when searchableText.Contains("alquiler") => "Alquiler",
            PublicationGroup.Embarcaciones when searchableText.Contains("permut") => "Permuta",
            PublicationGroup.Agro when searchableText.Contains("alquiler") => "Alquiler",
            PublicationGroup.Agro when searchableText.Contains("permut") => "Permuta",
            PublicationGroup.Electronica when searchableText.Contains("permut") => "Permuta",
            PublicationGroup.Generales when searchableText.Contains("permut") => "Permuta",
            _ => "Venta"
        };
    }

    private static async Task BackfillGalleryImagesAsync(VentagramDbContext db, params int[] seededUserIds)
    {
        var publications = await db.Publications
            .Include(x => x.MediaItems)
            .ToListAsync();
        var seededUserIdSet = seededUserIds.ToHashSet();
        var seedGalleryImages = new HashSet<string>(
            PropertyGalleryPool
                .Concat(VehicleGalleryPool)
                .Concat(BoatGalleryPool)
                .Concat(GeneralGalleryPool),
            StringComparer.OrdinalIgnoreCase);
        var pendingUpdates = new List<(Publication Publication, string ImagesCsv, string? VideoUrl)>();

        foreach (var publication in publications)
        {
            var currentVideo = publication.MediaItems
                .Where(x => x.MediaType == PublicationMediaType.Video && !string.IsNullOrWhiteSpace(x.Url))
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Url)
                .FirstOrDefault();
            var currentImages = publication.MediaItems
                .Where(x => x.MediaType == PublicationMediaType.Image && !string.IsNullOrWhiteSpace(x.Url))
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Url)
                .ToArray();

            if (!publication.UserId.HasValue || !seededUserIdSet.Contains(publication.UserId.Value))
            {
                if (currentImages.Length <= 1)
                {
                    continue;
                }

                var cleanedImages = currentImages
                    .Take(1)
                    .Concat(currentImages.Skip(1).Where(x => !seedGalleryImages.Contains(x)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(11)
                    .ToArray();

                if (currentImages.SequenceEqual(cleanedImages, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                pendingUpdates.Add((publication, string.Join(",", cleanedImages), currentVideo));
                continue;
            }

            if (currentImages.Length >= 2)
            {
                continue;
            }

            var primary = currentImages.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(primary))
            {
                continue;
            }

            var rebuilt = publication.Group switch
            {
                PublicationGroup.Inmuebles => BuildImagesCsv(primary, PropertyGalleryPool),
                PublicationGroup.Rodados => BuildImagesCsv(primary, VehicleGalleryPool),
                PublicationGroup.Embarcaciones => BuildImagesCsv(primary, BoatGalleryPool),
                PublicationGroup.Agro => BuildImagesCsv(primary, GeneralGalleryPool),
                _ => BuildImagesCsv(primary, GeneralGalleryPool)
            };

            pendingUpdates.Add((publication, rebuilt, currentVideo));
        }

        if (pendingUpdates.Count == 0)
        {
            return;
        }

        foreach (var update in pendingUpdates)
        {
            db.PublicationMedia.RemoveRange(update.Publication.MediaItems.ToList());
            update.Publication.MediaItems.Clear();
        }

        await db.SaveChangesAsync();

        foreach (var update in pendingUpdates)
        {
            update.Publication.MediaItems.AddRange(PublicationMediaBuilder.Build(
                update.ImagesCsv,
                update.VideoUrl,
                update.Publication.CreatedAtUtc));
        }

        await db.SaveChangesAsync();
    }

    private static string BuildImagesCsv(string primaryImage, params string[] pool)
    {
        return string.Join(",",
            new[] { primaryImage }
                .Concat(pool)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(11));
    }

    private sealed class CitySeed(string locality, string zone, double latitude, double longitude)
    {
        public string Locality { get; } = locality;
        public string Zone { get; } = zone;
        public double Latitude { get; } = latitude;
        public double Longitude { get; } = longitude;
    }

    private sealed class CategoryFieldSeed(
        string internalName,
        string label,
        PublicationCategoryFieldDataType dataType,
        bool required,
        int sortOrder,
        string? unit = null,
        string? optionsCsv = null)
    {
        public string InternalName { get; } = internalName;
        public string Label { get; } = label;
        public PublicationCategoryFieldDataType DataType { get; } = dataType;
        public bool Required { get; } = required;
        public int SortOrder { get; } = sortOrder;
        public string? Unit { get; } = unit;
        public string? OptionsCsv { get; } = optionsCsv;
    }

    private sealed class PropertySeed(string category, string propertyType, string operation)
    {
        public string Category { get; } = category;
        public string PropertyType { get; } = propertyType;
        public string Operation { get; } = operation;
    }

    private sealed class VehicleSeed(string category, string vehicleType, string brand, string model, string transmission, string fuel)
    {
        public string Category { get; } = category;
        public string VehicleType { get; } = vehicleType;
        public string Brand { get; } = brand;
        public string Model { get; } = model;
        public string Transmission { get; } = transmission;
        public string Fuel { get; } = fuel;
    }

    private sealed class GeneralSeed(string category, string subcategory, string brand, string model, string warranty, string shipping)
    {
        public string Category { get; } = category;
        public string Subcategory { get; } = subcategory;
        public string Brand { get; } = brand;
        public string Model { get; } = model;
        public string Warranty { get; } = warranty;
        public string Shipping { get; } = shipping;
    }

    private sealed class PublicationCategoryLookup(Dictionary<string, int> byKey, Dictionary<PublicationGroup, int> fallbackByGroup)
    {
        public Dictionary<string, int> ByKey { get; } = byKey;
        public Dictionary<PublicationGroup, int> FallbackByGroup { get; } = fallbackByGroup;
    }
}
