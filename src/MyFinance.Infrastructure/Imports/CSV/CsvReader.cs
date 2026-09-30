using System.Text;

namespace MyFinance.Infrastructure.Imports.CSV;

internal sealed record CsvRecord(int LineNumber, string RawText, IReadOnlyList<string> Fields)
{
    public bool IsBlank => Fields.All(string.IsNullOrWhiteSpace);
}

/// <summary>
/// Leitor CSV (RFC 4180): campos entre aspas podem conter delimitador, quebra de linha e aspas duplicadas ("").
/// </summary>
internal static class CsvReader
{
    private static readonly char[] CandidateDelimiters = [';', ',', '\t'];

    /// <summary>Escolhe o delimitador mais frequente (fora de aspas) na primeira linha não vazia.</summary>
    public static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;
        var counts = CandidateDelimiters.ToDictionary(d => d, _ => 0);
        var inQuotes = false;

        foreach (var c in firstLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && counts.TryGetValue(c, out var count))
            {
                counts[c] = count + 1;
            }
        }

        var best = counts.MaxBy(kv => kv.Value);
        return best.Value > 0 ? best.Key : ',';
    }

    public static IEnumerable<CsvRecord> Read(string text, char delimiter)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var raw = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordStartLine = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    raw.Append("\"\"");
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                    line += c == '\n' ? 1 : 0;
                }

                raw.Append(c);
                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
                raw.Append(c);
            }
            else if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
                raw.Append(c);
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                fields.Add(field.ToString());
                yield return new CsvRecord(recordStartLine, raw.ToString(), [.. fields]);

                fields.Clear();
                field.Clear();
                raw.Clear();
                line++;
                recordStartLine = line;
            }
            else
            {
                field.Append(c);
                raw.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0 || raw.Length > 0)
        {
            fields.Add(field.ToString());
            yield return new CsvRecord(recordStartLine, raw.ToString(), [.. fields]);
        }
    }
}