// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Services;

/// <summary>A single result returned by Everything's ES CLI.</summary>
internal sealed class EverythingResult
{
    public required string FullPath { get; init; }

    public bool IsFolder { get; init; }

    public long Size { get; init; }

    public DateTimeOffset? DateModified { get; init; }

    public uint Attributes { get; init; }

    /// <summary>File or folder name without the parent path.</summary>
    public string Name => IsFolder
        ? Path.GetFileName(Path.TrimEndingDirectorySeparator(FullPath))
        : Path.GetFileName(FullPath);

    /// <summary>Parent directory, used as the subtitle.</summary>
    public string Directory => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(FullPath)) ?? string.Empty;
}

internal enum SearchStatus
{
    Ok,
    NoResults,
    EverythingNotRunning,
    EsExeNotFound,
    QueryFailed,
    Timeout,
}

internal sealed class SearchOutcome
{
    public required SearchStatus Status { get; init; }

    public IReadOnlyList<EverythingResult> Results { get; init; } = [];

    public string? Error { get; init; }
}
