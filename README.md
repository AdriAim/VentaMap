# VentaMap

VentaMap es un portal de clasificados hecho en ASP.NET Core 8 con Razor Pages, MySQL y Entity Framework Core.

El producto quedó orientado a tres grandes rubros:

- `Inmuebles`
- `Rodados`
- `Generales`

También incorpora una lógica comunitaria para moderación:

- mensaje principal: `Basta de anuncios sin precios`
- denuncia de avisos por `No corresponde`, `Fraude` o `Sin precio`
- `Papelera` comunitaria con publicaciones denunciadas

## Estado actual

La app está montada en:

- `ASP.NET Core 8`
- `Razor Pages` como shell visual
- `Controllers MVC` para servir HTML parcial desde API
- `Pomelo.EntityFrameworkCore.MySql`
- `MySQL` como base principal

## Arquitectura actual

### Shell + HTML desde API

Las páginas Razor principales ya no renderizan el contenido completo del negocio en servidor. Ahora funcionan como host mínimo y cargan HTML desde endpoints `api/content/...`.

Páginas shell:

- [Pages/Index.cshtml](E:\Proyectos\ventamap\Pages\Index.cshtml)
- [Pages/Trash.cshtml](E:\Proyectos\ventamap\Pages\Trash.cshtml)
- [Pages/Publications/Details.cshtml](E:\Proyectos\ventamap\Pages\Publications\Details.cshtml)
- [Pages/Publications/Create.cshtml](E:\Proyectos\ventamap\Pages\Publications\Create.cshtml)

Controllers:

- [Controllers/ContentController.cs](E:\Proyectos\ventamap\Controllers\ContentController.cs)

Vistas parciales HTML servidas por controller:

- [Views/Content/Home.cshtml](E:\Proyectos\ventamap\Views\Content\Home.cshtml)
- [Views/Content/Trash.cshtml](E:\Proyectos\ventamap\Views\Content\Trash.cshtml)
- [Views/Content/Details.cshtml](E:\Proyectos\ventamap\Views\Content\Details.cshtml)
- [Views/Content/Create.cshtml](E:\Proyectos\ventamap\Views\Content\Create.cshtml)

Cliente:

- [wwwroot/js/site.js](E:\Proyectos\ventamap\wwwroot\js\site.js)

### Endpoints API actuales

- `GET /api/content/home`
- `GET /api/content/trash`
- `GET /api/content/details/{id}`
- `GET /api/content/create`
- `POST /api/content/report`
- `POST /api/content/create`

## Funcionalidad implementada

### Búsqueda y visualización

- búsqueda por grupo
- búsqueda por texto
- vistas:
  - `Mapa`
  - `Clasificado`
  - `Galería`

### Publicaciones

- alta por cuenta local
- alta con cuenta Google si se configuran credenciales
- alta anónima
- contraseña de baja para publicaciones anónimas
- detalle de publicación

### Moderación comunitaria

- botón `Denunciar` en listados
- motivos de denuncia:
  - `No corresponde`
  - `Fraude`
  - `Sin precio`
- página `Papelera` con publicaciones reportadas

## Modelo de datos

Se usa un modelo mixto:

- tabla principal de publicaciones
- detalle por rubro
- atributos extra dinámicos

Entidades relevantes:

- [Models/Publication.cs](E:\Proyectos\ventamap\Models\Publication.cs)
- [Models/PropertyDetail.cs](E:\Proyectos\ventamap\Models\PropertyDetail.cs)
- [Models/VehicleDetail.cs](E:\Proyectos\ventamap\Models\VehicleDetail.cs)
- [Models/GeneralDetail.cs](E:\Proyectos\ventamap\Models\GeneralDetail.cs)
- [Models/PublicationExtraAttribute.cs](E:\Proyectos\ventamap\Models\PublicationExtraAttribute.cs)
- [Models/PublicationReport.cs](E:\Proyectos\ventamap\Models\PublicationReport.cs)
- [Models/ApplicationUser.cs](E:\Proyectos\ventamap\Models\ApplicationUser.cs)

Persistencia:

- [Data/VentaMapDbContext.cs](E:\Proyectos\ventamap\Data\VentaMapDbContext.cs)
- [Data/SeedData.cs](E:\Proyectos\ventamap\Data\SeedData.cs)

## Base de datos

Proveedor configurado:

- `Pomelo.EntityFrameworkCore.MySql`

Cadena de desarrollo actual:

- [appsettings.Development.json](E:\Proyectos\ventamap\appsettings.Development.json)

Entorno de prueba definido:

- host: `127.0.0.1`
- puerto: `3307`
- usuario: `root`
- base: `ventamap`

## Autenticación

Soporta:

- usuario local con email, teléfono y contraseña
- Google, si se completa:
  - `Authentication:Google:ClientId`
  - `Authentication:Google:ClientSecret`

Configuración:

- [Program.cs](E:\Proyectos\ventamap\Program.cs)
- [Services/AuthService.cs](E:\Proyectos\ventamap\Services\AuthService.cs)

## Mapa

La vista de mapa usa MapTiler si se configura:

- `MapTiler:ApiKey`

Si no hay clave, se muestra placeholder.

## Estructura relevante

- `Controllers/`: endpoints MVC/API
- `Data/`: contexto EF y semilla
- `Models/`: entidades
- `Pages/`: shells Razor y flujos auxiliares
- `Services/`: lógica de negocio
- `ViewModels/`: modelos de render para controllers
- `Views/Content/`: HTML parcial servido por API
- `wwwroot/`: CSS y JS

## Ejecución

### Build

```powershell
dotnet build E:\Proyectos\ventamap\VentaMap.csproj
```

### Run

```powershell
dotnet run --project E:\Proyectos\ventamap\VentaMap.csproj --urls http://127.0.0.1:5099
```

### Debug local con chat

El chat no vive dentro de `VentaMap.Web`. Para que funcione en local hay que iniciar los dos proyectos:

- `VentaMap.Web` en `https://localhost:7048`
- `VentaMap.ChatService` en `https://localhost:7065`

Si solo se ejecuta `VentaMap.Web`, las acciones del chat van a fallar porque el frontend intenta llamar a `https://localhost:7065/api/chat/...`.

## Estado Git

`E:\Proyectos\ventamap` hoy no es un repositorio Git. Por eso:

- no hay branch local para actualizar
- no se puede hacer `git status`, `commit` ni `switch` ahí

Si querés versionarlo, el siguiente paso sería inicializar Git o mover este proyecto dentro de un repo existente.
