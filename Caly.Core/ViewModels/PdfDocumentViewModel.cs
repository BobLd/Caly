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
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core.Handlers;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Caly.Core.ViewModels
{
    public sealed partial class PdfDocumentViewModel : ViewModelBase
    {
        private const int _initialPagesInfoToLoad = 25;

        private readonly IPdfService _pdfService;
        private readonly ISettingsService _settingsService;

        private readonly ChannelWriter<PdfPageViewModel>? _channelWriter;
        private readonly ChannelReader<PdfPageViewModel>? _channelReader;

        private readonly CancellationTokenSource _cts = new();

        private Task? _processPagesInfoQueueTask;

        internal string? LocalPath { get; private set; }

        [ObservableProperty] private ObservableCollection<PdfPageViewModel> _pages = [];

        [ObservableProperty] private bool _isPaneOpen = !CalyExtensions.IsMobilePlatform();

        [ObservableProperty] private double _paneSize;

        [ObservableProperty] private int _selectedTabIndex;

        /// <summary>
        /// Starts at <c>1</c>, ends at <see cref="PageCount"/>.
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GoToPreviousPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(GoToNextPageCommand))]
        private string _selectedPageIndexString = "1";

        private int? _selectedPageIndex = 1;

        /// <summary>
        /// Starts at <c>1</c>, ends at <see cref="PageCount"/>.
        /// </summary>
        public int? SelectedPageIndex
        {
            get
            {
                return _selectedPageIndex;
            }

            set
            {
                if (!SetProperty(ref _selectedPageIndex, value))
                {
                    return;
                }

                SelectedPageIndexString = value.HasValue ? value.Value.ToString("0") : string.Empty;
            }
        }
        
        [ObservableProperty] private int _pageCount;

        [ObservableProperty] private string? _fileName;

        [ObservableProperty] private string? _fileSize;

        [ObservableProperty] private ITextSelectionHandler _textSelectionHandler;
        
        private readonly Lazy<Task> _loadPagesTask;
        public Task LoadPagesTask => _loadPagesTask.Value;

        partial void OnPaneSizeChanged(double oldValue, double newValue)
        {
            _settingsService.SetProperty(CalySettings.CalySettingsProperty.PaneSize, newValue);
        }

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
            catch (OperationCanceledException)
            { }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in WorkerProc {e}");
                Debug.WriteExceptionToFile(e);
                Exception = new ExceptionViewModel(e);
            }
        }

        private readonly IDisposable _searchResultsDisposable;

        public PdfDocumentViewModel(IPdfService pdfService, ISettingsService settingsService)
        {
            ArgumentNullException.ThrowIfNull(pdfService, nameof(pdfService));
            ArgumentNullException.ThrowIfNull(settingsService, nameof(settingsService));

            System.Diagnostics.Debug.Assert(pdfService.NumberOfPages == 0);

            _pdfService = pdfService;
            _settingsService = settingsService;

            _paneSize = _settingsService.GetSettings().PaneSize;

            // We only need the channel if we have more than 1 page in the document?
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

            _loadPagesTask = new Lazy<Task>(LoadPages);
            _loadBookmarksTask = new Lazy<Task>(LoadBookmarks);
            _loadPropertiesTask = new Lazy<Task>(LoadProperties);
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

                                    var first = e.NewItems.OfType<TextSearchResultViewModel>().FirstOrDefault();

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

            SearchResultsSource = new HierarchicalTreeDataGridSource<TextSearchResultViewModel>(SearchResults)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<TextSearchResultViewModel>(
                        new TextColumn<TextSearchResultViewModel, string>(null, x => x.ToString()),
                        x => x.Nodes)
                }
            };
            
            Dispatcher.UIThread.Post(() =>
            {
                SearchResultsSource.RowSelection!.SingleSelect = true;
                SearchResultsSource.RowSelection.SelectionChanged += TextSearchSelectionChanged;
            });
        }

        /// <summary>
        /// Open the pdf document.
        /// </summary>
        /// <returns>The number of pages in the opened document. <c>0</c> if the document was not opened.</returns>
        public async Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token)
        {
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);

            int pageCount = await _pdfService.OpenDocument(storageFile, password, combinedCts.Token);

            if (pageCount == 0)
            {
                return pageCount;
            }

            PageCount = _pdfService.NumberOfPages;
            FileName = _pdfService.FileName;
            LocalPath = _pdfService.LocalPath;

            if (_pdfService.FileSize.HasValue)
            {
                FileSize = Helpers.FormatSizeBytes(_pdfService.FileSize.Value);
            }

            TextSelectionHandler = new TextSelectionHandler(PageCount);

            if (PageCount > 1)
            {
                // Fire and forget
                _processPagesInfoQueueTask = Task.Run(() => ProcessPagesInfoQueue(combinedCts.Token), combinedCts.Token);
            }

            return pageCount;
        }

        public void ClearAllThumbnails()
        {
            _pdfService.ClearAllThumbnail();
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
        
        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private void GoToPreviousPage()
        {
            if (!SelectedPageIndex.HasValue)
            {
                return;
            }
            SelectedPageIndex = Math.Max(1, SelectedPageIndex.Value - 1);
        }

        private bool CanGoToPreviousPage()
        {
            if (!SelectedPageIndex.HasValue)
            {
                return false;
            }

            return SelectedPageIndex.Value > 1;
        }

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private void GoToNextPage()
        {
            if (!SelectedPageIndex.HasValue)
            {
                return;
            }
            SelectedPageIndex = Math.Min(PageCount, SelectedPageIndex.Value + 1);
        }

        private bool CanGoToNextPage()
        {
            if (!SelectedPageIndex.HasValue)
            {
                return false;
            }

            return SelectedPageIndex.Value < PageCount;
        }
    }
}
