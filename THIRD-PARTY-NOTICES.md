# Third-party notices

This project redistributes / invokes the following third-party software:

## ES — Everything Command Line Interface

- Project: <https://github.com/voidtools/ES>
- License: MIT
- Copyright (c) voidtools
- Release builds of `es.exe` are packaged inside the MSIX (`Tools\es.exe`) and are downloaded
  at build time from the official GitHub releases. `es.exe` is only a client; it talks to a
  locally installed and running Everything instance.

### MIT License (ES)

```
MIT License

Copyright (c) voidtools

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Everything

- Project: <https://www.voidtools.com/>
- Not redistributed. Users install Everything themselves; the extension only queries it.

## Microsoft Command Palette Extension SDK / C#/WinRT / Shmuelie.WinRTServer

- Used as NuGet dependencies under their respective licenses (MIT).

## .NET runtime and libraries

- The MSIX is published **self-contained**, so it carries the .NET runtime and Microsoft
  libraries (MIT). Their license texts are available from <https://github.com/dotnet/runtime>.

## EverythingPal itself

- MIT — see [LICENSE](LICENSE), which is also packaged inside the MSIX next to this file.
