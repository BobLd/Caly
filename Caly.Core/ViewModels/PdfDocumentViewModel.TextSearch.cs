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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Caly.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lifti;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel
    {
        private readonly Lazy<Task> _buildSearchIndex;
        private Task? _pendingSearchTask;
        private CancellationTokenSource? _pendingSearchTaskCts;

        private bool _isSearchQueryError;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BuildingIndex))]
        private int _buildIndexProgress;

        public bool BuildingIndex => BuildIndexProgress != 0 && BuildIndexProgress != 100;

        [ObservableProperty] private string _searchStatus;

        [ObservableProperty] private string? _textSearch;

        [ObservableProperty] private ObservableCollection<TextSearchResultViewModel> _searchResults = [];
        [ObservableProperty] private HierarchicalTreeDataGridSource<TextSearchResultViewModel> _searchResultsSource;

        [ObservableProperty] private TextSearchResultViewModel? _selectedTextSearchResult;

        async partial void OnTextSearchChanged(string? value)
        {
            await SearchText(); // TODO - subscribe to event change instead and use rolling time window
        }

        [RelayCommand]
        private void ActivateSearchTextTab()
        {
            IsPaneOpen = true;
            SelectedTabIndex = 2;
        }
        
        private async Task BuildSearchIndex()
        {
            _cts.Token.ThrowIfCancellationRequested();
            var progress = new Progress<int>(done =>
            {
                BuildIndexProgress = (int)Math.Ceiling((done / (double)PageCount) * 100);
            });

            await Task.Run(() => _pdfService.BuildIndex(this, progress, _cts.Token), _cts.Token);

            SetSearchStatusFinal();
        }

        /// <summary>
        /// Search the document.
        /// <para>
        /// Takes care of cancelling any search task currently running before starting the new one.
        /// </para>
        /// </summary>
        [RelayCommand]
        private async Task SearchText()
        {
            // https://stackoverflow.com/questions/18999827/a-pattern-for-self-cancelling-and-restarting-task

            try
            {
                var previousCts = _pendingSearchTaskCts;
                var newCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                _pendingSearchTaskCts = newCts;

                if (previousCts is not null)
                {
                    // cancel the previous session and wait for its termination
                    System.Diagnostics.Debug.WriteLine("cancel the previous session and wait for its termination");
                    await previousCts.CancelAsync();
                    try
                    {
                        await _pendingSearchTask;
                    }
                    catch (OperationCanceledException e)
                    {
                        System.Diagnostics.Debug.WriteLine(e);
                        throw;
                    }
                    catch
                    {
                         /* Ignore */
                    }
                }

                newCts.Token.ThrowIfCancellationRequested();
                _pendingSearchTask = SearchTextInternal(newCts.Token);
                await _pendingSearchTask;
            }
            catch (OperationCanceledException)
            { }
        }

        internal void SetSearchStatus(string status)
        {
            Dispatcher.UIThread.Post(() =>
            {
                SearchStatus = status;
            });
        }

        private void SetSearchStatusFinal()
        {
            if (string.IsNullOrEmpty(TextSearch))
            {
                SetSearchStatus("");
            }
            else if (SearchResults.Count == 0)
            {
                if (!_isSearchQueryError)
                {
                    SetSearchStatus("No Result Found");
                }
            }
            else
            {
                SetSearchStatus("");
            }
        }

        private async Task SearchTextInternal(CancellationToken token)
        {
            try
            {
                ActivateSearchTextTab();
                SelectedTextSearchResult = null;
                SearchResults.ClearSafely();

                Task indexBuildTask = _buildSearchIndex.Value;

                if (string.IsNullOrEmpty(TextSearch))
                {
                    SetSearchStatus("");
                    return;
                }

                Task searchTask = Task.Run(async () =>
                {
                    SetSearchStatus("Searching...");

                    bool indexBuildTaskComplete;
                    var pagesDone = new HashSet<int>();
                    do
                    {
                        token.ThrowIfCancellationRequested();
                        indexBuildTaskComplete = indexBuildTask.IsCompleted;
                        var searchResults = await _pdfService.SearchText(this, TextSearch, token);

                        foreach (var result in searchResults.OrderBy(r => r.PageNumber))
                        {
                            token.ThrowIfCancellationRequested();
                            if (result.PageNumber == -1)
                            {
                                break;
                            }

                            if (pagesDone.Contains(result.PageNumber))
                            {
                                continue;
                            }

                            SearchResults.AddSafely(result);
                            pagesDone.Add(result.PageNumber);
                        }

                        await Task.Delay(indexBuildTaskComplete ? 0 : 500, token);
                    } while (!indexBuildTaskComplete);
                }, token);

                if (!indexBuildTask.IsCompleted)
                {
                    await Task.WhenAny(indexBuildTask, searchTask);
                    if (indexBuildTask is { IsCompleted: true, Exception: not null })
                    {
                        throw new Exception("Something wrong happened while indexing the document.",
                            indexBuildTask.Exception);
                    }
                }
                else
                {
                    await searchTask;
                }

                _isSearchQueryError = searchTask is { IsCompleted: true, Exception: not null };

                if (_isSearchQueryError)
                {
                    SetSearchStatus(string.Join(' ', searchTask.Exception!.InnerExceptions.Select(e => e.Message)));
                }

                SetSearchStatusFinal();
            }
            catch (OperationCanceledException)
            { }
            catch (LiftiException qpe)
            {
                System.Diagnostics.Debug.Write(qpe.ToString());
                _isSearchQueryError = true;
                SetSearchStatus(qpe.Message);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(e.ToString());
                Exception = new ExceptionViewModel(e);
            }
        }

        private void TextSearchSelectionChanged(object? sender, Avalonia.Controls.Selection.TreeSelectionModelSelectionChangedEventArgs<TextSearchResultViewModel> e)
        {
            if (e.SelectedItems.Count == 0)
            {
                return;
            }

            SelectedTextSearchResult = e.SelectedItems[0];
        }
    }
}
