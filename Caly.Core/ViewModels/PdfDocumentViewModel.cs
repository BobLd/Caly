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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Collections;
using Caly.Core.Handlers;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Caly.Core.ViewModels
{
    public sealed partial class PdfDocumentViewModel : ViewModelBase
    {
        private const int _initialPagesInfoToLoad = 25;

        private readonly IPdfService _pdfService;
        private readonly CancellationTokenSource _cts = new();

        [ObservableProperty] private ObservableCollection<PdfPageViewModel> _pages = [];

        [ObservableProperty] private int _selectedTabIndex;

        [ObservableProperty] private int? _selectedPageIndex = 1;

        [ObservableProperty] private int _pageCount;

        [ObservableProperty] private string? _fileName;

        [ObservableProperty] private ITextSelectionHandler _textSelectionHandler;

        [ObservableProperty] private ObservableCollection<TextSearchResultViewModel> _searchResults = [];

        [ObservableProperty] private string? _textSearch;

        [ObservableProperty] private TextSearchResultViewModel? _selectedTextSearchResult;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BuildingIndex))]
        private int _buildIndexProgress;

        public bool BuildingIndex => BuildIndexProgress != 0 && BuildIndexProgress != 100;

        private readonly ChannelWriter<PdfPageViewModel>? _channelWriter;
        private readonly ChannelReader<PdfPageViewModel>? _channelReader;

        private readonly Lazy<Task> _loadPagesTask;
        public Task LoadPagesTask => _loadPagesTask.Value;

        private readonly Lazy<Task> _buildSearchIndex;

        internal string? LocalPath { get; private set; }

        private async Task ProcessPagesInfoQueue(CancellationToken token)
        {
            try
            {
                Debug.ThrowOnUiThread();

                if (_channelReader is null)
                {
                    throw new NullReferenceException("Channel reader should not be null in ProcessPagesInfoQueue().");
                }

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

        private readonly IDisposable _searchResultsDisposable;

        public PdfDocumentViewModel(IPdfService pdfService)
        {
            ArgumentNullException.ThrowIfNull(pdfService, nameof(pdfService));

            if (pdfService.NumberOfPages <= 0)
            {
                throw new ArgumentException(
                    $"Invalid number of pages in PdfPageService, got '{pdfService.NumberOfPages}'.");
            }

            if (pdfService.NumberOfPages > 1)
            {
                // We only need the channel if we have more than 1 page in the document
                Channel<PdfPageViewModel> pageInfoChannel = Channel.CreateBounded<PdfPageViewModel>(
                    new BoundedChannelOptions(_initialPagesInfoToLoad)
                    {
                        AllowSynchronousContinuations = false,
                        FullMode = BoundedChannelFullMode.DropWrite,
                        SingleReader = false,
                        SingleWriter = true
                    });
                _channelWriter = pageInfoChannel.Writer;
                _channelReader = pageInfoChannel.Reader;

                Task.Run(() => ProcessPagesInfoQueue(_cts.Token));
            }

            _pdfService = pdfService;
            PageCount = _pdfService.NumberOfPages;
            FileName = _pdfService.FileName;
            LocalPath = _pdfService.LocalPath;

            TextSelectionHandler = new TextSelectionHandler(PageCount);

            _loadPagesTask = new Lazy<Task>(LoadPages);
            _loadBookmarksTask = new Lazy<Task>(LoadBookmarks);
            _buildSearchIndex = new Lazy<Task>(BuildSearchIndex);

            _searchResultsDisposable = SearchResults
                .GetWeakCollectionChangedObservable()
                .ObserveOn(Scheduler.Default)
                .Subscribe(e =>
                {
                    try
                    {
                        switch (e.Action)
                        {
                            case NotifyCollectionChangedAction.Reset:
                                TextSelectionHandler.ClearTextSearchResults(this);
                                break;

                            case NotifyCollectionChangedAction.Add:
                                if (e.NewItems?.Count > 0)
                                {
                                    var searchResult = e.NewItems.OfType<TextSearchResultViewModel>().ToArray();

                                    var first = searchResult.FirstOrDefault();

                                    if (first is null || first.PageNumber <= 0)
                                    {
                                        TextSelectionHandler.ClearTextSearchResults(this);
                                    }
                                    else
                                    {
                                        TextSelectionHandler.AddTextSearchResults(this, searchResult);
                                    }
                                }

                                if (e.OldItems?.Count > 0)
                                {
                                    throw new NotImplementedException($"SearchResults Action '{e.Action}' with OldItems.");
                                }
                                break;

                            case NotifyCollectionChangedAction.Remove:
                            case NotifyCollectionChangedAction.Replace:
                            case NotifyCollectionChangedAction.Move:
                                throw new NotImplementedException($"SearchResults Action '{e.Action}'.");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // No op
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteExceptionToFile(ex);
                        Exception = new ExceptionViewModel(ex);
                    }
                });
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
            _searchResultsDisposable.Dispose();
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

                if (PageCount > 1 && _channelWriter is null)
                {
                    throw new NullReferenceException("Document has more than 1 page, the channel writer should not be null.");
                }

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
                        await _channelWriter!.WriteAsync(newPage, _cts.Token);
                    }
                }
                _channelWriter?.Complete();
            }, _cts.Token);
        }

        private async Task BuildSearchIndex()
        {
            _cts.Token.ThrowIfCancellationRequested();
            var progress = new Progress<int>(done => 
            {
                BuildIndexProgress = (int)Math.Ceiling((done / (double)PageCount) * 100);
            });

            await Task.Run(() => _pdfService.BuildIndex(this, progress, _cts.Token), _cts.Token);
        }

        [RelayCommand]
        private void GoToPreviousPage()
        {
            if (!SelectedPageIndex.HasValue)
            {
                return;
            }
            SelectedPageIndex = Math.Max(1, SelectedPageIndex.Value - 1);
        }

        [RelayCommand]
        private void GoToNextPage()
        {
            if (!SelectedPageIndex.HasValue)
            {
                return;
            }
            SelectedPageIndex = Math.Min(PageCount, SelectedPageIndex.Value + 1);
        }
    }
}
