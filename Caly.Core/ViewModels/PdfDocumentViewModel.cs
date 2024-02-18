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

        [ObservableProperty] private PdfTextSelection _selection;

        private readonly Channel<PdfPageViewModel> _pageInfoChannel;
        private readonly ChannelWriter<PdfPageViewModel> _channelWriter;
        private readonly ChannelReader<PdfPageViewModel> _channelReader;

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
            _pageInfoChannel = Channel.CreateBounded<PdfPageViewModel>(new BoundedChannelOptions(_initialPagesInfoToLoad)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = false,
                SingleWriter = true
            });
            _channelWriter = _pageInfoChannel.Writer;
            _channelReader = _pageInfoChannel.Reader;

            Task.Run(() => ProcessPagesInfoQueue(_cts.Token));

            _pdfService = pdfService;
            PageCount = _pdfService.NumberOfPages;
            FileName = _pdfService.FileName;
            LocalPath = _pdfService.LocalPath;
            _selection = new PdfTextSelection(PageCount);
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

        public async Task LoadPages()
        {
            await Task.Run(async () =>
            {
                // Use 1st page size as default page size
                var firstPage = new PdfPageViewModel(1, _pdfService);
                await firstPage.LoadPageSize(_cts.Token);
                double defaultWidth = firstPage.Width;
                double defaultHeight = firstPage.Height;

                Pages.Add(firstPage);

                for (int p = 2; p <= PageCount; p++)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var newPage = new PdfPageViewModel(p, _pdfService) { Height = defaultHeight, Width = defaultWidth };
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

        public async Task LoadBookmarks()
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
        private async Task CopyText(CancellationToken token)
        {
            try
            {
                var selection = Selection;

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

                string text = await Task.Run(() =>
                {
                    System.Diagnostics.Debug.WriteLine("Starting SetClipboardAsync: Get text");
                    var sb = new StringBuilder();

                    foreach (var word in selection.GetDocumentSelectionAs(FullWord, PartialWord))
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
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(e.ToString());
                Exception = new ExceptionViewModel(e);
            }
        }
    }
}
