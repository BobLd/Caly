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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;
using SkiaSharp;

namespace Caly.Core.Services.Interfaces
{
    public interface IPdfService : IDisposable, IAsyncDisposable
    {
        int NumberOfPages { get; }

        string? FileName { get; }

        string? LocalPath { get; }

        /// <summary>
        /// Open the pdf document.
        /// </summary>
        /// <returns>The number of pages in the opened document. <c>0</c> if the document was not opened.</returns>
        Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken cancellationToken);

        Task<PdfPageInformation?> GetPageInformationAsync(int pageNumber, CancellationToken cancellationToken);

        Task<IRef<SKPicture>?> GetRenderPageAsync(int pageNumber, CancellationToken cancellationToken);

        Task<PdfTextLayer?> GetTextLayerAsync(int pageNumber, CancellationToken cancellationToken);

        Task<ObservableCollection<PdfBookmarkNode>?> GetPdfBookmark(CancellationToken cancellationToken);

        Task BuildIndex(PdfDocumentViewModel pdfDocument, CancellationToken cancellationToken);
        Task<IEnumerable<TextSearchResultViewModel>> SearchText(PdfDocumentViewModel pdfDocument, string query, CancellationToken cancellationToken);
    }
}
