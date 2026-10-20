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

    /// <summary>
    /// File or folder name without the parent path.
    ///
    /// Drive roots are special: <see cref="Path.GetFileName(string)"/> returns an empty string for
    /// <c>D:\</c> / <c>D:</c> (a root has no file name), which used to leave the list item title
    /// blank. Roots fall back to the volume name, e.g. <c>D:</c>.
    /// </summary>
    public string Name
    {
        get
        {
            var path = IsFolder ? Path.TrimEndingDirectorySeparator(FullPath) : FullPath;
            var name = Path.GetFileName(path);
            return name.Length > 0
                ? name
                : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    /// <summary>Parent directory, used as the subtitle.</summary>
    public string Directory => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(FullPath)) ?? string.Empty;
}
