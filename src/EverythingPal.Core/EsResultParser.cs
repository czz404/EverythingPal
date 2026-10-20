// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text.Json;

namespace EverythingPal.Core;

/// <summary>
/// Parses the JSON produced by <c>es.exe -export-json</c>.
///
/// Verified output shape (ES 1.1.1.39, columns requested by <see cref="EsArguments"/>):
/// <code>
/// [{"filename":"D:\\wantai\\","size":0,"date_modified":"2026-10-19T20:31:29","attributes":16},
///  {"filename":"C:\\...\\wantai.lnk","size":434,"date_modified":"2026-10-19T20:31:33","attributes":32}]
/// </code>
/// Folders carry a trailing separator in <c>filename</c>; that is the folder detection. Zero
/// results produce an empty file (not <c>[]</c>). The export file is UTF-8, so CJK names survive
/// (stdout is written in the console code page instead, which is why we do not use it).
/// </summary>
public static class EsResultParser
{
    public static List<EverythingResult> Parse(Stream utf8Json, string query)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);

        var results = new List<EverythingResult>();
        if (utf8Json.CanSeek && utf8Json.Length == 0)
        {
            return results;
        }

        using var document = JsonDocument.Parse(utf8Json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("filename", out var filenameElement) ||
                filenameElement.GetString() is not { Length: > 0 } filename)
            {
                continue;
            }

            var isFolder = filename.EndsWith('\\') || filename.EndsWith('/');
            var fullPath = isFolder ? Path.TrimEndingDirectorySeparator(filename) : filename;

            long size = 0;
            if (element.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize))
            {
                size = parsedSize;
            }

            DateTimeOffset? modified = null;
            if (element.TryGetProperty("date_modified", out var modifiedElement) &&
                modifiedElement.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(modifiedElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedModified))
            {
                modified = parsedModified;
            }

            uint attributes = 0;
            if (element.TryGetProperty("attributes", out var attributesElement) && attributesElement.TryGetUInt32(out var parsedAttributes))
            {
                attributes = parsedAttributes;
            }

            results.Add(new EverythingResult
            {
                FullPath = fullPath,
                IsFolder = isFolder,
                Size = size,
                DateModified = modified,
                Attributes = attributes,
            });
        }

        return ResultRanker.Rank(results, query);
    }

    public static List<EverythingResult> ParseFile(string path, string query)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream, query);
    }
}
