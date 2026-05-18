using System.Text;
using System.Text.RegularExpressions;

namespace PlaylistMiner.Core;

public static partial class SlugGenerator
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleWhitespace();

    [GeneratedRegex(@"[^a-z0-9-]")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleDashes();

    public static string Generate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var slug = name.Trim().ToLowerInvariant();

        // Replace language-specific chars before general cleanup
        slug = slug
            .Replace("c#", "csharp")
            .Replace("asp.net", "aspnet")
            .Replace("f#", "fsharp")
            .Replace("q#", "qsharp");

        // Replace dots that are part of version identifiers (node.js → nodejs)
        slug = Regex.Replace(slug, @"([a-z])\.([a-z])", "$1$2");

        // Replace separators with dashes
        slug = slug
            .Replace("/", "-")
            .Replace("_", "-")
            .Replace(".", "-");

        // Normalize whitespace to dashes
        slug = MultipleWhitespace().Replace(slug, "-");

        // Remove any remaining non-slug characters
        slug = NonSlugChars().Replace(slug, string.Empty);

        // Collapse multiple dashes
        slug = MultipleDashes().Replace(slug, "-");

        return slug.Trim('-');
    }
}
