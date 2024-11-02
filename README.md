## Caly Pdf Reader: A Fast, Cross-Platform Pdf Reader

**Caly Pdf Reader** is a free, cross-platform and open-source Pdf reader built with performance and efficiency in mind. Written in C# (net8.0 with AOT), it's designed to be lightweight, fast, and consume minimal memory.

> [!IMPORTANT]
> The development is currently in alpha.

![caly preview](https://github.com/user-attachments/assets/dfc4f96a-cd9a-436a-a540-1865c69b87d4)

### Cross-Platform Compatibility

Caly Pdf Reader leverages the power of [Avalonia UI](https://github.com/AvaloniaUI/Avalonia), [SkiaSharp](https://github.com/mono/SkiaSharp) and [PdfPig](https://github.com/UglyToad/PdfPig) to run seamlessly on Windows, macOS, and Linux.

As of now, only the Windows, Linux and Android versions have been tested. Android version runs, but is not optimised for the platfotm. Better mobile support is planned, including iOS.

## Key Features

* **Tabbed Interface:** Effortlessly manage multiple Pdf documents in separate tabs.
* **Lightning-Fast Navigation:** Navigate through pages with smooth performance.
* **Text Selection and Copy/Paste:** Select, copy, and paste text from Pdfs to the clipboard.
* **Powerful Search:** Quickly locate specific text within documents using the built-in search function.
* **Thumbnail View:** Get a visual overview of all pages with the intuitive thumbnail sidebar.
* **Bookmark Support:** Navigate through bookmarks for quick access to important sections.
* **Zoom In/Out:** Zoom in/out of the document.
* **Minimalist UI:** Enjoy a clean and distraction-free reading experience.

## Usage

1. Open the application and click on the "+" (New Tab) button to open a new Pdf document. You can also drag and drop your document or set Caly Pdf Reader as your default reader application.
2. Select a Pdf file to open using the file dialog.
3. The document will be displayed in the tab, and you can navigate through the pages using the navigation buttons.
4. To select text in the document, click and drag the mouse over the text.
5. To copy the selected text, right-click and select "Copy" or use `Ctrl+C`.
6. To search for text in the document, click on the "Search" button in the left menu and enter the text to search for. You can also use `Ctrl+F`.
7. To view page thumbnails, click on the "Thumbnails" button in the left menu.
8. To navigate through bookmarks, click on the "Bookmarks" button in the left menu.
9. To zoom in and out of your document, either use the scroll wheel while pressing `Ctrl`, or use the "Zoom" buttons in the top menu.
## Why Caly Pdf Reader?

* **Open Source:** The source code is freely available on GitHub, allowing for community contributions and customization.
* **Cross-Platform:** Run Caly Pdf Reader on your preferred operating system.
* **Lightweight & Fast:** Designed for optimal performance and minimal resource consumption.
* **Active Development:** We are constantly working on improving Caly Pdf Reader with new features and bug fixes.

![image](https://github.com/user-attachments/assets/e7d40369-b404-4144-a7dd-b66fb6f3c493)

## Getting Started

Caly Pdf Reader uses submodules, you will need to run the following command after cloning it:
```
git submodule update --init --recursive
```

> [!IMPORTANT]
> If the submodule branches appear as 'detached', you can re-link each of them to their respective `develop-caly` branches.

Caly Pdf Reader is a net8.0 application with Native AOT (ahead of time compilation) enabled. To publish the application, run the following (example for Windows):
```
dotnet publish -r win-x64 -c Release -f net8.0
```

## Contributing

We welcome contributions from the community! If you find a bug, have a feature request, or want to contribute code, please feel free to do it!

You can also help Caly Pdf Reader by contributing to:
- https://github.com/UglyToad/PdfPig
- https://github.com/BobLd/PdfPig.Rendering.Skia

## License

Caly Pdf Reader is licensed under the [GPL-3.0 License](https://github.com/BobLd/Caly?tab=GPL-3.0-1-ov-file#readme).
