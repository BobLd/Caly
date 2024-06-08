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
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Caly.Core.Services.Interfaces;

namespace Caly.Core.Services
{
    // https://github.com/AvaloniaUI/AvaloniaUI.QuickGuides/blob/main/IoCFileOps/Services/FilesService.cs

    internal sealed class FilesService : IFilesService
    {
        private readonly Visual _target;
        private readonly IReadOnlyList<FilePickerFileType> _pdfFileFilter = new[] { FilePickerFileTypes.Pdf };

        public FilesService(Visual target)
        {
            _target = target;
        }

        public async Task<IStorageFile?> OpenPdfFileAsync()
        {
            TopLevel? top = TopLevel.GetTopLevel(_target);
            if (top is null)
            {
                return null;
            }

            IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open",
                AllowMultiple = false,
                FileTypeFilter = _pdfFileFilter
            });

            return files.Count >= 1 ? files[0] : null;
        }

        public Task<IStorageFile?> SavePdfFileAsync()
        {
            TopLevel? top = TopLevel.GetTopLevel(_target);
            if (top is null)
            {
                return Task.FromResult<IStorageFile?>(null);
            }

            return top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save Pdf File"
            });
        }

        public Task<IStorageFile?> TryGetFileFromPathAsync(string path)
        {
            TopLevel? top = TopLevel.GetTopLevel(_target);
            return top is null ? Task.FromResult<IStorageFile?>(null) : top.StorageProvider.TryGetFileFromPathAsync(path);
        }
    }
}
