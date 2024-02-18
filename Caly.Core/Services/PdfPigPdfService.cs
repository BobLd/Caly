using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Pdf;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Rendering.Skia;

namespace Caly.Core.Services
{
    /// <summary>
    /// One instance per document.
    /// </summary>
    internal sealed class PdfPigPdfService : IPdfService
    {
        private readonly IDialogService _dialogService;

        // PdfPig only allow to read 1 page at a time for now
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        private FileStream? fileStream;
        private PdfDocument? document;
        private Uri? filePath;

        public string? LocalPath => filePath?.LocalPath;

        public string? FileName => Path.GetFileNameWithoutExtension(LocalPath);

        public int NumberOfPages { get; private set; }

        public PdfPigPdfService(IDialogService dialogService)
        {
            if (dialogService is null)
            {
                throw new NullReferenceException("Missing Dialog Service instance.");
            }

            _dialogService = dialogService;
        }

        public async Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            // TODO - Ensure method is called only once (one instance per document)

            try
            {
                if (storageFile is null)
                {
                    return 0; // no pdf loaded
                }

                if (Path.GetExtension(storageFile.Path.LocalPath) != ".pdf")
                {
                    throw new ArgumentOutOfRangeException($"The loaded file '{Path.GetFileName(storageFile.Path.LocalPath)}' is not a pdf document.");
                }

                filePath = storageFile.Path;
                System.Diagnostics.Debug.WriteLine($"[INFO] Opening {FileName}...");
                fileStream = (FileStream)await storageFile.OpenReadAsync();

                return await Task.Run(() =>
                {
                    var pdfParsingOptions = new ParsingOptions()
                    {
                        SkipMissingFonts = true,
                        Logger = new CalyPdfPigLogger(_dialogService)
                    };

                    if (!string.IsNullOrEmpty(password))
                    {
                        pdfParsingOptions.Password = password;
                    }

                    document = PdfDocument.Open(fileStream, pdfParsingOptions);
                    document.AddPageFactory<PdfPageInformation, PageInformationFactory>();
                    document.AddPageFactory<SKPicture, SkiaPageFactory>();
                    document.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                    NumberOfPages = document.NumberOfPages;
                    return NumberOfPages;
                }, cancellationToken);
            }
            catch (PdfDocumentEncryptedException)
            {
                if (!string.IsNullOrEmpty(password))
                {
                    // Only stay at first level, do not recurse: If password is NOT null, this is recursion
                    return 0;
                }

                bool shouldContinue = true;
                while (shouldContinue)
                {
                    string? pw = await Dispatcher.UIThread.InvokeAsync(_dialogService.ShowPdfPasswordDialogAsync);
                    Debug.ThrowOnUiThread();

                    shouldContinue = !string.IsNullOrEmpty(pw);
                    if (!shouldContinue)
                    {
                        continue;
                    }

                    var pageCount = await OpenDocument(storageFile, pw, cancellationToken);
                    if (pageCount > 0)
                    {
                        // Password OK and document opened
                        return pageCount;
                    }
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }

        public async Task<PdfPageInformation?> GetPageInformationAsync(int pageNumber, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            try
            {
                if (cancellationToken.IsCancellationRequested || isDiposed())
                {
                    return null;
                }

                await semaphore.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                return document!.GetPage<PdfPageInformation>(pageNumber);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                Debug.WriteExceptionToFile(ex);
                throw;
            }
            finally
            {
                if (semaphore.CurrentCount == 0 && !isDiposed())
                {
                    semaphore.Release();
                }
            }
        }

        public async Task<IRef<SKPicture>?> GetRenderPageAsync(int pageNumber, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            SKPicture? pic;
            try
            {
                if (cancellationToken.IsCancellationRequested || isDiposed())
                {
                    return null;
                }

                await semaphore.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                pic = document!.GetPage<SKPicture>(pageNumber);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                Debug.WriteExceptionToFile(ex);
                throw;
            }
            finally
            {
                if (semaphore.CurrentCount == 0 && !isDiposed())
                {
                    semaphore.Release();
                }
            }

            return pic is null ? null : RefCountable.Create(pic);
        }

        public async Task<PdfTextLayer?> GetTextLayerAsync(int pageNumber, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (document is null || isDiposed())
                {
                    return null;
                }

                await semaphore.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                var page = document.GetPage<PageTextLayerContent>(pageNumber);
                return PdfTextLayerHelper.GetTextLayer(page, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                Debug.WriteExceptionToFile(ex);
                throw;
            }
            finally
            {
                if (semaphore.CurrentCount == 0 && !isDiposed())
                {
                    semaphore.Release();
                }
            }
        }

        public async Task<ObservableCollection<PdfBookmarkNode>?> GetPdfBookmark(CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            try
            {
                if (cancellationToken.IsCancellationRequested || isDiposed())
                {
                    return null;
                }

                await semaphore.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                if (!document!.TryGetBookmarks(out var bookmarks) || bookmarks.Roots.Count == 0)
                {
                    return null;
                }

                var children = new ObservableCollection<PdfBookmarkNode>();
                foreach (BookmarkNode node in bookmarks.Roots)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var n = BuildPdfBookmarkNode(node, cancellationToken);
                    if (n is not null)
                    {
                        children.Add(n);
                    }
                }

                return children;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                Debug.WriteExceptionToFile(ex);
                throw;
            }
            finally
            {
                if (semaphore.CurrentCount == 0 && !isDiposed())
                {
                    semaphore.Release();
                }
            }
        }

        private static PdfBookmarkNode? BuildPdfBookmarkNode(BookmarkNode node, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int? pageNumber = null;
            if (node is DocumentBookmarkNode bookmarkNode)
            {
                pageNumber = bookmarkNode.PageNumber;
            }

            if (node.IsLeaf)
            {
                return new PdfBookmarkNode(node.Title, pageNumber, null);
            }

            var children = new List<PdfBookmarkNode>();
            foreach (var child in node.Children)
            {
                var n = BuildPdfBookmarkNode(child, cancellationToken);
                if (n is not null)
                {
                    children.Add(n);
                }
            }

            return new PdfBookmarkNode(node.Title, pageNumber, children.Count == 0 ? null : children);
        }

        private bool isDiposed()
        {
            return Interlocked.Read(ref _isDisposed) != 0;
        }

        private long _isDisposed;

        public void Dispose()
        {
            Debug.ThrowOnUiThread();

            Interlocked.Increment(ref _isDisposed);

            System.Diagnostics.Debug.WriteLine($"Disposing document for {FileName}");
            semaphore.Dispose();

            if (fileStream is not null)
            {
                fileStream.Dispose();
                fileStream = null;
            }

            if (document is not null)
            {
                document.Dispose();
                document = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Debug.ThrowOnUiThread();

            try
            {
                Interlocked.Increment(ref _isDisposed);

                System.Diagnostics.Debug.WriteLine($"Disposing document async for {FileName}");
                semaphore.Dispose();

                if (fileStream is not null)
                {
                    await fileStream.DisposeAsync();
                    fileStream = null;
                }

                if (document is not null)
                {
                    document.Dispose();
                    document = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR DisposeAsync for {FileName}: {ex.Message}");
            }
        }
    }
}
