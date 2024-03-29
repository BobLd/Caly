# Caly Pdf Reader
## About
Caly Pdf Reader is a cross-platform pdf document reader application written in C# (net8.0 with AOT), only using [Avalonia](https://github.com/AvaloniaUI/Avalonia) & [SkiaSharp](https://github.com/mono/SkiaSharp) + [PdfPig](https://github.com/UglyToad/PdfPig). It is released under GPL-3.0 license.

The development is currently in alpha.

## Windows, Linux and MacOS compatible
As of now, only the Windows version has been tested. There are also future plans for mobile versions.

## Contributions are welcome
Feel free to contribute to the project! You can also help Caly Pdf Reader by contributing to:
- https://github.com/UglyToad/PdfPig
- https://github.com/BobLd/PdfPig.Rendering.Skia

## 
Caly Pdf Reader uses submodules, you will need to run the following after cloning it:
```
git submodule update --init --recursive
```

Caly Pdf Reader is a net8.0 application with Native AOT (ahead of time compilation) enabled. To publish the application, run the following (example for Windows):
```
dotnet publish -r win-x64 -c Release -f net8.0
```
