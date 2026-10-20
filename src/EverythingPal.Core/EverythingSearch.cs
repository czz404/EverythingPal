// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace EverythingPal.Core;

/// <summary>
/// Runs one Everything query through the official <c>es.exe</c> CLI.
///
/// Why the CLI: it avoids redistributing the voidtools native SDK DLLs and works with any
/// Everything 1.4/1.5 install that ships ES. Process start dominates latency (~43 ms on this
/// machine); the query itself is a few ms, so callers debounce.
/// </summary>
public sealed class EverythingSearch
{
    private const string EsDefaultRelativePath = @"Microsoft\WindowsApps\es.exe";
    private const string EsExeFileName = "es.exe";

    private static readonly string[] EsCandidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), EsDefaultRelativePath),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", EsExeFileName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything 1.5a", EsExeFileName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", EsExeFileName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything 1.5a", EsExeFileName),
    ];

    /// <summary>
    /// Last successful es.exe lookup. PATH may contain dozens of directories and is otherwise
    /// re-scanned for every debounced keystroke; the cache is keyed by the configured path and only
    /// stores hits (a missing es.exe is retried on the next query).
    /// </summary>
    private static readonly EsExePathCache Cache = new();

    /// <summary>
    /// Locates es.exe, in this order:
    ///   1. explicit setting or EVERYTHINGPAL_ES environment variable;
    ///   2. the copy shipped inside the MSIX (<c>Tools\es.exe</c> next to the extension exe);
    ///   3. well-known Everything install locations;
    ///   4. PATH.
    /// The bundled copy is what makes the extension work without users installing ES separately.
    /// Successful lookups are cached until the configured path changes (or the file disappears).
    /// </summary>
    public static string? FindEsExe(string? configured = null, string? baseDirectory = null)
    {
        var effectiveConfigured = string.IsNullOrWhiteSpace(configured)
            ? Environment.GetEnvironmentVariable("EVERYTHINGPAL_ES")
            : configured;

        // baseDirectory is a test seam, so it is part of the key to keep temporary layouts apart.
        var cacheKey = $"{effectiveConfigured}\u0000{baseDirectory}";

        var cached = Cache.Get(cacheKey);
        if (cached is not null && File.Exists(cached))
        {
            return cached;
        }

        var found = FindEsExeUncached(effectiveConfigured, baseDirectory);
        if (found is not null)
        {
            Cache.Set(cacheKey, found);
        }

        return found;
    }

    private static string? FindEsExeUncached(string? configured, string? baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
            {
                return configured;
            }

            // Allow pointing at the containing directory as well.
            var inDirectory = Path.Combine(configured, EsExeFileName);
            if (File.Exists(inDirectory))
            {
                return inDirectory;
            }
        }

        var bundled = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "Tools", EsExeFileName);
        if (File.Exists(bundled))
        {
            return bundled;
        }

        foreach (var candidate in EsCandidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), EsExeFileName);
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
    public static SearchOutcome Search(string query, SearchOptions? options = null)
    {
        options ??= new SearchOptions();

        var normalized = QueryNormalizer.Normalize(query);
        if (normalized.Length == 0)
        {
            return new SearchOutcome { Status = SearchStatus.NoResults };
        }

        var esExe = FindEsExe(options.EsExePath);
        if (esExe is null)
        {
            return new SearchOutcome
            {
                Status = SearchStatus.EsExeNotFound,
                Error = "es.exe not found. Install Everything (or ES) and set its path in the settings.",
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

            foreach (var argument in EsArguments.Build(normalized, tempFile, options))
            {
                psi.ArgumentList.Add(argument);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return new SearchOutcome { Status = SearchStatus.QueryFailed, Error = "Failed to start es.exe." };
            }

            // ES can block for the full -timeout when Everything's IPC is unhealthy, so cap it.
            var deadline = Math.Clamp(options.IpcTimeoutMs, 100, 10000) + 1000;
            if (!process.WaitForExit(deadline))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return new SearchOutcome { Status = SearchStatus.Timeout, Error = "es.exe did not finish in time." };
            }

            var stderr = process.StandardError.ReadToEnd();
            var exitCode = process.ExitCode;

            // Only parse the export for exit 0; other codes produce no usable JSON. Exit 0 with zero
            // parsed items means "no matches" because of -no-result-error (see SearchOutcomeMapper).
            var parsed = exitCode == 0 ? EsResultParser.ParseFile(tempFile, normalized) : null;

            return SearchOutcomeMapper.FromExitCode(exitCode, stderr, parsed);
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
}
