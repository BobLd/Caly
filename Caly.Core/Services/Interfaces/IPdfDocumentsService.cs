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
        /// Open and load the pdf document.
        /// </summary>
        Task OpenLoadDocument(string? path, CancellationToken cancellationToken);

        Task CloseUnloadDocument(PdfDocumentViewModel? document);
    }
}
