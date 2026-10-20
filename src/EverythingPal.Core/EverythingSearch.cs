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

    private static readonly string[] EsCandidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), EsDefaultRelativePath),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "es.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything 1.5a", "es.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "es.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything 1.5a", "es.exe"),
    ];

    /// <summary>
    /// Locates es.exe, in this order:
    ///   1. explicit setting or EVERYTHINGPAL_ES environment variable;
    ///   2. the copy shipped inside the MSIX (<c>Tools\es.exe</c> next to the extension exe);
    ///   3. well-known Everything install locations;
    ///   4. PATH.
    /// The bundled copy is what makes the extension work without users installing ES separately.
    /// </summary>
    public static string? FindEsExe(string? configured = null, string? baseDirectory = null)
    {
        configured = string.IsNullOrWhiteSpace(configured)
            ? Environment.GetEnvironmentVariable("EVERYTHINGPAL_ES")
            : configured;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
            {
                return configured;
            }

            // Allow pointing at the containing directory as well.
            var inDirectory = Path.Combine(configured, "es.exe");
            if (File.Exists(inDirectory))
            {
                return inDirectory;
            }
        }

        var bundled = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "Tools", "es.exe");
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
                var candidate = Path.Combine(directory.Trim('"'), "es.exe");
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

            return exitCode switch
            {
                0 => new SearchOutcome { Status = SearchStatus.Ok, Results = EsResultParser.ParseFile(tempFile, normalized) },
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
}


