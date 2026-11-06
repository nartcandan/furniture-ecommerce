namespace FurnitureEcommerce.Application.Products;

public static class ImageFormat
{
    private const int HeaderLength = 12;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();

    /// <summary>Detects the format from the file content, not from its name or declared type.</summary>
    /// <returns>The file extension, or null if the content is not a supported image.</returns>
    public static async Task<string?> DetectExtensionAsync(Stream content, CancellationToken cancellationToken)
    {
        var buffer = new byte[HeaderLength];
        var length = await content.ReadAtLeastAsync(
            buffer,
            HeaderLength,
            throwOnEndOfStream: false,
            cancellationToken);
        content.Position = 0;

        var header = buffer.AsSpan(0, length);

        if (header.StartsWith(Jpeg))
        {
            return ".jpg";
        }

        if (header.StartsWith(Png))
        {
            return ".png";
        }

        if (length == HeaderLength && header.StartsWith(Riff) && header[8..].SequenceEqual(Webp))
        {
            return ".webp";
        }

        return null;
    }
}
