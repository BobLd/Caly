// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
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
        private readonly IDialogService _dialogService;
        private readonly ITextSearchService _textSearchService;

        // PdfPig only allow to read 1 page at a time for now
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private MemoryStream? _fileStream;
        private PdfDocument? _document;
        private Uri? _filePath;

        public string? LocalPath => _filePath?.LocalPath;

        public string? FileName => Path.GetFileNameWithoutExtension(LocalPath);

        public long? FileSize => _fileStream?.Length;

        public int NumberOfPages { get; private set; }

        public PdfPigPdfService(IDialogService dialogService, ITextSearchService textSearchService)
        {
            _dialogService = dialogService ?? throw new NullReferenceException("Missing Dialog Service instance.");
            _textSearchService = textSearchService;

            // Priority to rendering page
            _priorityRequests = [_pendingHighPriorityRequests, _pendingOtherRequests];

            var channel = Channel.CreateUnbounded<RenderRequest>();
            _requestsWriter = channel.Writer;
            _requestsReader = channel.Reader;

            _processingLoopTask = Task.Run(ProcessingLoop, _mainCts.Token);
            _enqueuingLoopTask = Task.Run(EnqueuingLoop, _mainCts.Token);
        }
        
        public async Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token)
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
                    await fs.CopyToAsync(_fileStream, token);
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
                }, token);
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

                    var pageCount = await OpenDocument(storageFile, pw, token);
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
        
        public async Task SetPageInformationAsync(PdfPageViewModel page, CancellationToken token)
        {
            Debug.ThrowOnUiThread();
            bool hasLock = false;

            try
            {
                token.ThrowIfCancellationRequested();

                if (IsDisposed())
                {
                    return;
                }

                await _semaphore.WaitAsync(token);
                hasLock = true;

                if (IsDisposed())
                {
                    return;
                }

                token.ThrowIfCancellationRequested();

                var info = _document!.GetPage<PdfPageInformation>(page.PageNumber);

                if (!token.IsCancellationRequested)
                {
                    page.Width = info.Width;
                    page.Height = info.Height;
                }
            }
            finally
            {
                if (hasLock && !IsDisposed())
                {
                    _semaphore.Release();
                }
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

        private async Task<PdfTextLayer?> GetTextLayerAsync(int pageNumber, CancellationToken token)
        {
            Debug.ThrowOnUiThread();
            bool hasLock = false;

            PageTextLayerContent? page;
            try
            {
                token.ThrowIfCancellationRequested();

                if (IsDisposed())
                {
                    return null;
                }

                await _semaphore.WaitAsync(token);
                hasLock = true;

                if (IsDisposed())
                {
                    return null;
                }

                token.ThrowIfCancellationRequested();

                page = _document.GetPage<PageTextLayerContent>(pageNumber);
            }
            finally
            {
                if (hasLock && !IsDisposed())
                {
                    _semaphore.Release();
                }
            }

            return page is null ? null : PdfTextLayerHelper.GetTextLayer(page, token);
        }

        public ValueTask SetDocumentPropertiesAsync(PdfDocumentViewModel document, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            if (_document is null || IsDisposed())
            {
                return ValueTask.CompletedTask;
            }

            var info = _document.Information;

            var others =
                _document.Information.DocumentInformationDictionary?.Data?
                    .Where(x => x.Value is not null)
                    .ToDictionary(x => x.Key,
                        x => x.Value.ToString()!);

            document.Properties = new PdfDocumentProperties()
            {
                PdfVersion = _document.Version.ToString("0.0"),
                Title = info.Title,
                Author = info.Author,
                CreationDate = FormatPdfDate(info.CreationDate),
                Creator = info.Creator,
                Keywords = info.Keywords,
                ModifiedDate = FormatPdfDate(info.ModifiedDate),
                Producer = info.Producer,
                Subject = info.Subject,
                Others = others
            };

            return ValueTask.CompletedTask;
        }

        private static string? FormatPdfDate(string? rawDate)
        {
            if (string.IsNullOrEmpty(rawDate))
            {
                return rawDate;
            }

            if (rawDate.StartsWith("D:"))
            {
                rawDate = rawDate.Substring(2, rawDate.Length - 2);
            }

            if (UglyToad.PdfPig.Util.DateFormatHelper.TryParseDateTimeOffset(rawDate, out DateTimeOffset offset))
            {
                return offset.ToString("yyyy-MM-dd HH:mm:ss zzz");
            }

            return rawDate;
        }

        public async Task SetPdfBookmark(PdfDocumentViewModel pdfDocument, CancellationToken token)
        {
            Debug.ThrowOnUiThread();
            bool hasLock = false;

            Bookmarks? bookmarks = null;

            try
            {
                token.ThrowIfCancellationRequested();

                if (IsDisposed())
                {
                    return;
                }

                await _semaphore.WaitAsync(token);
                hasLock = true;

                if (IsDisposed())
                {
                    return;
                }

                token.ThrowIfCancellationRequested();

                if (!_document!.TryGetBookmarks(out bookmarks))
                {
                    return;
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (hasLock && !IsDisposed())
                {
                    _semaphore.Release();
                }
            }

            try
            {
                if (IsDisposed() || bookmarks is null || bookmarks.Roots.Count == 0)
                {
                    return;
                }

                var children = new ObservableCollection<PdfBookmarkNode>();
                foreach (BookmarkNode node in bookmarks.Roots)
                {
                    var n = BuildPdfBookmarkNode(node, token);
                    if (n is not null)
                    {
                        children.Add(n);
                    }
                }

                pdfDocument.Bookmarks = children;
            }
            catch (OperationCanceledException) { }
        }

        public async Task BuildIndex(PdfDocumentViewModel pdfDocument, IProgress<int> progress, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            await _textSearchService.BuildPdfDocumentIndex(pdfDocument, progress, token);
        }

        public Task<IEnumerable<TextSearchResultViewModel>> SearchText(PdfDocumentViewModel pdfDocument, string query, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            return _textSearchService.Search(pdfDocument, query, token);
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

        private bool IsDisposed()
        {
            return Interlocked.Read(ref _isDisposed) != 0;
        }

        private long _isDisposed;

        [Conditional("DEBUG")]
        private static void AssertTokensCancelled(ConcurrentDictionary<int, CancellationTokenSource> tokens)
        {
            foreach (var kvp in tokens)
            {
                System.Diagnostics.Debug.Assert(kvp.Value.IsCancellationRequested);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Debug.ThrowOnUiThread();

            try
            {
                if (IsDisposed())
                {
                    System.Diagnostics.Debug.WriteLine($"[WARN] Trying to dispose but already disposed for {FileName}.");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[INFO] Disposing document async for {FileName}.");

                Interlocked.Increment(ref _isDisposed); // Flag as disposed

                await _mainCts.CancelAsync();

                AssertTokensCancelled(_thumbnailTokens);
                AssertTokensCancelled(_textLayerTokens);
                AssertTokensCancelled(_pictureTokens);

                _pendingOtherRequests.CompleteAdding();
                _pendingHighPriorityRequests.CompleteAdding();
                _requestsWriter.Complete();

                _semaphore.Dispose();

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

                await _processingLoopTask;
                await _enqueuingLoopTask;
                
                _pendingOtherRequests.Dispose();
                _pendingHighPriorityRequests.Dispose();

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] ERROR DisposeAsync for {FileName}: {ex.Message}");
            }
        }

        public async void Dispose()
        {
            await DisposeAsync();
        }
    }
}
