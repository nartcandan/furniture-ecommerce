using System.Globalization;
using System.Text;

namespace FurnitureEcommerce.Application.Common;

public static class SlugGenerator
{
    public static async Task<string> GenerateUniqueAsync(
        string text,
        Func<string, CancellationToken, Task<bool>> slugExists,
        CancellationToken cancellationToken)
    {
        var baseSlug = Generate(text);
        var slug = baseSlug;

        for (var suffix = 2; await slugExists(slug, cancellationToken); suffix++)
        {
            slug = $"{baseSlug}-{suffix}";
        }

        return slug;
    }

    public static string Generate(string text)
    {
        var builder = new StringBuilder(text.Length);
        var normalized = text.Replace('ı', 'i').Normalize(NormalizationForm.FormD);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
