using FurnitureEcommerce.Application.Common;

namespace FurnitureEcommerce.UnitTests.Common;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Living Room", "living-room")]
    [InlineData("  Living   Room  ", "living-room")]
    [InlineData("Çalışma Alanı", "calisma-alani")]
    [InlineData("İç Mekân & Güzel Şey", "ic-mekan-guzel-sey")]
    [InlineData("Sofa 3000", "sofa-3000")]
    [InlineData("!!!", "")]
    public void Generate_ReturnsUrlFriendlyText(string text, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Generate(text));
    }

    [Fact]
    public async Task GenerateUniqueAsync_ReturnsBaseSlug_WhenItIsFree()
    {
        var slug = await SlugGenerator.GenerateUniqueAsync(
            "Oslo Sofa",
            (_, _) => Task.FromResult(false),
            CancellationToken.None);

        Assert.Equal("oslo-sofa", slug);
    }

    [Fact]
    public async Task GenerateUniqueAsync_AddsFirstFreeSuffix_WhenSlugIsTaken()
    {
        string[] takenSlugs = ["oslo-sofa", "oslo-sofa-2"];

        var slug = await SlugGenerator.GenerateUniqueAsync(
            "Oslo Sofa",
            (candidate, _) => Task.FromResult(takenSlugs.Contains(candidate)),
            CancellationToken.None);

        Assert.Equal("oslo-sofa-3", slug);
    }
}
