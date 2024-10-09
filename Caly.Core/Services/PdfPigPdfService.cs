using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
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
    internal sealed partial class PdfPigPdfService : IPdfService
    {
        private enum RenderRequestTypes
        {
            Picture = 0,
            Thumbnail = 1,
            Information = 2,
            TextLayer = 3
        }

        private sealed record RenderRequest(PdfPageViewModel Page, RenderRequestTypes Type, CancellationToken Token)
        {
        }

        private readonly BlockingCollection<RenderRequest> _pendingRenderRequests = new(new ConcurrentStack<RenderRequest>());

        private readonly IDialogService _dialogService;
        private readonly ITextSearchService _textSearchService;

        // PdfPig only allow to read 1 page at a time for now
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private MemoryStream? _fileStream;
        private PdfDocument? _document;
        private Uri? _filePath;

        public string? LocalPath => _filePath?.LocalPath;

        public string? FileName => Path.GetFileNameWithoutExtension(LocalPath);

        public int NumberOfPages { get; private set; }

        private readonly CancellationTokenSource _mainCts = new();

        private readonly Task _renderingLoopTask;

        public PdfPigPdfService(IDialogService dialogService, ITextSearchService textSearchService)
        {
            if (dialogService is null)
            {
                throw new NullReferenceException("Missing Dialog Service instance.");
            }

            _dialogService = dialogService;
            _textSearchService = textSearchService;

            _renderingLoopTask = Task.Run(RenderingLoop, _mainCts.Token);
        }

        private async Task RenderingLoop()
        {
            Debug.ThrowOnUiThread();

            // https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/blockingcollection-overview
            while (!_pendingRenderRequests.IsCompleted)
            {
                // Blocks if dataItems.Count == 0.
                // IOE means that Take() was called on a completed collection.
                // Some other thread can call CompleteAdding after we pass the
                // IsCompleted check but before we call Take.
                // In this example, we can simply catch the exception since the
                // loop will break on the next iteration.
                try
                {
                    RenderRequest renderRequest = _pendingRenderRequests.Take(_mainCts.Token);
                    switch (renderRequest.Type)
                    {
                        case RenderRequestTypes.Picture:
                            await ProcessPictureRequest(renderRequest);
                            break;

                        case RenderRequestTypes.Thumbnail:
                            await ProcessThumbnailRequest(renderRequest);
                            break;

                        case RenderRequestTypes.TextLayer:
                            await ProcessTextLayerRequest(renderRequest);
                            break;

                        default:
                            throw new NotImplementedException(renderRequest.Type.ToString());
                    }

                }
                catch (OperationCanceledException) { }
                catch (InvalidOperationException) { }
            }
        }

        private async Task ProcessTextLayerRequest(RenderRequest renderRequest)
        {
            await SetPageTextLayer(renderRequest.Page, renderRequest.Token);
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
                    //throw new ArgumentOutOfRangeException($"The loaded file '{Path.GetFileName(storageFile.Path.LocalPath)}' is not a pdf document.");
                }

                _filePath = storageFile.Path;
                System.Diagnostics.Debug.WriteLine($"[INFO] Opening {FileName}...");

                _fileStream = new MemoryStream();
                await using (var fs = await storageFile.OpenReadAsync())
                {
                    await fs.CopyToAsync(_fileStream, cancellationToken);
                    _fileStream.Position = 0;
                }

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

                    _document = PdfDocument.Open(_fileStream, pdfParsingOptions);
                    _document.AddPageFactory<PdfPageInformation, PageInformationFactory>();
                    _document.AddPageFactory<SKPicture, SkiaPageFactory>();
                    _document.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                    NumberOfPages = _document.NumberOfPages;
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
                    string? pw = await _dialogService.ShowPdfPasswordDialogAsync();
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

        public async Task SetPageTextLayer(PdfPageViewModel page, CancellationToken token)
        {
            page.PdfTextLayer ??= await GetTextLayerAsync(page.PageNumber, token);
            if (page.PdfTextLayer is not null)
            {
                // We ensure the correct selection is set now that we have the text layer
                page.TextSelectionHandler.Selection.SelectWordsInRange(page);
            }
        }

        public void AskPageTextLayer(PdfPageViewModel page, CancellationToken token)
        {
            _pendingRenderRequests.Add(new RenderRequest(page, RenderRequestTypes.TextLayer, token), token);
        }

        public async Task<PdfPageInformation?> GetPageInformationAsync(int pageNumber,
            CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            try
            {
                if (cancellationToken.IsCancellationRequested || isDiposed())
                {
                    return null;
                }

                await _semaphore.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                return _document!.GetPage<PdfPageInformation>(pageNumber);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                if (_semaphore.CurrentCount == 0 && !isDiposed())
                {
                    _semaphore.Release();
                }
            }
        }

        private async Task<PdfTextLayer?> GetTextLayerAsync(int pageNumber, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_document is null || isDiposed())
                {
                    return null;
                }

                await _semaphore.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                var page = _document.GetPage<PageTextLayerContent>(pageNumber);
                return PdfTextLayerHelper.GetTextLayer(page, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                if (_semaphore.CurrentCount == 0 && !isDiposed())
                {
                    _semaphore.Release();
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

                await _semaphore.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                if (!_document!.TryGetBookmarks(out var bookmarks) || bookmarks.Roots.Count == 0)
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
            finally
            {
                if (_semaphore.CurrentCount == 0 && !isDiposed())
                {
                    _semaphore.Release();
                }
            }
        }

        public async Task BuildIndex(PdfDocumentViewModel pdfDocument, IProgress<int> progress, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            await _textSearchService.BuildPdfDocumentIndex(pdfDocument, progress, cancellationToken);
        }

        public Task<IEnumerable<TextSearchResultViewModel>> SearchText(PdfDocumentViewModel pdfDocument, string query, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            return _textSearchService.Search(pdfDocument, query, cancellationToken);
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

            System.Diagnostics.Debug.WriteLine($"[INFO] Disposing document for {FileName}");

            _pendingRenderRequests.CompleteAdding();

            _semaphore.Dispose();

            _textSearchService.Dispose();

            if (_fileStream is not null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }

            if (_document is not null)
            {
                _document.Dispose();
                _document = null;
            }

            _pendingRenderRequests.Dispose();

            GC.KeepAlive(_renderingLoopTask);

            System.Diagnostics.Debug.Assert(_renderingLoopTask.IsCanceled ||
                                            _renderingLoopTask.IsCompleted ||
                                            _renderingLoopTask.IsCompletedSuccessfully ||
                                            _renderingLoopTask.IsFaulted);
        }

        public async ValueTask DisposeAsync()
        {
            Debug.ThrowOnUiThread();

            try
            {
                Interlocked.Increment(ref _isDisposed);
                
                System.Diagnostics.Debug.WriteLine($"[INFO] Disposing document async for {FileName}");

                _pendingRenderRequests.CompleteAdding();

                _semaphore.Dispose();

                _textSearchService.Dispose();

                if (_fileStream is not null)
                {
                    await _fileStream.DisposeAsync();
                    _fileStream = null;
                }

                if (_document is not null)
                {
                    _document.Dispose();
                    _document = null;
                }

                _pendingRenderRequests.Dispose();

                GC.KeepAlive(_renderingLoopTask);

                await _renderingLoopTask;
                
                System.Diagnostics.Debug.Assert(_renderingLoopTask.IsCanceled ||
                                                _renderingLoopTask.IsCompleted ||
                                                _renderingLoopTask.IsCompletedSuccessfully ||
                                                _renderingLoopTask.IsFaulted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] ERROR DisposeAsync for {FileName}: {ex.Message}");
            }
        }
    }
}
