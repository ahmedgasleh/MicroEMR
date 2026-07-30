using System.Text;
using System.Text.RegularExpressions;

namespace MicroEMR.Infrastructure.Provisioning;

public static partial class SqlBatchParser
{
    public static IReadOnlyList<string> Parse(string script)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        var batches = new List<string>();
        var current = new StringBuilder();
        var inString = false;
        var inBlockComment = false;

        using var reader = new StringReader(script);
        while (reader.ReadLine() is { } line)
        {
            if (!inString && !inBlockComment && RepeatGoRegex().IsMatch(line))
                throw new InvalidOperationException("SQL GO repeat counts are not supported.");

            if (!inString && !inBlockComment && GoRegex().IsMatch(line))
            {
                AddBatch(batches, current);
                continue;
            }

            current.AppendLine(line);
            ScanLine(line, ref inString, ref inBlockComment);
        }

        if (inString || inBlockComment)
            throw new InvalidOperationException("The SQL script contains an unterminated string or block comment.");

        AddBatch(batches, current);
        return batches;
    }

    private static void ScanLine(
        string line,
        ref bool inString,
        ref bool inBlockComment)
    {
        for (var index = 0; index < line.Length; index++)
        {
            if (inBlockComment)
            {
                if (index + 1 < line.Length && line[index] == '*' && line[index + 1] == '/')
                {
                    inBlockComment = false;
                    index++;
                }
                continue;
            }

            if (inString)
            {
                if (line[index] == '\'' && index + 1 < line.Length && line[index + 1] == '\'')
                {
                    index++;
                }
                else if (line[index] == '\'')
                {
                    inString = false;
                }
                continue;
            }

            if (index + 1 < line.Length && line[index] == '-' && line[index + 1] == '-')
                return;
            if (index + 1 < line.Length && line[index] == '/' && line[index + 1] == '*')
            {
                inBlockComment = true;
                index++;
            }
            else if (line[index] == '\'')
            {
                inString = true;
            }
        }
    }

    private static void AddBatch(List<string> batches, StringBuilder current)
    {
        var batch = current.ToString().Trim();
        if (batch.Length > 0)
            batches.Add(batch);
        current.Clear();
    }

    [GeneratedRegex(@"^\s*GO\s*(?:--.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex GoRegex();

    [GeneratedRegex(@"^\s*GO\s+\d+\s*(?:--.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RepeatGoRegex();
}
