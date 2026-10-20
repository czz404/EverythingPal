// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>A single result returned by Everything's ES CLI.</summary>
public sealed class EverythingResult
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
