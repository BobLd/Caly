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
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _thumbnailTokens = new();

        private readonly ConcurrentDictionary<int, PdfPageViewModel> _bitmaps = new();
        
        private async Task ProcessThumbnailRequest(RenderRequest renderRequest)
        {
            if (renderRequest.Token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"[RENDER] [THUMBNAIL] Cancelled {renderRequest.Page.PageNumber}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RENDER] [THUMBNAIL] Start process {renderRequest.Page.PageNumber}");

            try
            {
                if (_thumbnailTokens.TryRemove(renderRequest.Page.PageNumber, out _))
                {
                    if (renderRequest.Page.Thumbnail is not null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RENDER] [THUMBNAIL] No need process {renderRequest.Page.PageNumber}");
                        return;
                    }

                    var picture = renderRequest.Page.PdfPicture?.Clone();
                    if (picture is not null)
                    {
                        await SetThumbnail(renderRequest.Page, picture.Item);
                        picture.Dispose();
                        return;
                    }

                    // Need to get picture first
                    using (picture = await GetRenderPageAsync(renderRequest.Page.PageNumber, renderRequest.Token))
                    {
                        if (picture is not null)
                        {
                            await SetThumbnail(renderRequest.Page, picture.Item);
                        }
                    }
                }
                else
                {
                    if (renderRequest.Page.Thumbnail is not null)
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[RENDER] [THUMBNAIL] Cancelled process {renderRequest.Page.PageNumber}");
                if (renderRequest.Page.Thumbnail is not null)
                {
                }
            }

            System.Diagnostics.Debug.WriteLine($"[RENDER] [THUMBNAIL] End process {renderRequest.Page.PageNumber}");
        }
        
        private async Task SetThumbnail(PdfPageViewModel vm, SKPicture picture)
        {
            int tWidth = (int)(vm.ThumbnailWidth / 1.5);
            int tHeight = (int)(vm.ThumbnailHeight / 1.5);

            SKMatrix scale = SKMatrix.CreateScale(tWidth / (float)vm.Width, tHeight / (float)vm.Height);

            using (SKBitmap bitmap = new SKBitmap(tWidth, tHeight))
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawPicture(picture, ref scale);

                using (SKData d = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100))
                await using (Stream stream = d.AsStream())
                {
                    vm.Thumbnail = Bitmap.DecodeToWidth(stream, vm.ThumbnailWidth, BitmapInterpolationMode.LowQuality);

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

        public void AskPageThumbnail(PdfPageViewModel page, CancellationToken token)
        {
            System.Diagnostics.Debug.WriteLine($"[RENDER] AskPageThumbnail {page.PageNumber}");
            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (_thumbnailTokens.TryAdd(page.PageNumber, pageCts))
            {
                _pendingRenderRequests.Add(new RenderRequest(page, RenderRequestTypes.Thumbnail, pageCts.Token), pageCts.Token);
            }
            else
            {

            }
            System.Diagnostics.Debug.WriteLine($"[RENDER] Thumbnail Count {_bitmaps.Count}");
        }

        public void AskRemoveThumbnail(PdfPageViewModel page)
        {
            System.Diagnostics.Debug.WriteLine($"[RENDER] AskRemoveThumbnail {page.PageNumber}");

            var thumbnail = page.Thumbnail;
            page.Thumbnail = null;

            if (_thumbnailTokens.TryRemove(page.PageNumber, out var cts))
            {
                System.Diagnostics.Debug.WriteLine($"[RENDER] REMOVED {page.PageNumber}");
                cts.Cancel();
                cts.Dispose();
            }

            if (_bitmaps.TryRemove(page.PageNumber, out var vm))
            {
                // Should always be null
                System.Diagnostics.Debug.Assert(vm.Thumbnail is null);
            }

            thumbnail?.Dispose();

            System.Diagnostics.Debug.WriteLine($"[RENDER] Thumbnail Count {_bitmaps.Count}");
        }
    }
}
