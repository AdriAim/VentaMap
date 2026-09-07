using Microsoft.AspNetCore.Mvc;
using VentaMap.Services;

namespace VentaMap.Controllers;

[ApiController]
[Route("api/categorias")]
public class CategoriasController(PublicationCategoryFieldService publicationCategoryFieldService) : ControllerBase
{
    [HttpGet("{id:int}/campos")]
    public async Task<IActionResult> GetCampos(int id)
    {
        var fields = await publicationCategoryFieldService.GetActiveByCategoryIdAsync(id);
        var operationOptions = fields
            .Where(x => string.Equals(x.InternalName, "operacion", StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => string.IsNullOrWhiteSpace(x.OptionsCsv)
                ? Array.Empty<string>()
                : x.OptionsCsv.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new
        {
            operationOptions,
            fields = fields
                .Where(x => !string.Equals(x.InternalName, "operacion", StringComparison.OrdinalIgnoreCase))
                .Select(x => new
                {
                    id = x.Id,
                    nombreInterno = x.InternalName,
                    etiqueta = x.Label,
                    tipoDato = x.DataType.ToString().ToLowerInvariant(),
                    obligatorio = x.Required,
                    orden = x.SortOrder,
                    mostrarEnDatosMinimos = x.ShowInBasicData,
                    unidad = x.Unit,
                    ejemplo = x.InputExample,
                    opciones = string.IsNullOrWhiteSpace(x.OptionsCsv)
                        ? Array.Empty<string>()
                        : x.OptionsCsv.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                })
        });
    }
}
