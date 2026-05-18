namespace PlaylistMiner.Core.Import;

public record TakeoutEntry(string VideoId, DateTime? AddedAt);

public static class TakeoutParser
{
    public static List<TakeoutEntry> Parse(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        var header = reader.ReadLine();

        if (header is null)
            throw new ImportException("CSV file is empty.");

        var columns = ParseCsvLine(header);
        var videoIdIndex = columns.FindIndex(c =>
            c.Equals("Video Id", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("Video ID", StringComparison.OrdinalIgnoreCase));

        if (videoIdIndex < 0)
            throw new ImportException("Invalid CSV format: missing 'Video Id' column.");

        var timeAddedIndex = columns.FindIndex(c =>
            c.Equals("Time Added", StringComparison.OrdinalIgnoreCase));

        var entries = new List<TakeoutEntry>();

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseCsvLine(line);
            if (fields.Count <= videoIdIndex) continue;

            var videoId = fields[videoIdIndex].Trim();
            if (string.IsNullOrWhiteSpace(videoId)) continue;

            DateTime? addedAt = null;
            if (timeAddedIndex >= 0 && fields.Count > timeAddedIndex &&
                DateTime.TryParse(fields[timeAddedIndex].Trim(), out var parsed))
            {
                addedAt = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            entries.Add(new TakeoutEntry(videoId, addedAt));
        }

        if (entries.Count == 0)
            throw new ImportException("CSV file contains no video entries.");

        return entries;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
