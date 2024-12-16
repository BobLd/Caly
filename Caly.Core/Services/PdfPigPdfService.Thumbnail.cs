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

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Caly.Core.ViewModels;
using SkiaSharp;

namespace Caly.Core.Services
{
    internal sealed partial class PdfPigPdfService
    {
        private readonly ConcurrentDictionary<int, PdfPageViewModel> _bitmaps = new();
        
        private async Task SetThumbnail(PdfPageViewModel vm, SKPicture picture, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            int tWidth = (int)(vm.ThumbnailWidth / 1.5);
            int tHeight = (int)(vm.ThumbnailHeight / 1.5);

            if (tWidth <= 1 || tHeight <= 1)
            {
                tWidth = vm.ThumbnailWidth;
                tHeight = vm.ThumbnailHeight;
            }

            SKMatrix scale = SKMatrix.CreateScale(tWidth / (float)vm.Width, tHeight / (float)vm.Height);

            using (SKBitmap bitmap = new SKBitmap(tWidth, tHeight))
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                token.ThrowIfCancellationRequested();

                canvas.Clear(SKColors.White);
                canvas.DrawPicture(picture, ref scale);

                using (SKData d = bitmap.Encode(SKEncodedImageFormat.Jpeg, 50))
                await using (Stream stream = d.AsStream())
                {
                    vm.Thumbnail = Bitmap.DecodeToWidth(stream, vm.ThumbnailWidth,
                        BitmapInterpolationMode.LowQuality);

                    if (!_bitmaps.TryAdd(vm.PageNumber, vm))
                    {

                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[RENDER] Thumbnail Count {_bitmaps.Count}");
        }

        public void ClearAllThumbnail()
        {
            foreach (var p in _bitmaps.Keys.ToArray())
            {
                if (_bitmaps.TryRemove(p, out var vm))
                {
                    var bmp = vm.Thumbnail;
                    vm.Thumbnail = null;
                    bmp?.Dispose();
                }
            }
        }
    }
}
