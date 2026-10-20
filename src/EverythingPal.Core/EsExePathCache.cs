// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

namespace EverythingPal.Core;

/// <summary>
/// Caches the last successfully resolved <c>es.exe</c> path so a keystroke does not rescan PATH and
/// the well-known install directories on every query.
///
/// The cache is keyed by the configured path (the effective value after the
/// <c>EVERYTHINGPAL_ES</c> environment fallback), so changing the setting invalidates it. Only a
/// successful lookup is stored; a miss is retried next time, which lets the user install Everything
/// after the extension started.
/// </summary>
public sealed class EsExePathCache
{
    private readonly object _gate = new();
    private string? _key;
    private string? _path;

    /// <summary>Gets the cached path for <paramref name="configured"/>, or <c>null</c> on a miss.</summary>
    public string? Get(string? configured)
    {
        lock (_gate)
        {
            return _path is not null && KeysEqual(_key, configured) ? _path : null;
        }
    }

    /// <summary>
    /// Stores a successful lookup. A <c>null</c>/empty path clears the entry instead: only successful
    /// lookups are cached.
    /// </summary>
    public void Set(string? configured, string? path)
    {
        lock (_gate)
        {
            _key = configured;
            _path = string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _key = null;
            _path = null;
        }
    }

    private static bool KeysEqual(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
