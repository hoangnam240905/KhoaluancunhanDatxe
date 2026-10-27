using System.Globalization;
using System.Text;
using Backend.Services.Chatbot;

namespace Backend.Services.Csv;

/// <summary>
/// Minimal CSV reader / writer for admin import-export (plan1.md Giai đoạn 3). No library:
/// RFC-4180 quoting, UTF-8 with BOM (Excel on Windows reads Vietnamese correctly), comma or
/// semicolon delimiter (Excel with Vietnamese regional settings saves with ';').
/// </summary>
public static class CsvFile
{
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    public static byte[] Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string?>> rows, IEnumerable<string>? notes = null)
    {
        var sb = new StringBuilder();
        foreach (var note in notes ?? [])
            sb.Append(Field(note)).Append("\r\n");
        sb.AppendJoin(',', header.Select(Field)).Append("\r\n");
        foreach (var row in rows)
            sb.AppendJoin(',', row.Select(Field)).Append("\r\n");
        return [.. Utf8Bom.GetPreamble(), .. Utf8Bom.GetBytes(sb.ToString())];
    }

    /// <summary>
    /// Quote when needed; also neutralise spreadsheet formulas (CSV injection): a cell starting with
    /// = + - @ is prefixed with an apostrophe so Excel shows it as text.
    /// </summary>
    private static string Field(string? value)
    {
        var v = value ?? string.Empty;
        if (v.StartsWith(TextMarker))
            return $"\"=\"\"{v[1..].Replace("\"", "")}\"\"\"";
        if (v.Length > 0 && v[0] is '=' or '+' or '@' or '\t' or '\r'
            || (v.StartsWith('-') && !decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
            v = "'" + v;
        return v.IndexOfAny([',', ';', '"', '\r', '\n']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    private const char TextMarker = '\u0001';

    /// <summary>
    /// Digits that must stay text in Excel (phone, CCCD): written as ="0901234567" so Excel keeps the
    /// leading zero instead of turning it into a number. <see cref="Clean"/> undoes it on import.
    /// </summary>
    public static string? ExcelText(string? value) => string.IsNullOrEmpty(value) ? value : TextMarker + value;

    /// <summary>Cell value as typed by the user: trimmed, without our ' guard or the ="…" text wrapper.</summary>
    public static string Clean(string value)
    {
        var v = value.Trim().TrimStart('\'');
        if (v.StartsWith("=\"") && v.EndsWith('"') && v.Length >= 3) v = v[2..^1];
        return v.Trim();
    }

    /// <summary>Rows of fields; the first non-empty row is the header. Blank lines are skipped.</summary>
    public static List<string[]> Read(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var firstLine = text.Split('\n', 2)[0];
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';

        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
                continue;
            }
            if (c == '"') inQuotes = true;
            else if (c == delimiter) { fields.Add(field.ToString()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                fields.Add(field.ToString()); field.Clear();
                if (fields.Any(f => f.Trim().Length > 0)) rows.Add([.. fields]);
                fields.Clear();
            }
            else field.Append(c);
        }
        fields.Add(field.ToString());
        if (fields.Any(f => f.Trim().Length > 0)) rows.Add([.. fields]);
        return rows;
    }

    /// <summary>Header key used for matching: no accents, lower case, no '*' or spaces around.</summary>
    public static string Key(string header) => ChatQueryParser.Normalize(header.Replace("*", "")).Trim();
}
