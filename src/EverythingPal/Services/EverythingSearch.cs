// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EverythingPal.Services;

/// <summary>
/// Searches Everything through the official <c>es.exe</c> CLI.
///
/// Why the CLI: it avoids redistributing the voidtools native SDK DLLs and works with any
/// Everything 1.4/1.5 install that ships ES. Details that matter (all verified on ES 1.1.1.39):
///   * stdout is written in the console code page (GBK here), so we use <c>-export-json</c>,
///     which writes UTF-8 to a file instead.
///   * folder results end with a trailing separator in the JSON; that is how we detect folders.
///   * exit code 9 means "no results" (with -no-result-error); 8 means Everything is not running.
///   * an empty query would return the whole index, so it is never passed through.
///   * process start dominates latency (~43 ms), the query itself is a few ms; callers debounce.
/// </summary>
internal sealed class EverythingSearch
{
    private const string EsDefaultRelativePath = @"Microsoft\WindowsApps\es.exe";

    private static readonly string[] EsCandidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), EsDefaultRelativePath),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "es.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything 1.5a", "es.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "es.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything 1.5a", "es.exe"),
    ];

    public static string? FindEsExe()
    {
        // 1. Explicit setting (settings page or EVERYTHINGPAL_ES environment variable).
        var configured = EverythingPalSettings.Instance.EsExePath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable("EVERYTHINGPAL_ES") ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
            {
                return configured;
            }

            // Allow pointing at the containing directory as well.
            var candidate = Path.Combine(configured, "es.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        foreach (var candidate in EsCandidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fall back to PATH.
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), "es.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }

    /// <summary>
    /// Runs one ES query. Never throws: failures are returned as <see cref="SearchStatus"/>.
    /// Intended to be called off the UI thread.
    /// </summary>
    public SearchOutcome Search(string query, int? maxResults = null, string? sort = null)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return new SearchOutcome { Status = SearchStatus.NoResults };
        }

        var settings = EverythingPalSettings.Instance;
        var effectiveMax = Math.Clamp(maxResults ?? settings.MaxResults, 1, 500);
        var effectiveSort = string.IsNullOrWhiteSpace(sort) ? settings.Sort : sort;

        var esExe = FindEsExe();
        if (esExe is null)
        {
            return new SearchOutcome
            {
                Status = SearchStatus.EsExeNotFound,
                Error = "es.exe not found. Install Everything (or ES) and make sure es.exe is available.",
            };
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"EverythingPal-{Guid.NewGuid():N}.json");

        try
        {
            var psi = new ProcessStartInfo(esExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var arg in new[]
                     {
                         "-export-json", tempFile,
                         "-n", effectiveMax.ToString(CultureInfo.InvariantCulture),
                         "-sort", effectiveSort,
                         "-date-format", "1",
                         "-size",
                         "-date-modified",
                         "-attributes",
                         "-no-result-error",
                         "-timeout", "1500",
                         "-search", query,
                     })
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return new SearchOutcome { Status = SearchStatus.QueryFailed, Error = "Failed to start es.exe." };
            }

            // ES can block for the full -timeout when Everything's IPC is unhealthy, so cap it.
            if (!process.WaitForExit(2500))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return new SearchOutcome { Status = SearchStatus.Timeout, Error = "es.exe did not finish in time." };
            }

            var stderr = process.StandardError.ReadToEnd();
            var exitCode = process.ExitCode;

            return exitCode switch
            {
                0 => new SearchOutcome { Status = SearchStatus.Ok, Results = ReadResults(tempFile, query) },
                9 => new SearchOutcome { Status = SearchStatus.NoResults },
                8 => new SearchOutcome
                {
                    Status = SearchStatus.EverythingNotRunning,
                    Error = "Everything is not running (ES IPC not found).",
                },
                _ => new SearchOutcome
                {
                    Status = SearchStatus.QueryFailed,
                    Error = $"es.exe exited with {exitCode}: {stderr.Trim()}",
                },
            };
        }
        catch (Exception ex)
        {
            return new SearchOutcome { Status = SearchStatus.QueryFailed, Error = ex.Message };
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* ignore */ }
        }
    }

    private static List<EverythingResult> ReadResults(string path, string query)
    {
        var results = new List<EverythingResult>();
        if (!File.Exists(path))
        {
            return results;
        }

        using var stream = File.OpenRead(path);
        if (stream.Length == 0)
        {
            return results;
        }

        using var document = JsonDocument.Parse(stream);
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

        return Rank(results, query);
    }

    /// <summary>
    /// Everything has no relevance score; ES sorts by name. Re-rank so the most obvious
    /// match (exact name, prefix, word start, substring) comes first, folders first on ties.
    /// </summary>
    private static List<EverythingResult> Rank(List<EverythingResult> results, string query)
    {
        return [.. results
            .Select(r => (Result: r, Score: MatchScore(r.Name, query)))
            .OrderBy(t => t.Score)
            .ThenBy(t => t.Result.IsFolder ? 0 : 1)
            .ThenBy(t => t.Result.Name.Length)
            .Select(t => t.Result)];
    }

    private static int MatchScore(string name, string query)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        if (stem.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (stem.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 5;
    }
}

