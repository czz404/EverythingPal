// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

public enum SearchStatus
{
    Ok,
    NoResults,
    EverythingNotRunning,
    EsExeNotFound,
    QueryFailed,
    Timeout,
}

/// <summary>Options for one ES query, normally derived from the extension settings.</summary>
public sealed class SearchOptions
{
    public string? EsExePath { get; init; }

    public int MaxResults { get; init; } = 50;

    public string Sort { get; init; } = "name-ascending";

    /// <summary>How long ES may wait for Everything's index; also used as the process timeout budget.</summary>
    public int IpcTimeoutMs { get; init; } = 1500;
}

public sealed class SearchOutcome
{
    public required SearchStatus Status { get; init; }

    public IReadOnlyList<EverythingResult> Results { get; init; } = [];

    public string? Error { get; init; }
}
