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
using Caly.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel
    {
        private readonly Lazy<Task> _loadBookmarksTask;
        public Task LoadBookmarksTask => _loadBookmarksTask.Value;

        [ObservableProperty] private ObservableCollection<PdfBookmarkNode>? _bookmarks;

        [ObservableProperty] private PdfBookmarkNode? _selectedBookmark;

        private async Task LoadBookmarks()
        {
            _cts.Token.ThrowIfCancellationRequested();
            Bookmarks = await Task.Run(() => _pdfService.GetPdfBookmark(_cts.Token));
        }
    }
}
