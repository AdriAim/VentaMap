using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Ventagram.Services;

public sealed class CloudflareR2ImageStorageService
{
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/bmp"
    };

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".jfif",
        ".png",
        ".webp",
        ".gif",
        ".bmp"
    };

    private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4",
        "video/webm",
        "video/quicktime"
    };

    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CloudflareR2ImageStorageService> _logger;
    private readonly Lazy<AmazonS3Client> _client;
    private readonly Lazy<byte[]> _watermarkBytes;

    public CloudflareR2ImageStorageService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<CloudflareR2ImageStorageService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _client = new Lazy<AmazonS3Client>(CreateClient);
        _watermarkBytes = new Lazy<byte[]>(LoadWatermarkBytes);
    }

    public async Task<List<string>> UploadPublicationImagesAsync(IReadOnlyList<IFormFile> files, CancellationToken cancellationToken = default)
    {
        var options = GetOptions();
        ValidateConfiguration(options);

        var urls = new List<string>();
        var rejectedMessages = new List<string>();
        foreach (var file in files.Where(x => x.Length > 0).Take(11))
        {
            if (!TryValidateImageFile(file, out var rejectionMessage))
            {
                _logger.LogWarning("Skipped unsupported publication image {FileName}. Reason: {Reason}", file.FileName, rejectionMessage);
                rejectedMessages.Add(rejectionMessage);
                continue;
            }

            try
            {
                var url = await ProcessAndUploadAsync(file, options, cancellationToken);
                urls.Add(url);
            }
            catch (UnknownImageFormatException)
            {
                var message = BuildUnsupportedImageMessage(file);
                _logger.LogWarning("Skipped undecodable publication image {FileName}.", file.FileName);
                rejectedMessages.Add(message);
            }
        }

        if (urls.Count == 0 && rejectedMessages.Count > 0)
        {
            throw new InvalidOperationException(rejectedMessages[0]);
        }

        return urls;
    }

    public async Task<string> UploadCompanyLogoAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("El logo está vacío.");
        }

        var options = GetOptions();
        ValidateConfiguration(options);

        await using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(inputStream, cancellationToken);
        if (image.Width != image.Height)
        {
            throw new InvalidOperationException("El logo de la empresa debe ser cuadrado.");
        }

        if (image.Width > 900 || image.Height > 900)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(900, 900),
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        await using var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, new WebpEncoder
        {
            Quality = 90
        }, cancellationToken);

        output.Position = 0;
        var key = BuildObjectKey(options with { Prefix = "companies/logos" }, ".webp");
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = output,
            ContentType = "image/webp",
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await _client.Value.PutObjectAsync(request, cancellationToken);
        return BuildPublicUrl(options.PublicBaseUrl, key);
    }

    public async Task<string> UploadCompanyHeroBackgroundAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("El fondo está vacío.");
        }

        var options = GetOptions();
        ValidateConfiguration(options);

        await using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(inputStream, cancellationToken);

        if (image.Width < 960 || image.Height < 320)
        {
            throw new InvalidOperationException("El fondo debe tener al menos 960x320 pixeles.");
        }

        if (image.Width > 2200 || image.Height > 1200)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(2200, 1200),
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        await using var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, new WebpEncoder
        {
            Quality = 88
        }, cancellationToken);

        output.Position = 0;
        var key = BuildObjectKey(options with { Prefix = "companies/backgrounds" }, ".webp");
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = output,
            ContentType = "image/webp",
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await _client.Value.PutObjectAsync(request, cancellationToken);
        return BuildPublicUrl(options.PublicBaseUrl, key);
    }

    public async Task<string> UploadPublicationVideoAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("El video esta vacio.");
        }

        var options = GetOptions();
        ValidateConfiguration(options);

        var contentType = NormalizeVideoContentType(file);
        if (!AllowedVideoContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException("El video debe estar en formato MP4, WEBM o MOV.");
        }

        if (file.Length > options.MaxVideoBytes)
        {
            throw new InvalidOperationException($"El video supera el limite de {options.MaxVideoBytes / (1024 * 1024)} MB.");
        }

        var inputExtension = ResolveVideoExtension(file.FileName, contentType);
        var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{inputExtension}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        var fallbackOutputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-fallback.mp4");

        try
        {
            await using (var inputStream = file.OpenReadStream())
            await using (var tempFileStream = File.Create(inputPath))
            {
                await inputStream.CopyToAsync(tempFileStream, cancellationToken);
            }

            var metadata = await ReadVideoMetadataAsync(inputPath, cancellationToken);
            if (metadata.Width <= 0 || metadata.Height <= 0)
            {
                throw new InvalidOperationException("No pudimos leer el tamaño del video.");
            }

            if (metadata.Width >= metadata.Height)
            {
                throw new InvalidOperationException("VentaMap solo permite videos Verticales.");
            }

            if (metadata.DurationSeconds is > 60.5d)
            {
                throw new InvalidOperationException("El video no puede durar mas de 1 minuto.");
            }

            await TranscodeVideoAsync(
                inputPath,
                outputPath,
                targetWidth: 1080,
                targetHeight: 1920,
                crf: 23,
                maxRateKbps: 8000,
                audioBitrateKbps: 128,
                cancellationToken);

            var chosenOutputPath = outputPath;
            var processedVideoLength = new FileInfo(outputPath).Length;
            if (processedVideoLength > options.MaxProcessedVideoBytes)
            {
                await TranscodeVideoAsync(
                    inputPath,
                    fallbackOutputPath,
                    targetWidth: 720,
                    targetHeight: 1280,
                    crf: 26,
                    maxRateKbps: 4500,
                    audioBitrateKbps: 128,
                    cancellationToken);
                chosenOutputPath = fallbackOutputPath;
                processedVideoLength = new FileInfo(fallbackOutputPath).Length;
            }

            if (processedVideoLength > options.MaxProcessedVideoBytes)
            {
                throw new InvalidOperationException($"El video final supera el limite de {options.MaxProcessedVideoBytes / (1024 * 1024)} MB.");
            }

            await using var outputStream = File.OpenRead(chosenOutputPath);
            var key = BuildObjectKey(options, ".mp4");
            var request = new PutObjectRequest
            {
                BucketName = options.Bucket,
                Key = key,
                InputStream = outputStream,
                ContentType = "video/mp4",
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };
            request.Headers.CacheControl = "public, max-age=31536000, immutable";

            await _client.Value.PutObjectAsync(request, cancellationToken);
            return BuildPublicUrl(options.PublicBaseUrl, key);
        }
        finally
        {
            TryDeleteTempFile(inputPath);
            TryDeleteTempFile(outputPath);
            TryDeleteTempFile(fallbackOutputPath);
        }
    }

    public async Task DeletePublicObjectsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var options = GetOptions();
        ValidateConfiguration(options);

        var keys = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => TryExtractManagedObjectKey(url!, options.PublicBaseUrl))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var key in keys)
        {
            try
            {
                await _client.Value.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = options.Bucket,
                    Key = key
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo borrar el objeto {ObjectKey} de R2.", key);
            }
        }
    }

    private async Task<string> ProcessAndUploadAsync(IFormFile file, R2Options options, CancellationToken cancellationToken)
    {
        await using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(inputStream, cancellationToken);

        if (Math.Max(image.Width, image.Height) > options.MaxImageSide)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(options.MaxImageSide, options.MaxImageSide),
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        ApplyWatermark(image, options);
        var webpQuality = ResolveWebpQuality(file.Length, options);

        await using var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, new WebpEncoder
        {
            Quality = webpQuality
        }, cancellationToken);

        _logger.LogInformation(
            "Compressed publication image {FileName} from {OriginalBytes} bytes to {CompressedBytes} bytes using q={Quality} and {Width}x{Height}.",
            file.FileName,
            file.Length,
            output.Length,
            webpQuality,
            image.Width,
            image.Height);

        output.Position = 0;
        var key = BuildObjectKey(options, ".webp");
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = output,
            ContentType = "image/webp",
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await _client.Value.PutObjectAsync(request, cancellationToken);

        return BuildPublicUrl(options.PublicBaseUrl, key);
    }

    private void ApplyWatermark(Image image, R2Options options)
    {
        var watermarkBytes = _watermarkBytes.Value;
        using var watermark = Image.Load(watermarkBytes);

        var targetWidth = Math.Clamp((int)(image.Width * options.WatermarkScale), 96, 260);
        var targetHeight = (int)Math.Round(watermark.Height * (targetWidth / (double)watermark.Width));
        watermark.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Stretch,
            Size = new Size(targetWidth, targetHeight),
            Sampler = KnownResamplers.Lanczos3
        }));

        var margin = Math.Max(12, image.Width / 40);
        var x = Math.Max(margin, image.Width - watermark.Width - margin);
        var y = Math.Max(margin, image.Height - watermark.Height - margin);
        image.Mutate(ctx => ctx.DrawImage(watermark, new Point(x, y), options.WatermarkOpacity));
    }

    private AmazonS3Client CreateClient()
    {
        var options = GetOptions();
        ValidateConfiguration(options);

        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = options.Region
        };

        return new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, config);
    }

    private byte[] LoadWatermarkBytes()
    {
        var watermarkPath = Path.Combine(_environment.WebRootPath, "images", "logo5.png");
        if (File.Exists(watermarkPath))
        {
            return File.ReadAllBytes(watermarkPath);
        }

        throw new FileNotFoundException("No se encontro wwwroot/images/logo5.png para la marca de agua.");
    }

    private R2Options GetOptions()
    {
        var section = _configuration.GetSection("Cloudflare:R2");
        var serviceUrl = section["ServiceUrl"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serviceUrl) && !string.IsNullOrWhiteSpace(section["AccountId"]))
        {
            serviceUrl = $"https://{section["AccountId"]}.r2.cloudflarestorage.com";
        }

        return new R2Options
        {
            AccountId = section["AccountId"] ?? string.Empty,
            AccessKeyId = section["AccessKeyId"] ?? string.Empty,
            SecretAccessKey = section["SecretAccessKey"] ?? string.Empty,
            Bucket = section["Bucket"] ?? string.Empty,
            PublicBaseUrl = section["PublicBaseUrl"] ?? string.Empty,
            ServiceUrl = serviceUrl,
            Prefix = section["Prefix"] ?? "publications",
            Region = section["Region"] ?? "auto",
            MaxImageSide = section.GetValue("MaxImageSide", 2000),
            WebpQuality = section.GetValue("WebpQuality", 90),
            WatermarkScale = section.GetValue("WatermarkScale", 0.14f),
            WatermarkOpacity = section.GetValue("WatermarkOpacity", 0.45f),
            MaxVideoBytes = section.GetValue("MaxVideoBytes", 100 * 1024 * 1024),
            MaxProcessedVideoBytes = section.GetValue("MaxProcessedVideoBytes", 45 * 1024 * 1024)
        };
    }

    private static void ValidateConfiguration(R2Options options)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ServiceUrl) && string.IsNullOrWhiteSpace(options.AccountId)) missing.Add("ServiceUrl/AccountId");
        if (string.IsNullOrWhiteSpace(options.AccessKeyId)) missing.Add("AccessKeyId");
        if (string.IsNullOrWhiteSpace(options.SecretAccessKey)) missing.Add("SecretAccessKey");
        if (string.IsNullOrWhiteSpace(options.Bucket)) missing.Add("Bucket");
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl)) missing.Add("PublicBaseUrl");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Faltan configurar Cloudflare:R2: {string.Join(", ", missing)}. " +
                "Configura las variables Cloudflare__R2__AccountId o Cloudflare__R2__ServiceUrl, " +
                "Cloudflare__R2__AccessKeyId, Cloudflare__R2__SecretAccessKey, Cloudflare__R2__Bucket y Cloudflare__R2__PublicBaseUrl. " +
                "No se puede continuar con la subida.");
        }
    }

    private static string BuildObjectKey(R2Options options, string extension)
    {
        // Edge Cache TTL is 1 year, so every replacement must get a brand-new URL.
        return $"{options.Prefix.Trim('/')}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
    }

    private static string BuildPublicUrl(string publicBaseUrl, string key)
    {
        return $"{publicBaseUrl.TrimEnd('/')}/{key.TrimStart('/')}";
    }

    private static string? TryExtractManagedObjectKey(string url, string publicBaseUrl)
    {
        var normalizedBaseUrl = publicBaseUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
        {
            return null;
        }

        var normalizedUrl = url.Trim();
        if (!normalizedUrl.StartsWith(normalizedBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = normalizedUrl[normalizedBaseUrl.Length..].TrimStart('/');
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    private static int ResolveWebpQuality(long originalBytes, R2Options options)
    {
        return Math.Clamp(options.WebpQuality, 80, 100);
    }

    private static bool TryValidateImageFile(IFormFile file, out string rejectionMessage)
    {
        var extension = Path.GetExtension(file.FileName ?? string.Empty);
        var contentType = file.ContentType?.Trim() ?? string.Empty;
        var isAllowedContentType = !string.IsNullOrWhiteSpace(contentType) && AllowedImageContentTypes.Contains(contentType);
        var isAllowedExtension = !string.IsNullOrWhiteSpace(extension) && AllowedImageExtensions.Contains(extension);

        if (isAllowedContentType || isAllowedExtension)
        {
            rejectionMessage = string.Empty;
            return true;
        }

        rejectionMessage = BuildUnsupportedImageMessage(file);
        return false;
    }

    private static string BuildUnsupportedImageMessage(IFormFile file)
    {
        var fileName = string.IsNullOrWhiteSpace(file.FileName) ? "La imagen seleccionada" : $"La imagen \"{file.FileName}\"";
        return $"{fileName} no tiene un formato compatible. Usa JPG, PNG o WEBP.";
    }

    private static string NormalizeVideoContentType(IFormFile file)
    {
        var contentType = file.ContentType?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            return contentType;
        }

        return ContentTypeProvider.TryGetContentType(file.FileName, out var inferred)
            ? inferred
            : "application/octet-stream";
    }

    private static string ResolveVideoExtension(string? fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        return contentType.ToLowerInvariant() switch
        {
            "video/webm" => ".webm",
            "video/quicktime" => ".mov",
            _ => ".mp4"
        };
    }

    private async Task<VideoMetadata> ReadVideoMetadataAsync(string inputPath, CancellationToken cancellationToken)
    {
        var output = await RunExternalToolAsync(
            "ffprobe",
            $"-v error -print_format json -show_entries stream=width,height:format=duration \"{inputPath}\"",
            cancellationToken);

        using var document = JsonDocument.Parse(output);
        var width = 0;
        var height = 0;
        double? durationSeconds = null;

        if (document.RootElement.TryGetProperty("streams", out var streamsElement)
            && streamsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streamsElement.EnumerateArray())
            {
                if (stream.TryGetProperty("width", out var widthElement)
                    && stream.TryGetProperty("height", out var heightElement)
                    && widthElement.TryGetInt32(out width)
                    && heightElement.TryGetInt32(out height))
                {
                    break;
                }
            }
        }

        if (document.RootElement.TryGetProperty("format", out var formatElement)
            && formatElement.TryGetProperty("duration", out var durationElement)
            && double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDuration))
        {
            durationSeconds = parsedDuration;
        }

        return new VideoMetadata(width, height, durationSeconds);
    }

    private async Task TranscodeVideoAsync(
        string inputPath,
        string outputPath,
        int targetWidth,
        int targetHeight,
        int crf,
        int maxRateKbps,
        int audioBitrateKbps,
        CancellationToken cancellationToken)
    {
        var vf = $"scale=w='trunc(min({targetWidth},iw)/2)*2':h='trunc(min({targetHeight},ih)/2)*2':force_original_aspect_ratio=decrease";
        var arguments =
            $"-y -i \"{inputPath}\" -vf \"{vf}\" -r 30 -c:v libx264 -preset veryfast -pix_fmt yuv420p " +
            $"-profile:v high -level 4.1 -crf {crf} -maxrate {maxRateKbps}k -bufsize {maxRateKbps * 2}k " +
            $"-movflags +faststart -c:a aac -b:a {audioBitrateKbps}k -ac 2 \"{outputPath}\"";

        await RunExternalToolAsync("ffmpeg", arguments, cancellationToken);
    }

    private async Task<string> RunExternalToolAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"No se pudo iniciar {fileName} para procesar el video.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        if (process.ExitCode != 0)
        {
            _logger.LogWarning("{Tool} fallo al procesar video. ExitCode={ExitCode}. Error={Error}", fileName, process.ExitCode, standardError);
            throw new InvalidOperationException("No se pudo procesar el video seleccionado.");
        }

        return standardOutput;
    }

    private static void TryDeleteTempFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private sealed record R2Options
    {
        public string AccountId { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string PublicBaseUrl { get; set; } = string.Empty;
        public string ServiceUrl { get; set; } = string.Empty;
        public string Prefix { get; set; } = "publications";
        public string Region { get; set; } = "auto";
        public int MaxImageSide { get; set; } = 2000;
        public int WebpQuality { get; set; } = 90;
        public float WatermarkScale { get; set; } = 0.14f;
        public float WatermarkOpacity { get; set; } = 0.45f;
        public int MaxVideoBytes { get; set; } = 100 * 1024 * 1024;
        public int MaxProcessedVideoBytes { get; set; } = 45 * 1024 * 1024;
    }

    private sealed record VideoMetadata(int Width, int Height, double? DurationSeconds);
}
