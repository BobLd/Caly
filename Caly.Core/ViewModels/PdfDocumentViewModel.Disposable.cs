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
using System.Threading.Tasks;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel : IAsyncDisposable, IDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Debug.ThrowOnUiThread();

            await Parallel.ForEachAsync(Pages, (p, _) => p.DisposeAsync());

            Pages.Clear();
            Bookmarks?.Clear();

            _cts.Dispose();

            await _pdfService.DisposeAsync();
            _searchResultsDisposable.Dispose();
        }

        public async void Dispose()
        {
            await DisposeAsync();
        }
    }
}
