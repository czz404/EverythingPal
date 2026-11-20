// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.ComponentModel;
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
    /// Synchronous compatibility entry point (used by <c>--search-test</c>). Prefer
    /// <see cref="SearchAsync"/> from UI code so a superseded query can cancel the child process.
    /// Never throws for search failures: they are returned as <see cref="SearchStatus"/>. A cancelled
    /// <paramref name="cancellationToken"/> is the one exception: it is propagated as
    /// <see cref="OperationCanceledException"/> so callers can stay silent.
    /// </summary>
    public static SearchOutcome Search(string query, SearchOptions? options = null, CancellationToken cancellationToken = default)
        => SearchAsync(query, options, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Runs one user query. Never throws for search failures: failures are returned as
    /// <see cref="SearchStatus"/>. Intended to be called off the UI thread.
    ///
    /// A plain single token is first searched with Everything 1.5's fuzzy modifiers
    /// (<c>nocase:ignorews:</c>, see <see cref="SearchQueryBuilder"/>) so "term1" finds "Term 1".
    /// If that run is empty (or failed in a retryable way), the original query is run once and its
    /// result preferred; this also keeps Everything 1.4 working, where unknown modifiers return zero
    /// results. Timeouts / "Everything not running" / missing es.exe are never retried.
    ///
    /// The token cancels both the wait and the es.exe process this query started. Cancellation is
    /// reported by throwing <see cref="OperationCanceledException"/>; a query that genuinely exceeds
    /// the deadline is reported as <see cref="SearchStatus.Timeout"/>.
    /// </summary>
    public static async Task<SearchOutcome> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SearchOptions();

        cancellationToken.ThrowIfCancellationRequested();

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

        // Both runs are ranked against the text the user actually typed, not against the modifier
        // expression ("nocase:ignorews:term1" would otherwise be compared with file names).
        var fuzzyExpression = SearchQueryBuilder.Build(normalized);
        var fuzzyOutcome = await RunEsAsync(esExe, fuzzyExpression, normalized, options, cancellationToken).ConfigureAwait(false);

        // The user may have typed a newer query while the fuzzy run was in flight; do not start the
        // 1.4 compatibility retry for a query nobody is waiting for anymore.
        cancellationToken.ThrowIfCancellationRequested();

        if (!SearchRetryPolicy.ShouldRetry(fuzzyOutcome, fuzzyExpression, normalized))
        {
            return fuzzyOutcome;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var originalOutcome = await RunEsAsync(esExe, normalized, normalized, options, cancellationToken).ConfigureAwait(false);
        return SearchRetryPolicy.Select(fuzzyOutcome, originalOutcome);
    }

    /// <summary>
    /// Runs one es.exe invocation with the given search expression and maps its result. Each run gets
    /// a fresh unique export file; the export is parsed (and ranked against
    /// <paramref name="rankingQuery"/>, which is the user-visible query rather than the ES expression).
    /// </summary>
    private static async Task<SearchOutcome> RunEsAsync(
        string esExe,
        string searchExpression,
        string rankingQuery,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tempFile = Path.Combine(Path.GetTempPath(), $"EverythingPal-{Guid.NewGuid():N}.json");
        Process? process = null;
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;

        // ONE deadline covers both "wait for es.exe to exit" and "drain stdout/stderr". A custom ES
        // wrapper can exit while a child it spawned keeps the inherited pipe open; awaiting the reads
        // without this deadline used to exceed the configured timeout by an arbitrary amount.
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var psi = new ProcessStartInfo(esExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,

                // -search* consumes the raw command-line tail, so the expression must not be passed
                // through ArgumentList (which would add its own quotes).
                Arguments = EsArguments.Build(searchExpression, tempFile, options).ToCommandLine(),
            };

            process = Process.Start(psi);
            if (process is null)
            {
                return new SearchOutcome { Status = SearchStatus.QueryFailed, Error = "Failed to start es.exe." };
            }

            // ES can block for the full -timeout when Everything's IPC is unhealthy, so cap it.
            var deadline = Math.Clamp(options.IpcTimeoutMs, 100, 10000) + 1000;
            deadlineCts.CancelAfter(deadline);

            // Start reading both pipes immediately. ES prints its help dump to stdout for some
            // failures; leaving that pipe unread can fill the buffer and block the process.
            stdoutTask = process.StandardOutput.ReadToEndAsync(deadlineCts.Token);
            stderrTask = process.StandardError.ReadToEndAsync(deadlineCts.Token);

            try
            {
                await process.WaitForExitAsync(deadlineCts.Token).ConfigureAwait(false);

                // The process has exited, but a wrapper's child may still hold the pipe. Both reads
                // stay under the same absolute deadline as the process wait.
                var stderr = await WaitForReadAsync(stderrTask, deadlineCts.Token).ConfigureAwait(false);
                _ = await WaitForReadAsync(stdoutTask, deadlineCts.Token).ConfigureAwait(false);

                var exitCode = process.ExitCode;

                // Only parse the export for exit 0; other codes produce no usable JSON. Exit 0 with zero
                // parsed items means "no matches" because of -no-result-error (see SearchOutcomeMapper).
                var parsed = exitCode == 0 ? EsResultParser.ParseFile(tempFile, rankingQuery, options.Sort) : null;

                return SearchOutcomeMapper.FromExitCode(exitCode, stderr, parsed);
            }
            catch (OperationCanceledException)
            {
                KillProcess(process);
                DisposeStandardStreams(process);
                Observe(stdoutTask);
                Observe(stderrTask);

                // A fresh keystroke must stay quiet; only a genuine deadline is a Timeout. Check the
                // caller token after the pipes are released so the UI never shows a stale timeout.
                cancellationToken.ThrowIfCancellationRequested();
                return new SearchOutcome { Status = SearchStatus.Timeout, Error = "es.exe did not finish in time." };
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation must reach the caller instead of being flattened into QueryFailed by the
            // generic handler below.
            throw;
        }
        catch (Exception ex)
        {
            return new SearchOutcome { Status = SearchStatus.QueryFailed, Error = ex.Message };
        }
        finally
        {
            // The export file may only be deleted after the child is gone: on cancellation/timeout
            // the kill happens above, but an unexpected exception can also leave it running.
            if (process is not null)
            {
                KillProcess(process);
                DisposeStandardStreams(process);
                Observe(stdoutTask);
                Observe(stderrTask);
                process.Dispose();
            }

            try { File.Delete(tempFile); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Awaits a pipe read under the same deadline as the process wait. If the deadline fires first,
    /// the read is left to the caller to dispose/cancel and an <see cref="OperationCanceledException"/>
    /// is thrown; this method never waits for a pipe held by an unrelated child process.
    /// </summary>
    private static async Task<string> WaitForReadAsync(Task<string> readTask, CancellationToken deadlineToken)
    {
        if (readTask.IsCompleted)
        {
            return await readTask.ConfigureAwait(false);
        }

        var deadlineTask = Task.Delay(Timeout.InfiniteTimeSpan, deadlineToken);
        var completed = await Task.WhenAny(readTask, deadlineTask).ConfigureAwait(false);
        if (!ReferenceEquals(completed, readTask))
        {
            deadlineToken.ThrowIfCancellationRequested();
        }

        return await readTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Kills this invocation's es.exe if it is still running, then waits a bounded time for it to
    /// exit. Handles the race where it exited just before the kill and a child that already outlived
    /// the process (the latter cannot be killed through this Process object).
    /// </summary>
    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Exited between HasExited and Kill.
                }
                catch (Win32Exception)
                {
                    // Access denied (already terminating / elevated child); fall through to the wait.
                }
            }

            // Bounded: after Kill the process should exit promptly. Never block the caller forever.
            _ = process.WaitForExit(2000);
        }
        catch (InvalidOperationException)
        {
            // No process associated / already exited.
        }
    }

    /// <summary>Closes the read ends of this invocation's pipes so lingering reads cannot hang.</summary>
    private static void DisposeStandardStreams(Process process)
    {
        try { process.StandardOutput.Dispose(); } catch { /* ignore */ }
        try { process.StandardError.Dispose(); } catch { /* ignore */ }
    }

    /// <summary>Marks a read task's exception as observed when it is abandoned after a timeout.</summary>
    private static void Observe(Task? task)
    {
        if (task is null)
        {
            return;
        }

        if (task.IsCompleted)
        {
            _ = task.Exception;
            return;
        }

        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
