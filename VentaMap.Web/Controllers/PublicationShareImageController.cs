using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using VentaMap.Data;
using VentaMap.Models;

namespace VentaMap.Controllers;

[ApiController]
public class PublicationShareImageController(VentaMapDbContext db, IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const int Width = 1080;
    private const int Height = 1350;

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
