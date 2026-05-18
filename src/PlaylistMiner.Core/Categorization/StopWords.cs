namespace PlaylistMiner.Core.Categorization;

public static class StopWords
{
    public static readonly HashSet<string> Set =
    [
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "up", "about", "into", "through", "during",
        "is", "are", "was", "were", "be", "been", "being", "have", "has", "had",
        "do", "does", "did", "will", "would", "could", "should", "may", "might",
        "shall", "can", "that", "this", "these", "those", "it", "its", "as",
        "if", "not", "no", "nor", "so", "yet"
    ];

    public static IEnumerable<string> Tokenize(string text)
        => text.ToLowerInvariant()
               .Split([' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '-', '_', '/', '\\', '(', ')', '[', ']', '{', '}', '"', '\''],
                      StringSplitOptions.RemoveEmptyEntries)
               .Where(t => t.Length >= 3 && !Set.Contains(t));
}
