# Caly Pdf Reader
## About
Caly Pdf Reader is a cross-platform pdf document reader application written in C# (net8.0 with AOT), only using [Avalonia](https://github.com/AvaloniaUI/Avalonia) & [SkiaSharp](https://github.com/mono/SkiaSharp) + [PdfPig](https://github.com/UglyToad/PdfPig). It is released under GPL-3.0 license.

The development is currently in alpha.

## Goal
The aim is to develop a C# cross-platform open source pdf reader/viewer application, with the following abilities:
- View pdf document - Work in Progress
- Open many documents with a tab system - Work in Progress
- Navigate and zoom document (go to page, zoom in/out, etc.) - Work in Progress
- Select text and copy to clipboard - Work in Progress
- Search text - Development not started

Other plans for later:
- OCR
- Table extraction


## Windows, Linux and MacOS compatible
As of now, only the Windows version has been tested. There are also future plans for mobile versions.

## Contributions are welcome
Feel free to contribute to the project! You can also help Caly Pdf Reader by contributing to:
- https://github.com/UglyToad/PdfPig
- https://github.com/BobLd/PdfPig.Rendering.Skia

## Clone and build
Caly Pdf Reader uses submodules, you will need to run the following after cloning it:
```
git submodule update --init --recursive
```

Caly Pdf Reader is a net8.0 application with Native AOT (ahead of time compilation) enabled. To publish the application, run the following (example for Windows):
```
dotnet publish -r win-x64 -c Release -f net8.0
```

## Preview
![image](https://github.com/BobLd/Caly/assets/38405645/b7511f10-8f18-4249-829e-923b79bad219)

