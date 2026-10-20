// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Inputs collected from the machine (registry, running processes, environment) for locating the
/// Everything GUI executable. Kept as plain data so the ordering/probing logic is unit-testable
/// without touching this machine's registry or process list.
/// </summary>
public sealed class EverythingGuiDiscoveryInputs
{
    /// <summary>Executable name Discovery looks for (defaults to <c>Everything.exe</c>).</summary>
    public string ExecutableName { get; init; } = EverythingGuiDiscovery.DefaultExecutableName;

    /// <summary>
    /// Full paths of the executable of a running Everything process (highest priority: it is the
    /// build the user is actually using, which also covers portable installs).
    /// </summary>
    public IReadOnlyList<string> RunningProcessPaths { get; init; } = [];

    /// <summary>
    /// Values of <c>App Paths\Everything.exe</c> registry keys (HKCU, HKLM, WOW6432Node). May be a
    /// full executable path, a directory, or a <c>DisplayIcon</c>-style value with a <c>,0</c> suffix.
    /// </summary>
    public IReadOnlyList<string> AppPathEntries { get; init; } = [];

    /// <summary>Install locations from uninstall registry entries (<c>InstallLocation</c> / <c>DisplayIcon</c>).</summary>
    public IReadOnlyList<string> UninstallLocations { get; init; } = [];

    /// <summary>Well-known install directories such as <c>%ProgramFiles%\Everything</c>.</summary>
    public IReadOnlyList<string> CommonDirectories { get; init; } = [];

    /// <summary>The raw PATH environment variable (semicolon separated); probed last.</summary>
    public string? PathVariable { get; init; }
}

/// <summary>
/// Resolves the location of the Everything GUI executable (<c>Everything.exe</c>), shared by the
/// "start Everything" and "locate in Everything" actions.
///
/// Strategy (first existing candidate wins):
///   1. the executable of a running Everything process,
///   2. <c>App Paths\Everything.exe</c> registry values (per-user first, then machine),
///   3. uninstall registry install locations,
///   4. well-known install directories,
///   5. PATH.
/// Entries are normalized (quotes, <c>,0</c> icon suffixes, directories vs files) and deduplicated.
/// I/O is injected so the candidate/probe behaviour is fully testable.
/// </summary>
public static class EverythingGuiDiscovery
{
    public const string DefaultExecutableName = "Everything.exe";

    private static readonly char[] PathSeparators = ['\\', '/'];

    /// <summary>Builds the ordered, normalized, deduplicated candidate list (no file-system probing).</summary>
    public static IReadOnlyList<string> BuildCandidates(EverythingGuiDiscoveryInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var executableName = string.IsNullOrWhiteSpace(inputs.ExecutableName)
            ? DefaultExecutableName
            : inputs.ExecutableName.Trim();

        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddFromEntry(string? entry)
        {
            var candidate = NormalizeCandidate(entry, executableName);
            if (candidate is not null && seen.Add(candidate))
            {
                candidates.Add(candidate);
            }
        }

        void AddFromDirectory(string? directory)
        {
            var candidate = CombineDirectory(directory, executableName);
            if (candidate is not null && seen.Add(candidate))
            {
                candidates.Add(candidate);
            }
        }

        foreach (var path in inputs.RunningProcessPaths ?? [])
        {
            AddFromEntry(path);
        }

        foreach (var entry in inputs.AppPathEntries ?? [])
        {
            AddFromEntry(entry);
        }

        foreach (var location in inputs.UninstallLocations ?? [])
        {
            AddFromEntry(location);
        }

        foreach (var directory in inputs.CommonDirectories ?? [])
        {
            AddFromDirectory(directory);
        }

        foreach (var directory in SplitPathVariable(inputs.PathVariable))
        {
            AddFromDirectory(directory);
        }

        return candidates;
    }

    /// <summary>
    /// Returns the first candidate that exists, or <c>null</c> when Everything is not installed /
    /// cannot be located. <paramref name="fileExists"/> defaults to <see cref="File.Exists"/>.
    /// </summary>
    public static string? Find(EverythingGuiDiscoveryInputs inputs, Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        fileExists ??= File.Exists;
        foreach (var candidate in BuildCandidates(inputs))
        {
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Normalizes one raw entry: trims whitespace/quotes, strips the <c>,&lt;index&gt;</c> suffix used by
    /// registry <c>DisplayIcon</c> values, and appends the executable name when the entry is a directory.
    /// </summary>
    internal static string? NormalizeCandidate(string? entry, string executableName)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return null;
        }

        var value = entry.Trim();

        // Registry values are occasionally stored quoted: "\"C:\Program Files\Everything\Everything.exe\"".
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1].Trim();
        }

        // DisplayIcon may be "path\Everything.exe,0" (optionally with quotes around either part).
        var comma = value.LastIndexOf(',');
        if (comma >= 0)
        {
            var suffix = value[(comma + 1)..].Trim().Trim('"');
            if (suffix.Length > 0 && IsAllDigits(suffix))
            {
                value = value[..comma].Trim().Trim('"');
            }
        }

        if (value.Length == 0)
        {
            return null;
        }

        value = Environment.ExpandEnvironmentVariables(value);

        try
        {
            value = value.TrimEnd(PathSeparators);
            if (value.Length == 0)
            {
                return null;
            }

            // Anything that already names the executable is used as-is (this also tolerates a
            // differently-cased spelling); everything else is treated as a directory.
            return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? value
                : Path.Combine(value, executableName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Invalid path from the registry: skip the candidate rather than fail the whole lookup.
            return null;
        }
    }

    private static string? CombineDirectory(string? directory, string executableName)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            var trimmed = Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"').TrimEnd(PathSeparators));
            return trimmed.Length == 0 ? null : Path.Combine(trimmed, executableName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SplitPathVariable(string? pathVariable)
    {
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            yield break;
        }

        foreach (var entry in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim().Trim('"');
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    private static bool IsAllDigits(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
