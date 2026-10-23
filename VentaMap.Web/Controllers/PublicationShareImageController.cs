using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Controllers;

[ApiController]
public class PublicationShareImageController(VentaMapDbContext db, IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const int Width = 1080;
    private const int Height = 1350;
    private static readonly HashSet<string> RotationPreviewKeys = ["07065b490d374715a0184ec4099d0754", "1d0cb9a0acd149ddb432ee09c659705a", "3b50c762e897446daa21b25bd7f78ec4", "c08669ae7fd74da28ae95336836cc80a", "5059aa4df32b44a2839e8ffb25c4bcaf", "8a45b2baf21d43fc990b2381819f9e37"];

    [HttpGet("/api/content/image-rotation-preview")]
    public async Task<IActionResult> GetRotationPreviewAsync([FromQuery] string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || !RotationPreviewKeys.Contains(key)) return NotFound();
        var sourceUri = new Uri($"https://img.ventamap.com.ar/publications/2026/10/{key}.webp");
        try
        {
            using var response = await httpClientFactory.CreateClient().GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 15 * 1024 * 1024) return NotFound();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var image = await Image.LoadAsync(source, cancellationToken);
            image.Mutate(context => context.Rotate(RotateMode.Rotate270));
            await using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = 82 }, cancellationToken);
            Response.Headers.CacheControl = "public, max-age=86400";
            return File(output.ToArray(), "image/webp");
        }
        catch (UnknownImageFormatException) { return NotFound(); }
        catch (HttpRequestException) { return NotFound(); }
    }

    [HttpGet("/anuncio/{id:int}/vista-previa.jpg")]
    public async Task<IActionResult> GetAsync(int id, CancellationToken cancellationToken)
    {
        var imageUrl = await db.PublicationMedia
            .AsNoTracking()
            .Where(media => media.PublicationId == id
                && media.MediaType == PublicationMediaType.Image
                && !string.IsNullOrWhiteSpace(media.Url))
            .OrderByDescending(media => media.IsPrimary)
            .ThenBy(media => media.SortOrder)
            .Select(media => media.Url)
            .FirstOrDefaultAsync(cancellationToken);

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var sourceUri)
            || (sourceUri.Scheme != Uri.UriSchemeHttps && sourceUri.Scheme != Uri.UriSchemeHttp))
        {
            return NotFound();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
            using var response = await httpClientFactory.CreateClient().SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 15 * 1024 * 1024)
            {
                return NotFound();
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var image = await Image.LoadAsync(source, cancellationToken);
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(Width, Height),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Sampler = KnownResamplers.Lanczos3
            }));

            await using var output = new MemoryStream();
            await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 88 }, cancellationToken);
            Response.Headers.CacheControl = "public, max-age=86400";
            return File(output.ToArray(), "image/jpeg");
        }
        catch (UnknownImageFormatException)
        {
            return NotFound();
        }
        catch (HttpRequestException)
        {
            return NotFound();
        }
    }
}
