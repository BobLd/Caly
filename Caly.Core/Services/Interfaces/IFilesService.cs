using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Caly.Core.Services.Interfaces
{
    public interface IFilesService
    {
        Task<IStorageFile?> OpenPdfFileAsync();

        Task<IStorageFile?> SavePdfFileAsync();

        Task<IStorageFile?> TryGetFileFromPathAsync(string path);
    }
}
