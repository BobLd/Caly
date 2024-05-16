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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Caly.Core.Handlers;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.ViewModels
{
    public sealed partial class PdfDocumentViewModel : ViewModelBase
    {
        private static readonly double[] _zoomLevelsDiscrete = [0.125, 0.25, 0.33, 0.5, 0.67, 0.75, 1, 1.25, 1.5, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64];

        private const int _initialPagesInfoToLoad = 25;

        private readonly IPdfService _pdfService;
        private readonly CancellationTokenSource _cts = new();

        [ObservableProperty] private ObservableCollection<PdfPageViewModel> _pages = new();

        [ObservableProperty] private ObservableCollection<PdfBookmarkNode>? _bookmarks;

        [ObservableProperty] private PdfBookmarkNode? _selectedBookmark;

        [ObservableProperty] private int _selectedPageIndex = 1;

        [ObservableProperty] private int _pageCount;

        [ObservableProperty] private string? _fileName;

        [ObservableProperty] private double _zoomLevel = 1;

        [ObservableProperty] private ITextSelectionHandler _textSelectionHandler;

        /*
         * See PDF Reference 1.7 - C.2 Architectural limits
         * The magnification factor of a view should be constrained to be between approximately 8 percent and 6400 percent.
         */
#pragma warning disable CA1822
        public double MinZoomLevel => 0.08;
        public double MaxZoomLevel => 64;
#pragma warning restore CA1822

        private readonly ChannelWriter<PdfPageViewModel> _channelWriter;
        private readonly ChannelReader<PdfPageViewModel> _channelReader;

        private readonly Lazy<Task> _loadPagesTask;
        public Task LoadPagesTask => _loadPagesTask.Value;

        private readonly Lazy<Task> _loadBookmarksTask;
        public Task LoadBookmarksTask => _loadBookmarksTask.Value;

        internal string? LocalPath { get; private set; }

        private async Task ProcessPagesInfoQueue(CancellationToken token)
        {
            try
            {
                Debug.ThrowOnUiThread();

                await Parallel.ForEachAsync(_channelReader.ReadAllAsync(token), token, async (p, ct) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Processing task for page {p.PageNumber}.");
                    await p.LoadPageSize(ct);
                });
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in WorkerProc {e}");
                Debug.WriteExceptionToFile(e);
                Exception = new ExceptionViewModel(e);
            }
        }

        public PdfDocumentViewModel(IPdfService pdfService)
        {
            ArgumentNullException.ThrowIfNull(pdfService, nameof(pdfService));

            if (pdfService.NumberOfPages <= 0)
            {
                throw new ArgumentException(
                    $"Invalid number of pages in PdfPageService, got '{pdfService.NumberOfPages}'.");
            }

            // TODO - We could optimise here as we only need the channel if we have more than 1 page in the document
            Channel<PdfPageViewModel> pageInfoChannel = Channel.CreateBounded<PdfPageViewModel>(new BoundedChannelOptions(_initialPagesInfoToLoad)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = false,
                SingleWriter = true
            });
            _channelWriter = pageInfoChannel.Writer;
            _channelReader = pageInfoChannel.Reader;

            Task.Run(() => ProcessPagesInfoQueue(_cts.Token));

            _pdfService = pdfService;
            PageCount = _pdfService.NumberOfPages;
            FileName = _pdfService.FileName;
            LocalPath = _pdfService.LocalPath;

            TextSelectionHandler = new TextSelectionHandler(PageCount);

            _loadPagesTask = new Lazy<Task>(LoadPages);
            _loadBookmarksTask = new Lazy<Task>(LoadBookmarks);
        }

        /// <summary>
        /// Dispose objects.
        /// </summary>
        internal async Task CleanAfterClose()
        {
            Debug.ThrowOnUiThread();

            await Parallel.ForEachAsync(Pages, (p, _) => p.DisposeAsync());

            Pages.Clear();
            Bookmarks?.Clear();
            await _pdfService.DisposeAsync();

            _cts.Dispose();
        }

        internal async ValueTask CancelAsync()
        {
            await _cts.CancelAsync();
        }

        private async Task LoadPages()
        {
            await Task.Run(async () =>
            {
                // Use 1st page size as default page size
                var firstPage = new PdfPageViewModel(1, _pdfService, TextSelectionHandler);
                await firstPage.LoadPageSize(_cts.Token);
                double defaultWidth = firstPage.Width;
                double defaultHeight = firstPage.Height;

                Pages.Add(firstPage);

                for (int p = 2; p <= PageCount; p++)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var newPage = new PdfPageViewModel(p, _pdfService, TextSelectionHandler)
                    {
                        Height = defaultHeight,
                        Width = defaultWidth
                    };
                    Pages.Add(newPage);

                    if (p <= _initialPagesInfoToLoad)
                    {
                        // We limit loading page info to n first page
                        await _channelWriter.WriteAsync(newPage, _cts.Token);
                    }
                }
                _channelWriter.Complete();
            }, _cts.Token);
        }

        private async Task LoadBookmarks()
        {
            _cts.Token.ThrowIfCancellationRequested();
            Bookmarks = await Task.Run(() => _pdfService.GetPdfBookmark(_cts.Token));
        }

        private static IEnumerable<ReadOnlyMemory<char>> FullWord(PdfWord word)
        {
            return word.Letters.Select(l => l.Value);
        }

        private static IEnumerable<ReadOnlyMemory<char>> PartialWord(PdfWord word, int startIndex, int endIndex)
        {
            System.Diagnostics.Debug.Assert(startIndex != -1);
            System.Diagnostics.Debug.Assert(endIndex != -1);
            System.Diagnostics.Debug.Assert(startIndex <= endIndex);

            for (int l = startIndex; l <= endIndex; ++l)
            {
                yield return word.Letters![l].Value;
            }
        }

        [RelayCommand]
        private void GoToPreviousPage()
        {
            SelectedPageIndex = Math.Max(1, SelectedPageIndex - 1);
        }

        [RelayCommand]
        private void GoToNextPage()
        {
            SelectedPageIndex = Math.Min(PageCount, SelectedPageIndex + 1);
        }

        [RelayCommand]
        private void ZoomIn()
        {
            var index = Array.BinarySearch(_zoomLevelsDiscrete, ZoomLevel);
            if (index < -1)
            {
                ZoomLevel = Math.Min(MaxZoomLevel, _zoomLevelsDiscrete[~index]);
            }
            else
            {
                if (index >= _zoomLevelsDiscrete.Length - 1)
                {
                    return;
                }

                ZoomLevel = Math.Min(MaxZoomLevel, _zoomLevelsDiscrete[index + 1]);
            }
        }

        [RelayCommand]
        private void ZoomOut()
        {
            var index = Array.BinarySearch(_zoomLevelsDiscrete, ZoomLevel);
            if (index < -1)
            {
                ZoomLevel = Math.Max(MinZoomLevel, _zoomLevelsDiscrete[~index - 1]);
            }
            else
            {
                if (index == 0)
                {
                    return;
                }

                ZoomLevel = Math.Max(MinZoomLevel, _zoomLevelsDiscrete[index - 1]);
            }
        }

        [RelayCommand]
        private async Task CopyText(CancellationToken token)
        {
            try
            {
                var selection = TextSelectionHandler.Selection;

                if (!selection.HasStarted())
                {
                    return;
                }

                // https://docs.avaloniaui.net/docs/next/concepts/services/clipboardS

                var clipboardService = App.Current?.Services?.GetRequiredService<IClipboardService>();
                if (clipboardService is null)
                {
                    throw new NullReferenceException($"Missing {nameof(IClipboardService)} instance.");
                }

                System.Diagnostics.Debug.WriteLine("Starting SetClipboardAsync");

                string text = await Task.Run(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("Starting SetClipboardAsync: Get text");
                    var sb = new StringBuilder();

                    await foreach (var word in selection.GetDocumentSelectionAsAsync(FullWord, PartialWord, this, token))
                    {
                        // TODO - optimise IsWhiteSpace check
                        var isWhiteSpace = word.All(l => l.Span.IsEmpty || l.Span.IsWhiteSpace());

                        if (!isWhiteSpace)
                        {
                            foreach (var letter in word)
                            {
                                sb.Append(letter);
                            }
                        }

                        if (sb.Length == 0 || !char.IsWhiteSpace(sb[^1]))
                        {
                            sb.Append(' ');
                        }
                    }

                    sb.Length--; // Last char added was a space
                    return sb.ToString();
                }, token);

                System.Diagnostics.Debug.WriteLine("Starting SetClipboardAsync: Get text Done");

                await clipboardService.SetAsync(text);
                System.Diagnostics.Debug.WriteLine("Ended SetClipboardAsync");

                // TODO - We could unload the selection from memory
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(e.ToString());
                Exception = new ExceptionViewModel(e);
            }
        }
    }
}
