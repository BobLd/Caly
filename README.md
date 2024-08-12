# Caly Pdf Reader
## About
Caly Pdf Reader is a cross-platform pdf document reader application written in C# (net8.0 with AOT), only using [Avalonia](https://github.com/AvaloniaUI/Avalonia) & [SkiaSharp](https://github.com/mono/SkiaSharp) + [PdfPig](https://github.com/UglyToad/PdfPig) for the UI / pdf rendering part. It is released under GPL-3.0 license.

> [!IMPORTANT]
> The development is currently in alpha.

## Goal and Roadmap
The aim is to develop a C# cross-platform open source pdf reader/viewer application. Below is a table of the current progress:

| Task | Priority | Progress | Comment | TODO |
| ---- | -------- | -------- | ------- | ---- |
| View pdf document | High | Done | | Performance to improve |
| Open many documents with tab system | High | Done | Use [Tabalonia](https://github.com/egorozh/Tabalonia) | Bugs to fix - see https://github.com/BobLd/Caly/issues/11 |
| Navigate and zoom document (go to page, zoom in/out, etc.) | High | Done | | Performance to improve |
| Text selection | High | Done | | Improve rendering by merging words into lines |
| Copy text selection to clipboard | High | Done | | |
| Search text | High | Done | | Improve word matching |
| Printing | High | Work in Progress | See [branch](https://github.com/BobLd/Caly/tree/printing). Way more complicated than expected without using `System.Drawing`. See https://github.com/Zelenov/SharpIpp https://github.com/dotnet/maui/issues/9931 https://devblogs.microsoft.com/xamarin/native-printing-with-android/ | |
| Run JavaScript embedded in a PDF | Won't do | Not started | Use [Jint](https://github.com/sebastienros/jint) | |
| UI / UX | Low | Work in Progress | How the app feels | Need better colors, icons, transitions, etc. |
| OCR | Low | Not started | Use [RapidOCR](https://github.com/RapidAI/RapidOCR) onnx models, see [RapidOcrNet](https://github.com/BobLd/RapidOcrNet) | |
| Table extraction | Low | Not started  | Use [tabula-sharp](https://github.com/BobLd/tabula-sharp) or [camelot-sharp](https://github.com/BobLd/camelot-sharp) | |

## Windows, Linux, MacOS and mobile compatible
As of now, only the Windows, Linux and Android versions have been tested. Android version runs, but is not optimised for the platfotm. Better mobile support is planned, including iOS.

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
![image](https://github.com/BobLd/Caly/assets/38405645/b8a191fd-fd9f-49f3-823e-cc6acb0f2429)

