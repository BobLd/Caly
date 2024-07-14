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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using CommunityToolkit.Mvvm.Input;
using Lifti.Querying;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel
    {
        async partial void OnTextSearchChanged(string? value)
        {
            await SearchText(CancellationToken.None); // TODO - subscribe to event change instead and use rolling time window
        }

        private Task? _pendingSearchTask;
        private CancellationTokenSource? _pendingSearchTaskCts;

        // https://stackoverflow.com/questions/18999827/a-pattern-for-self-cancelling-and-restarting-task
        [RelayCommand]
        private async Task SearchText(CancellationToken token) // TODO - To finish cancel/restart
        {
            try
            {
                var previousCts = _pendingSearchTaskCts;
                var newCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                _pendingSearchTaskCts = newCts;

                if (previousCts != null)
                {
                    // cancel the previous session and wait for its termination
                    System.Diagnostics.Debug.WriteLine("cancel the previous session and wait for its termination");
                    await previousCts.CancelAsync();
                    try { await _pendingSearchTask; } catch { /* Ignore */ }
                }

                newCts.Token.ThrowIfCancellationRequested();
                _pendingSearchTask = SearchTextInternal(newCts.Token);
                await _pendingSearchTask;
            }
            catch (OperationCanceledException e)
            {
                System.Diagnostics.Debug.WriteLine(e);
            }
        }

        private async Task SearchTextInternal(CancellationToken token)
        {
            try
            {
                SelectedTabIndex = 2;
                SelectedTextSearchResult = null;
                SearchResults.ClearSafely();

                if (string.IsNullOrEmpty(TextSearch))
                {
                    return;
                }

                Task indexBuildTask = _buildSearchIndex.Value;
                Task searchTask = Task.Run(async () =>
                {
                    bool indexBuildTaskComplete;
                    HashSet<int> pagesDone = new();
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

                    if (searchTask is { IsCompleted: true, Exception: not null })
                    {
                        throw searchTask.Exception;
                    }
                }
                else
                {
                    await searchTask;
                }
            }
            catch (QueryParserException qpe)
            {
                System.Diagnostics.Debug.Write(qpe.ToString());
                var dialogService = App.Current?.Services?.GetRequiredService<IDialogService>();
                if (dialogService is not null)
                {
                    dialogService.ShowNotification("Text Search Error", qpe.Message, NotificationType.Error);
                }

                if (SearchResults.Count == 0)
                {
                    // No match found
                    SearchResults.AddSafely(new TextSearchResultViewModel() { PageNumber = -99 });
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(e.ToString());
                Exception = new ExceptionViewModel(e);
            }
        }
    }
}
