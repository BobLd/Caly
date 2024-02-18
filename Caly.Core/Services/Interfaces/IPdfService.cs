using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Caly.Core.Models;
using Caly.Core.Utilities;
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
    }
}
