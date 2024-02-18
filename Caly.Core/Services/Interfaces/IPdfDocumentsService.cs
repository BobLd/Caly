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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Caly.Core.ViewModels;

namespace Caly.Core.Services.Interfaces
{
    public interface IPdfDocumentsService
    {
        /// <summary>
        /// Open and load pdf document through popup window.
        /// </summary>
        Task OpenLoadDocument(CancellationToken cancellationToken);

        /// <summary>
        /// Open and load the pdf document.
        /// </summary>
        Task OpenLoadDocument(IStorageFile? storageFile, CancellationToken cancellationToken);

        /// <summary>
        /// Open and load the pdf documents.
        /// </summary>
        Task OpenLoadDocuments(IEnumerable<IStorageItem?> storageFiles, CancellationToken cancellationToken);

        /// <summary>
        /// Open and load the pdf document.
        /// </summary>
        Task OpenLoadDocument(string? path, CancellationToken cancellationToken);

        Task CloseUnloadDocument(PdfDocumentViewModel? document);
    }
}
