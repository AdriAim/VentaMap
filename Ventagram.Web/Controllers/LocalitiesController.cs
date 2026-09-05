using Microsoft.AspNetCore.Mvc;
using Ventagram.Services;

namespace Ventagram.Controllers;

[ApiController]
[Route("api/localities")]
public class LocalitiesController(ArgentineLocalityLookupService localityLookupService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length < 2)
        {
            return Ok(Array.Empty<object>());
        }

        var results = await localityLookupService.SearchLocalAsync(query, cancellationToken);
        return Ok(results.Select(x => new
        {
            localId = x.LocalId,
            externalId = x.ExternalId,
            locality = x.Locality,
            province = x.Province,
            latitude = x.Latitude,
            longitude = x.Longitude,
            label = x.Label,
            isLocal = x.IsLocal,
            category = x.Category
        }));
    }

    [HttpGet("search-external")]
    public async Task<IActionResult> SearchExternal([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length < 2)
        {
            return Ok(Array.Empty<object>());
        }

        var results = await localityLookupService.SearchExternalAsync(query, cancellationToken);
        return Ok(results.Select(x => new
        {
            localId = x.LocalId,
            externalId = x.ExternalId,
            locality = x.Locality,
            province = x.Province,
            latitude = x.Latitude,
            longitude = x.Longitude,
            label = x.Label,
            isLocal = x.IsLocal,
            category = x.Category
        }));
    }

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve([FromBody] ResolveLocalityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var locality = await localityLookupService.EnsureLocalityAsync(
                request.LocalId,
                request.ExternalId,
                cancellationToken);

            return Ok(new
            {
                localId = locality.Id,
                locality = locality.Locality,
                province = locality.Province,
                latitude = locality.Latitude,
                longitude = locality.Longitude,
                label = $"{locality.Locality}, {locality.Province}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public sealed class ResolveLocalityRequest
    {
        public int? LocalId { get; set; }

        public string? ExternalId { get; set; }
    }
}
