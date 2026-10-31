using System.Text;
using Application.DTOs.QuestionBank;
using Domain.Enums;

namespace WebApi.Importing;

public sealed record QuestionCsvError(int Line, string Field, string Message);
public sealed record ParsedQuestionCsvRow(int Line, CreateQuestionRequest Request);
public sealed record QuestionCsvParseResult(IReadOnlyList<ParsedQuestionCsvRow> Rows, IReadOnlyList<QuestionCsvError> Errors);

public static class QuestionCsvParser
{
    public const int MaxRows = 1000;

    public static QuestionCsvParseResult Parse(string csv, Guid courseId)
    {
        var rows = new List<ParsedQuestionCsvRow>();
        var errors = new List<QuestionCsvError>();
        if (courseId == Guid.Empty)
        {
            errors.Add(new(0, "courseId", "A valid courseId is required."));
            return new(rows, errors);
        }
        if (string.IsNullOrWhiteSpace(csv))
        {
            errors.Add(new(1, "file", "CSV file is empty."));
            return new(rows, errors);
        }

        var lines = csv.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (!TryParseLine(lines[0], out var header) || header.Count != 2 ||
            !string.Equals(header[0], "Content", StringComparison.Ordinal) ||
            !string.Equals(header[1], "BloomLevel", StringComparison.Ordinal))
        {
            errors.Add(new(1, "header", "Expected exactly these CSV columns: Content,BloomLevel."));
            return new(rows, errors);
        }

        var processedRows = 0;
        for (var index = 1; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            if (string.IsNullOrWhiteSpace(lines[index])) continue;
            if (processedRows >= MaxRows)
            {
                errors.Add(new(lineNumber, "file", $"A CSV file may contain at most {MaxRows} question rows."));
                break;
            }
            processedRows++;
            if (!TryParseLine(lines[index], out var fields))
            {
                errors.Add(new(lineNumber, "row", "Malformed CSV row; quote fields containing commas and escape quotes as doubled quotes."));
                continue;
            }
            if (fields.Count != 2)
            {
                errors.Add(new(lineNumber, "row", "Each row must contain exactly Content and BloomLevel."));
                continue;
            }

            if (!Enum.TryParse<BloomLevel>(fields[1].Trim(), true, out var bloom) || !Enum.IsDefined(bloom))
            {
                errors.Add(new(lineNumber, "BloomLevel", "BloomLevel must be a defined name such as Remember, Understand, Apply, Analyze, Evaluate, or Create."));
                continue;
            }
            rows.Add(new(lineNumber, new CreateQuestionRequest
            {
                CourseId = courseId,
                Content = fields[0].Trim(),
                BloomLevel = bloom
            }));
        }

        if (rows.Count == 0 && errors.Count == 0)
            errors.Add(new(0, "file", "CSV file contains no question rows."));
        return new(rows, errors);
    }

    private static bool TryParseLine(string line, out List<string> fields)
    {
        fields = [];
        var field = new StringBuilder();
        var quoted = false;
        var closedQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    quoted = false;
                    closedQuote = true;
                }
                else field.Append(ch);
                continue;
            }
            if (ch == ',' )
            {
                fields.Add(field.ToString());
                field.Clear();
                closedQuote = false;
            }
            else if (ch == '"' && field.Length == 0 && !closedQuote) quoted = true;
            else if (closedQuote && !char.IsWhiteSpace(ch)) return false;
            else if (ch == '"') return false;
            else field.Append(ch);
        }
        if (quoted) return false;
        fields.Add(field.ToString());
        return true;
    }
}
