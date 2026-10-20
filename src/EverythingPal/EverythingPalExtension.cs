// EverythingPal - Everything search for PowerToys Command Palette.
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace EverythingPal;

/// <summary>
/// Entry point of the extension. The GUID must match the CLSID in Package.appxmanifest
/// (both the com:Class Id and the appExtension CreateInstance ClassId).
/// </summary>
[Guid("a2b723c3-d9d7-4a91-941f-2314f448299d")]
public sealed partial class EverythingPalExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly EverythingPalCommandsProvider _provider = new();

    public EverythingPalExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
        AppLog.Write("EverythingPalExtension constructed");
    }

    public object? GetProvider(ProviderType providerType)
    {
        AppLog.Write($"GetProvider requested: {providerType}");
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose()
    {
        AppLog.Write("EverythingPalExtension disposing");
        _extensionDisposedEvent.Set();
    }
}
