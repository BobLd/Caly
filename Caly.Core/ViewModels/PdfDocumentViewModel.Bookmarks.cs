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
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Media;
using Avalonia.Threading;
using Caly.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel
    {
        private readonly Lazy<Task> _loadBookmarksTask;
        public Task LoadBookmarksTask => _loadBookmarksTask.Value;

        [ObservableProperty] private ObservableCollection<PdfBookmarkNode>? _bookmarks;
        [ObservableProperty] private HierarchicalTreeDataGridSource<PdfBookmarkNode> _bookmarksSource;

        [ObservableProperty] private PdfBookmarkNode? _selectedBookmark;

        private async Task LoadBookmarks()
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Run(() => _pdfService.SetPdfBookmark(this, _cts.Token));
            if (Bookmarks?.Count > 0)
            {
                BookmarksSource = new HierarchicalTreeDataGridSource<PdfBookmarkNode>(Bookmarks)
                {
                    Columns =
                    {
                        new HierarchicalExpanderColumn<PdfBookmarkNode>(
                            new TextColumn<PdfBookmarkNode, string>(null,
                                x => x.Title, options: new TextColumnOptions<PdfBookmarkNode>()
                                {
                                    CanUserSortColumn = false,
                                    IsTextSearchEnabled = false,
                                    TextWrapping = TextWrapping.WrapWithOverflow,
                                    TextAlignment = TextAlignment.Left,
                                    MaxWidth = new GridLength(400)
                                }), x => x.Nodes)
                    }
                };

                Dispatcher.UIThread.Post(() =>
                {
                    BookmarksSource.RowSelection!.SingleSelect = true;
                    BookmarksSource.RowSelection.SelectionChanged += BookmarksSelectionChanged;
                    BookmarksSource.ExpandAll();
                });
            }
        }

        private void BookmarksSelectionChanged(object? sender, Avalonia.Controls.Selection.TreeSelectionModelSelectionChangedEventArgs<PdfBookmarkNode> e)
        {
            if (e.SelectedItems.Count == 0)
            {
                return;
            }

            SelectedBookmark = e.SelectedItems[0];
        }
    }
}
