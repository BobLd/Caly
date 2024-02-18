using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Caly.Core.Services.Interfaces;

namespace Caly.Core.Services
{
    // https://github.com/AvaloniaUI/AvaloniaUI.QuickGuides/blob/main/IoCFileOps/Services/FilesService.cs

    internal sealed class FilesService : IFilesService
    {
        private readonly Window _target;
        private readonly IReadOnlyList<FilePickerFileType> _pdfFileFilter = new[] { FilePickerFileTypes.Pdf };

        public FilesService(Window target)
        {
            _target = target;
        }

        public async Task<IStorageFile?> OpenPdfFileAsync()
        {
            var files = await _target.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open",
                AllowMultiple = false,
                FileTypeFilter = _pdfFileFilter
            });

            return files.Count >= 1 ? files[0] : null;
        }

        public async Task<IStorageFile?> SavePdfFileAsync()
        {
            return await _target.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save Pdf File"
            });
        }

        public Task<IStorageFile?> TryGetFileFromPathAsync(string path)
        {
            return _target.StorageProvider.TryGetFileFromPathAsync(path);
        }
    }
}
