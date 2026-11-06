using System.Text;
using FurnitureEcommerce.Application.Products;

namespace FurnitureEcommerce.UnitTests.Products;

public class ImageFormatTests
{
    private static readonly byte[] Padding = new byte[16];

    public static TheoryData<byte[], string> SupportedImages => new()
    {
        { [0xFF, 0xD8, 0xFF, .. Padding], ".jpg" },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Padding], ".png" },
        { [.. "RIFF"u8, 0x10, 0x00, 0x00, 0x00, .. "WEBP"u8, .. Padding], ".webp" }
    };

    public static TheoryData<byte[]> UnsupportedFiles => new()
    {
        Array.Empty<byte>(),
        new byte[] { 0xFF, 0xD8 },
        Encoding.UTF8.GetBytes("just some text, not an image"),
        Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"),
        Encoding.UTF8.GetBytes("RIFF\0\0\0\0WAVEfmt ")
    };

    [Theory]
    [MemberData(nameof(SupportedImages))]
    public async Task DetectExtensionAsync_ReturnsExtension_ForSupportedImages(byte[] content, string expected)
    {
        using var stream = new MemoryStream(content);

        var extension = await ImageFormat.DetectExtensionAsync(stream, CancellationToken.None);

        Assert.Equal(expected, extension);
    }

    [Theory]
    [MemberData(nameof(UnsupportedFiles))]
    public async Task DetectExtensionAsync_ReturnsNull_ForUnsupportedFiles(byte[] content)
    {
        using var stream = new MemoryStream(content);

        var extension = await ImageFormat.DetectExtensionAsync(stream, CancellationToken.None);

        Assert.Null(extension);
    }

    [Fact]
    public async Task DetectExtensionAsync_RewindsTheStream()
    {
        using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, .. Padding]);

        await ImageFormat.DetectExtensionAsync(stream, CancellationToken.None);

        Assert.Equal(0, stream.Position);
    }
}
