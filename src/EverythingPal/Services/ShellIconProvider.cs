// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace EverythingPal.Services;

/// <summary>
/// Resolves the real shell file-type icon (the icon Explorer shows for a file type) and caches the
/// rendered PNG per extension, so building a result list never repeats shell / GDI+ work.
///
/// Design:
/// <list type="bullet">
/// <item>Icons are extracted at the <b>extension</b> level with
/// <c>SHGetFileInfo(..., SHGFI_USEFILEATTRIBUTES)</c> from a synthetic <c>"EverythingPal.ext"</c>
/// name. The shell therefore only consults the extension's registered icon: no disk access, no
/// thumbnailer, no per-file cost, and the same icon for every file of that type.</item>
/// <item>The HICON is rendered to a 32x32 PNG once; the PNG bytes are cached under the extension key
/// (folders share the generic folder icon; extension-less files share the generic file icon).
/// A fresh WinRT in-memory stream / <see cref="IconInfo"/> is created per <c>GetIconAsync</c> call so
/// item icons never share a mutable stream reference.</item>
/// <item>One extraction per key per process, guarded by <see cref="Lazy{T}"/> so concurrent searches
/// cannot race. Failures are cached as <c>null</c> and callers fall back to the Segoe glyph.</item>
/// <item>The cache is capped (FIFO eviction) so an unusual number of extensions cannot grow it
/// without bound; 128 keys of a few KB is well under a megabyte.</item>
/// </list>
///
/// Cost: a warm hit is a dictionary lookup + a tiny PNG copy (tens of microseconds, no I/O, no shell
/// call). The first time a type shows up costs one SHGetFileInfo + one 32x32 PNG encode
/// (roughly 1-3 ms) on a thread-pool thread; <see cref="WarmUpInBackground"/> extracts the common
/// types at extension start-up, and the caller resolves a whole page in parallel, so search results
/// are not delayed by more than a couple of milliseconds in the worst cold case.
///
/// Deliberately not done: path-based extraction (<c>ThumbnailHelper.GetThumbnail(path)</c>), which
/// would show per-file content thumbnails for images or embedded icons for .exe/.lnk/.ico. That
/// costs a shell call per distinct result path (e.g. 50 .exe hits in one page) and would put real
/// work on the search result path, so only the stable extension-level icon is used here.
/// </summary>
internal static class ShellIconProvider
{
    private const int MaxCachedIcons = 128;

    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiUseFileAttributes = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeDirectory = 0x00000010;

    /// <summary>Cache key for the generic folder icon.</summary>
    private const string FolderKey = "folder";

    /// <summary>Cache key for files that have no extension.</summary>
    private const string NoExtensionKey = "ext:";

    private static readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> PngCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> InsertionOrder = new();

    /// <summary>
    /// Extracts (or reuses) the shell icon for the file type of <paramref name="fullPath"/>.
    /// Returns <c>null</c> when the shell has no usable icon (callers fall back to a glyph).
    /// Never throws.
    /// </summary>
    internal static async Task<IconInfo?> GetIconAsync(string fullPath, bool isFolder)
    {
        string key;
        try
        {
            key = isFolder ? FolderKey : GetExtensionKey(fullPath);
        }
        catch (Exception ex)
        {
            AppLog.Write($"ShellIconProvider key failed for '{fullPath}': {ex.Message}");
            return null;
        }

        var pngBytes = await GetPngBytesAsync(key).ConfigureAwait(false);
        if (pngBytes is null)
        {
            return null;
        }

        try
        {
            return await CreateIconInfoAsync(pngBytes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Write($"ShellIconProvider stream failed for '{key}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Warms the cache for the most common file types in the background at extension start-up, so the
    /// first search (and especially the 200 ms home-screen fallback budget) normally gets cache hits.
    /// Fire-and-forget; never throws.
    /// </summary>
    internal static void WarmUpInBackground()
    {
        _ = Task.Run(
            async () =>
            {
                string[] keys =
                [
                    FolderKey, NoExtensionKey,
                    ".txt", ".md", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                    ".png", ".jpg", ".jpeg", ".gif", ".svg", ".mp4", ".mp3", ".zip", ".7z", ".rar",
                    ".exe", ".dll", ".lnk", ".json", ".xml", ".cs", ".js", ".ts", ".py", ".ps1",
                    ".html", ".css", ".log", ".csv", ".iso", ".img",
                ];

                foreach (var key in keys)
                {
                    try
                    {
                        await GetPngBytesAsync(key).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write($"ShellIconProvider warm-up failed for '{key}': {ex.Message}");
                    }
                }
            });
    }

    private static Task<byte[]?> GetPngBytesAsync(string key)
    {
        var countBefore = PngCache.Count;
        var lazy = PngCache.GetOrAdd(
            key,
            static k => new Lazy<Task<byte[]?>>(
                () => Task.Run(() => ExtractPng(k)),
                LazyThreadSafetyMode.ExecutionAndPublication));

        if (PngCache.Count != countBefore)
        {
            InsertionOrder.Enqueue(key);
        }

        TrimCache();
        return lazy.Value;
    }

    /// <summary>Bounded FIFO cache: evict the oldest keys once the cap is exceeded.</summary>
    private static void TrimCache()
    {
        while (PngCache.Count > MaxCachedIcons && InsertionOrder.TryDequeue(out var oldest))
        {
            PngCache.TryRemove(oldest, out _);
        }
    }

    private static byte[]? ExtractPng(string key)
    {
        try
        {
            return RenderIconPng(key);
        }
        catch (Exception ex)
        {
            AppLog.Write($"ShellIconProvider extract failed for '{key}': {ex.Message}");
            return null;
        }
    }

    private static string GetExtensionKey(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        if (string.IsNullOrEmpty(extension))
        {
            return NoExtensionKey;
        }

        extension = extension.TrimEnd('.').ToLowerInvariant();
        return extension.Length <= 1 ? NoExtensionKey : extension;
    }

    /// <summary>Extracts one shell icon and renders it as PNG bytes. Never throws.</summary>
    private static byte[]? RenderIconPng(string key)
    {
        var (syntheticPath, attributes) = key == FolderKey
            ? ("EverythingPalFolder", FileAttributeDirectory)
            : ($"EverythingPal{(key == NoExtensionKey ? string.Empty : key)}", FileAttributeNormal);

        var info = new SHFILEINFO();
        var result = SHGetFileInfo(
            syntheticPath,
            attributes,
            ref info,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            ShgfiIcon | ShgfiUseFileAttributes);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        // Icon.FromHandle does not own the HICON; the shell icon handle must be released here.
        var hIcon = info.hIcon;
        try
        {
            using var icon = Icon.FromHandle(hIcon);
            using var bitmap = icon.ToBitmap();
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            return png.ToArray();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>Copies the cached PNG into a fresh WinRT stream and wraps it as an image icon.</summary>
    private static async Task<IconInfo> CreateIconInfoAsync(byte[] pngBytes)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(pngBytes);
            await writer.StoreAsync().AsTask().ConfigureAwait(false);
            await writer.FlushAsync().AsTask().ConfigureAwait(false);
        }

        stream.Seek(0);
        // IconInfo.FromStream leaves IconInfo.Icon empty, so the host uses the PNG data (the image
        // path) instead of interpreting a glyph string.
        return IconInfo.FromStream(stream);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
